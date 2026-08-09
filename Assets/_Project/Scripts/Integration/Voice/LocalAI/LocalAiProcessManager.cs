using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using PawsAndLoot.Audio;
using UnityEngine;
using UnityEngine.Networking;

namespace PawsAndLoot.Integration.Voice
{
    public enum LocalAiRuntimeState
    {
        Unavailable,
        Starting,
        LoadingModels,
        Ready,
        Degraded,
        Error
    }

    [DisallowMultipleComponent]
    public sealed class LocalAiProcessManager : MonoBehaviour
    {
        private static LocalAiProcessManager instance;

        private readonly LocalAiHealthClient healthClient = new();
        private LocalAiConfiguration configuration;
        private Process ownedGateway;
        private Process ownedOllama;
        private string gatewayToken;
        private bool gatewayOwned;
        private bool ollamaOwned;
        private bool startupInProgress;
        private bool gatewayCanHandleVoice;
        private readonly object processLogLock = new();

        public LocalAiRuntimeState State { get; private set; } =
            LocalAiRuntimeState.Unavailable;
        public string GatewayBaseUrl { get; private set; } =
            "http://127.0.0.1:8765";
        public bool IsReady => State == LocalAiRuntimeState.Ready
            || (State == LocalAiRuntimeState.Degraded && gatewayCanHandleVoice);
        public event Action<LocalAiRuntimeState> StateChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<LocalAiProcessManager>() != null)
            {
                return;
            }

            GameObject root = new("Local AI Process Manager");
            DontDestroyOnLoad(root);
            root.AddComponent<LocalAiProcessManager>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            configuration = LocalAiConfiguration.Load();
            GatewayBaseUrl = BuildGatewayUrl(configuration.Data.gateway.port);
        }

        private void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL keeps the existing browser/Fastify voice provider and has
            // no permission to launch a local Windows process.
            SetState(LocalAiRuntimeState.Degraded);
#else
            StartCoroutine(InitializeAtStartup());
