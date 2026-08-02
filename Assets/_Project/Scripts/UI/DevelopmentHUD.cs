using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    /// <summary>
    /// Keeps technical-validation presentation available to developers while
    /// hiding it from normal gameplay. The overlay is opt-in through the
    /// -showDebugOverlay command-line flag or SetDebugOverlayVisible.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DevelopmentHUD : MonoBehaviour
    {
        private static readonly string[] DebugObjectNameTokens =
        {
            "Scene Performance Probe",
            "Network Match Probe",
            "Validation Overlay",
            "Technical Validation",
            "Debug HUD"
        };

        private static readonly string[] DebugTextTokens =
        {
            "POLICESPAWN",
            "THIEFSPAWN",
            "SUPERMARKET",
            "POLICE STATUS",
            "THIEF LOOT",
            "COMMAND  [1/2]",
            "VALIDATION",
            "DEBUG"
        };

        private readonly Dictionary<GameObject, bool> hiddenObjects = new();
        private bool showDebugOverlay;

        public static DevelopmentHUD Instance { get; private set; }
        public bool ShowDebugOverlay => showDebugOverlay;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForScene()
        {
            if (Instance != null)
            {
                Instance.ApplyVisibility();
                return;
            }

            var root = new GameObject("Development HUD Settings");
            DontDestroyOnLoad(root);
            root.AddComponent<DevelopmentHUD>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            showDebugOverlay = HasCommandLineFlag("-showDebugOverlay");
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ApplyVisibility();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        public static void SetDebugOverlayVisible(bool visible)
        {
            if (Instance == null)
            {
                return;
            }

            Instance.showDebugOverlay = visible;
            Instance.ApplyVisibility();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            if (showDebugOverlay)
            {
                RestoreHiddenObjects();
                return;
            }

            foreach (Text text in Resources.FindObjectsOfTypeAll<Text>())
            {
                if (!IsSceneObject(text.gameObject)
                    || !ContainsDebugText(text.text))
                {
                    continue;
                }

                Hide(GetDebugTextTarget(text.transform));
            }

            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                HideDebugObjects(root.transform);
            }
        }

        private void HideDebugObjects(Transform current)
        {
            if (ContainsDebugObjectName(current.name))
            {
                Hide(current.gameObject);
            }

            foreach (Transform child in current)
            {
                HideDebugObjects(child);
            }
        }

        private static GameObject GetDebugTextTarget(Transform textTransform)
        {
            GameObject target = textTransform.gameObject;
            Transform current = textTransform.parent;
            while (current != null && current.GetComponent<Canvas>() == null)
            {
                string name = current.name;
                if (name.IndexOf("HUD", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Command", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Status", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Loot", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Label", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    target = current.gameObject;
                }

                current = current.parent;
            }

            return target;
        }

        private void Hide(GameObject target)
        {
            if (!hiddenObjects.ContainsKey(target))
            {
                hiddenObjects.Add(target, target.activeSelf);
            }

            target.SetActive(false);
        }

        private void RestoreHiddenObjects()
        {
            foreach (KeyValuePair<GameObject, bool> entry in hiddenObjects)
            {
                if (entry.Key != null)
                {
                    entry.Key.SetActive(entry.Value);
                }
            }

            hiddenObjects.Clear();
        }

        private static bool ContainsDebugObjectName(string value)
        {
            foreach (string token in DebugObjectNameTokens)
            {
                if (value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsDebugText(string value)
        {
            foreach (string token in DebugTextTokens)
            {
                if (value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSceneObject(GameObject target)
        {
            return target.scene.IsValid() && !target.hideFlags.HasFlag(HideFlags.HideAndDontSave);
        }

        private static bool HasCommandLineFlag(string flag)
        {
            foreach (string argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