#endif
        }

        private IEnumerator InitializeAtStartup()
        {
            string error = string.Empty;
            yield return EnsureReady((_, message) => error = message);
            if (!IsReady && !string.IsNullOrWhiteSpace(error))
            {
                UnityEngine.Debug.LogWarning(
                    "Local AI startup did not become ready: " + error);
            }
        }

        public IEnumerator EnsureReady(Action<bool, string> completed)
        {
            if (IsReady)
            {
                completed?.Invoke(true, string.Empty);
                yield break;
            }

            if (startupInProgress)
            {
                while (startupInProgress)
                {
                    yield return null;
                }

                completed?.Invoke(IsReady, IsReady ? string.Empty : "LOCAL_AI_NOT_READY");
                yield break;
            }

            startupInProgress = true;
            SetState(LocalAiRuntimeState.Starting);
            string error = string.Empty;
            yield return StartRuntime(message => error = message);
            startupInProgress = false;
            completed?.Invoke(IsReady, error);
        }

        private IEnumerator StartRuntime(Action<string> failed)
        {
            if (configuration == null)
            {
                configuration = LocalAiConfiguration.Load();
            }

            gatewayCanHandleVoice = false;
            LocalAiGatewaySettings gateway = configuration.Data.gateway;
            LocalAiOllamaSettings ollama = configuration.Data.ollama;
            bool ollamaReady = false;
            yield return healthClient.IsOllamaReady(
                BuildOllamaUrl(ollama.host, ollama.port),
                ollama.model,
                2,
                ready => ollamaReady = ready);

            if (!ollamaReady)
            {
                if (!File.Exists(configuration.OllamaExecutable))
                {
                    UnityEngine.Debug.LogWarning(
                        "Ollama runtime missing; starting voice gateway in fallback mode.");
                }
                else if (!StartOllama(ollama))
                {
                    UnityEngine.Debug.LogWarning(
                        "Ollama start failed; starting voice gateway in fallback mode.");
                }
                else
                {
                    float waitSeconds = Mathf.Min(
                        Mathf.Max(1f, gateway.startupTimeoutSeconds),
                        8f);
                    float deadline = Time.realtimeSinceStartup + waitSeconds;
                    while (Time.realtimeSinceStartup < deadline && !ollamaReady)
                    {
                        yield return new WaitForSecondsRealtime(0.5f);
                        yield return healthClient.IsOllamaReady(
                            BuildOllamaUrl(ollama.host, ollama.port),
                            ollama.model,
                            2,
                            ready => ollamaReady = ready);
                    }

                    if (!ollamaReady)
                    {
                        UnityEngine.Debug.LogWarning(
                            "Ollama model not ready; voice commands will use deterministic fallback first.");
                    }
                }
            }

            SetState(LocalAiRuntimeState.LoadingModels);
            bool gatewayReady = false;
            for (int index = 0; index < Mathf.Max(1, gateway.portScanCount); index++)
            {
                int port = gateway.port + index;
                GatewayBaseUrl = BuildGatewayUrl(port);
                LocalAiHealthResponse health = null;
                yield return healthClient.GetGatewayHealth(
                    GatewayBaseUrl,
                    2,
                    response =>
                    {
                        health = response;
                        gatewayReady = response != null && response.CanHandleVoice;
                    },
                    _ => { });
                if (gatewayReady)
                {
                    gatewayOwned = false;
                    gatewayCanHandleVoice = true;
                    SetState(health != null && health.IsReady
                        ? LocalAiRuntimeState.Ready
                        : LocalAiRuntimeState.Degraded);
                    yield break;
                }

                if (!StartGateway(port, ollama.port))
                {
                    continue;
                }

                float deadline = Time.realtimeSinceStartup + gateway.startupTimeoutSeconds;
                while (Time.realtimeSinceStartup < deadline && !gatewayReady)
                {
                    yield return new WaitForSecondsRealtime(0.5f);
                    yield return healthClient.GetGatewayHealth(
                        GatewayBaseUrl,
                        2,
                        response =>
                        {
                            health = response;
                            gatewayReady = response != null && response.CanHandleVoice;
                        },
                        _ => { });
                }

                if (gatewayReady)
                {
                    gatewayCanHandleVoice = true;
                    SetState(health != null && health.IsReady
                        ? LocalAiRuntimeState.Ready
                        : LocalAiRuntimeState.Degraded);
                    yield break;
                }

                StopOwnedGateway();
            }

            SetState(LocalAiRuntimeState.Error);
            gatewayCanHandleVoice = false;
            failed?.Invoke("GATEWAY_NOT_READY");
        }

        private bool StartOllama(LocalAiOllamaSettings ollama)
        {
            try
            {
                var info = new ProcessStartInfo
                {
                    FileName = configuration.OllamaExecutable,
                    Arguments = "serve",
                    WorkingDirectory = configuration.OllamaDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                info.EnvironmentVariables["OLLAMA_HOST"] =
                    ollama.host + ":" + ollama.port;
                info.EnvironmentVariables["OLLAMA_MODELS"] = Path.Combine(
                    configuration.ProjectRoot,
                    "LocalAI",
                    "models",
                    "ollama");
                ownedOllama = Process.Start(info);
                BeginProcessLogging(
                    ownedOllama,
                    Path.Combine(configuration.LogsDirectory, "ollama.log"));
                ollamaOwned = ownedOllama != null;
                return ollamaOwned;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError("Ollama start failed: " + exception.Message);
                return false;
            }
        }

        private bool StartGateway(int port, int ollamaPort)
        {
            if (!File.Exists(configuration.GatewayExecutable))
            {
                return false;
            }

            gatewayToken = Guid.NewGuid().ToString("N");
            string arguments = string.Join(
                " ",
                "--host " + Quote(configuration.Data.gateway.host),
                "--port " + port,
                "--ollama-host " + Quote(configuration.Data.ollama.host),
                "--ollama-port " + ollamaPort,
                "--ollama-model " + Quote(configuration.Data.ollama.model),
                "--stt-model-path " + Quote(configuration.ResolvePath(
                    configuration.Data.stt.modelPath)),
                "--language " + Quote(configuration.Data.stt.language),
                "--shutdown-token " + gatewayToken,
                "--owner-pid " + Process.GetCurrentProcess().Id);
            try
            {
                var info = new ProcessStartInfo
                {
                    FileName = configuration.GatewayExecutable,
                    Arguments = arguments,
                    WorkingDirectory = configuration.GatewayDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                ownedGateway = Process.Start(info);
                BeginProcessLogging(
                    ownedGateway,
                    Path.Combine(configuration.LogsDirectory, "gateway.log"));
                gatewayOwned = ownedGateway != null;
                return gatewayOwned;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError("Gateway start failed: " + exception.Message);
                return false;
            }
        }

        private void OnApplicationQuit()
        {
            StopOwnedGateway();
            StopOwnedOllama();
        }

        private void StopOwnedGateway()
        {
            if (!gatewayOwned || ownedGateway == null)
            {
                return;
            }

            try
            {
                using var request = UnityWebRequest.PostWwwForm(
                    GatewayBaseUrl + "/shutdown",
                    string.Empty);
                request.SetRequestHeader("X-Local-AI-Token", gatewayToken ?? string.Empty);
                request.SendWebRequest();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning("Gateway shutdown request failed: " + exception.Message);
            }

            try
            {
                if (!ownedGateway.HasExited)
                {
                    ownedGateway.Kill();
                }
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            ownedGateway.Dispose();
            ownedGateway = null;
            gatewayOwned = false;
        }

        private void StopOwnedOllama()
        {
            if (!ollamaOwned || ownedOllama == null)
            {
                return;
            }

            try
            {
                if (!ownedOllama.HasExited)
                {
                    ownedOllama.Kill();
                }
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            ownedOllama.Dispose();
            ownedOllama = null;
            ollamaOwned = false;
        }

        private void SetState(LocalAiRuntimeState state)
        {
            bool wasUsable = IsReady;
            State = state;

            // Once, the first time the gateway can actually take a request.
            //
            // Asked of `IsReady` rather than of the enum because Degraded counts
            // when the gateway itself came up — the model is the part that did
            // not, and voice still works through the deterministic fallback. The
            // player is being told the microphone is worth pressing, which is
            // true in both cases.
            //
            // This runs in Bootstrap, where there is no `GameSoundService` in the
            // scene; the request falls through to the persistent source.
            if (!wasUsable && IsReady)
            {
                GameSoundService.Request(GameSoundId.VoiceModelReady);
            }

            StateChanged?.Invoke(state);
        }

        private string BuildGatewayUrl(int port) =>
            "http://" + configuration.Data.gateway.host + ":" + port;

        private static string BuildOllamaUrl(string host, int port) =>
            "http://" + host + ":" + port;

        private static string Quote(string value) =>
            "\"" + (value ?? string.Empty).Replace("\"", "") + "\"";

        private void BeginProcessLogging(Process process, string path)
        {
            if (process == null)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                process.OutputDataReceived += (_, args) => AppendProcessLog(path, args.Data);
                process.ErrorDataReceived += (_, args) => AppendProcessLog(path, args.Data);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "Local AI process log capture failed: " + exception.Message);
            }
        }

        private void AppendProcessLog(string path, string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }

            try
            {
                lock (processLogLock)
                {
                    File.AppendAllText(
                        path,
                        DateTime.UtcNow.ToString("O") + " " + line
                            + Environment.NewLine);
                }
            }
            catch (IOException)
            {
                // Logging must not affect voice availability or shutdown.
            }
        }
    }
}
