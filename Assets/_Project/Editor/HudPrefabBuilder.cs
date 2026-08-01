using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using PawsAndLoot.UI;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Creates the reusable HUD prefab set without touching Game.unity.
    /// </summary>
    public static class HudPrefabBuilder
    {
        private const string PrefabRoot = "Assets/_Project/UI/Prefabs";
        private const string HudCanvasSourcePath =
            PrefabRoot + "/HudCanvas.prefab";
        private const string HudCanvasResourcesPath =
            "Assets/Resources/HudCanvas.prefab";
        private const string RuntimeFontAssetPath =
            "Assets/Resources/PawsAndLootDefaultFont.asset";
        private const string TmpSettingsAssetPath =
            "Assets/Resources/TMP Settings.asset";

        [MenuItem("Paws & Loot/UI/Sync HUD Canvas To Resources")]
        public static void SyncHudCanvasToResources()
        {
            EnsureFolder("Assets/Resources");
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(
                HudCanvasSourcePath);
            if (source == null)
            {
                throw new System.InvalidOperationException(
                    $"Configured HUD prefab is missing: {HudCanvasSourcePath}");
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(
                    HudCanvasResourcesPath) != null
                && !AssetDatabase.DeleteAsset(HudCanvasResourcesPath))
            {
                throw new System.InvalidOperationException(
                    $"Could not replace generated HUD prefab: {HudCanvasResourcesPath}");
            }

            if (!AssetDatabase.CopyAsset(
                    HudCanvasSourcePath,
                    HudCanvasResourcesPath))
            {
                throw new System.InvalidOperationException(
                    $"Could not copy configured HUD prefab to {HudCanvasResourcesPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"Synced configured HUD prefab to {HudCanvasResourcesPath}.");
        }

        [MenuItem("Paws & Loot/UI/Create Role-Aware HUD Prefabs")]
        public static void CreatePrefabs()
        {
            EnsureFolder("Assets/_Project/UI");
            EnsureFolder(PrefabRoot);
            EnsureFolder("Assets/Resources");
            EnsureRuntimeFontAssets();

            SavePrefab(
                $"{PrefabRoot}/HudCanvas.prefab",
                HudRuntimeInstaller.BuildRuntimeCanvas());
            SavePrefab(
                "Assets/Resources/HudCanvas.prefab",
                HudRuntimeInstaller.BuildRuntimeCanvas());
            SavePrefab(
                $"{PrefabRoot}/QuickSlotView.prefab",
                CreateQuickSlotPrefab());
            SavePrefab(
                $"{PrefabRoot}/AnimalCommandShortcutView.prefab",
                CreateAnimalCommandPrefab());
            SavePrefab(
                $"{PrefabRoot}/MicrophoneStatusView.prefab",
                CreateMicrophonePrefab());
            SavePrefab(
                $"{PrefabRoot}/ContextInteractionPromptView.prefab",
                CreateContextPrefab());
            SavePrefab(
                $"{PrefabRoot}/RoleStatusPanelView.prefab",
                CreateRoleStatusPrefab());
            SavePrefab(
                $"{PrefabRoot}/MinimapAlertView.prefab",
                CreateMinimapAlertPrefab());
            SavePrefab(
                $"{PrefabRoot}/InventorySlotView.prefab",
                CreateInventorySlotPrefab());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Created reusable role-aware HUD prefabs under {PrefabRoot}.");
        }

        private static GameObject CreateQuickSlotPrefab()
        {
            GameObject root = CreateRoot("QuickSlotView");
            TMP_Text key = CreateText(root.transform, "Key", "1");
            TMP_Text quantity = CreateText(root.transform, "Quantity", string.Empty);
            TMP_Text glyph = CreateText(root.transform, "Item Glyph", string.Empty);
            Image icon = CreateImage(root.transform, "Item Icon");
            Image selected = CreateImage(root.transform, "Selected Frame");
            Image disabled = CreateImage(root.transform, "Disabled");
            Image cooldown = CreateImage(root.transform, "Cooldown");
            cooldown.type = Image.Type.Filled;
            cooldown.fillMethod = Image.FillMethod.Radial360;
            root.AddComponent<QuickSlotView>().Configure(key, quantity, icon, selected, disabled, cooldown, glyph);
            return root;
        }

        private static GameObject CreateAnimalCommandPrefab()
        {
            GameObject root = CreateRoot("AnimalCommandShortcutView");
            TMP_Text modifier = CreateText(root.transform, "Modifier", "SHIFT +");
            TMP_Text key = CreateText(root.transform, "Key", "1");
            TMP_Text command = CreateText(root.transform, "Command", "TRACK");
            Image disabled = CreateImage(root.transform, "Disabled");
            root.AddComponent<AnimalCommandShortcutView>().Configure(modifier, key, command, disabled);
            return root;
        }

        private static GameObject CreateMicrophonePrefab()
        {
            GameObject root = CreateRoot("MicrophoneStatusView");
            TMP_Text state = CreateText(root.transform, "State", "READY");
            TMP_Text key = CreateText(root.transform, "Key", "V");
            TMP_Text cooldown = CreateText(root.transform, "Cooldown", string.Empty);
            Image radial = CreateImage(root.transform, "Recording Radial");
            radial.type = Image.Type.Filled;
            radial.fillMethod = Image.FillMethod.Radial360;
            Image disabled = CreateImage(root.transform, "Disabled");
            root.AddComponent<MicrophoneStatusView>().Configure(state, key, cooldown, radial, disabled);
            return root;
        }

        private static GameObject CreateContextPrefab()
        {
            GameObject root = CreateRoot("ContextInteractionPromptView");
            TMP_Text key = CreateText(root.transform, "Key", "[E]");
            TMP_Text action = CreateText(root.transform, "Action", "Interact");
            Image progress = CreateImage(root.transform, "Hold Progress");
            progress.type = Image.Type.Filled;
            root.AddComponent<ContextInteractionPromptView>().Configure(key, action, progress);
            return root;
        }

        private static GameObject CreateRoleStatusPrefab()
        {
            GameObject root = CreateRoot("RoleStatusPanelView");
            TMP_Text role = CreateText(root.transform, "Role", "POLICE");
            TMP_Text objective = CreateText(root.transform, "Objective", "Objective");
            TMP_Text status = CreateText(root.transform, "Status", "ACTIVE");
            Image tint = CreateImage(root.transform, "Role Tint");
            root.AddComponent<RoleStatusPanelView>().Configure(role, objective, status, tint);
            return root;
        }

        private static GameObject CreateMinimapAlertPrefab()
        {
            GameObject root = CreateRoot("MinimapAlertView");
            Image marker = CreateImage(root.transform, "Marker");
            root.AddComponent<MinimapAlertView>().Configure(marker);
            return root;
        }

        private static GameObject CreateInventorySlotPrefab()
        {
            GameObject root = CreateRoot("InventorySlotView");
            TMP_Text key = CreateText(root.transform, "Key", "1");
            TMP_Text quantity = CreateText(root.transform, "Quantity", string.Empty);
            TMP_Text glyph = CreateText(root.transform, "Item Glyph", string.Empty);
            Image icon = CreateImage(root.transform, "Item Icon");
            Image selected = CreateImage(root.transform, "Selected Frame");
            Image disabled = CreateImage(root.transform, "Disabled");
            root.AddComponent<InventorySlotView>().Configure(key, quantity, icon, selected, disabled, glyph);
            return root;
        }

        private static GameObject CreateRoot(string name)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.localScale = Vector3.one;
            return root;
        }

        private static TMP_Text CreateText(Transform parent, string name, string value)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            child.transform.SetParent(parent, false);
            TMP_Text text = child.GetComponent<TMP_Text>();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RuntimeFontAssetPath);
            if (font != null)
            {
                text.font = font;
            }
            text.text = value;
            text.fontSize = 16f;
            text.color = Color.white;
            return text;
        }

        private static Image CreateImage(Transform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(Image));
            child.transform.SetParent(parent, false);
            Image image = child.GetComponent<Image>();
            image.color = Color.white;
            return image;
        }

        private static void EnsureRuntimeFontAssets()
        {
            TMP_Settings settings =
                AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsAssetPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<TMP_Settings>();
                AssetDatabase.CreateAsset(settings, TmpSettingsAssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(
                    TmpSettingsAssetPath,
                    ImportAssetOptions.ForceUpdate);
            }

            TMP_Settings.LoadDefaultSettings();

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RuntimeFontAssetPath);
            if (font == null)
            {
                string packageFont = null;
                string packageCache = Path.GetFullPath("Library/PackageCache");
                foreach (string candidate in Directory.GetFiles(
                             packageCache,
                             "Inter-Regular SDF.asset",
                             SearchOption.AllDirectories))
                {
                    packageFont = candidate;
                    break;
                }

                if (string.IsNullOrWhiteSpace(packageFont))
                {
                    throw new System.InvalidOperationException(
                        "Could not locate the packaged TextMeshPro font asset.");
                }

                FileUtil.CopyFileOrDirectory(packageFont, RuntimeFontAssetPath);
                string packageMeta = packageFont + ".meta";
                if (File.Exists(packageMeta))
                {
                    FileUtil.CopyFileOrDirectory(
                        packageMeta,
                        RuntimeFontAssetPath + ".meta");
                }

                AssetDatabase.ImportAsset(
                    RuntimeFontAssetPath,
                    ImportAssetOptions.ForceUpdate);
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    RuntimeFontAssetPath);
                if (font == null)
                {
                    throw new System.InvalidOperationException(
                        "Could not import the packaged TextMeshPro font asset.");
                }
            }

            SerializedObject serializedSettings = new(settings);
            SerializedProperty defaultFont = serializedSettings.FindProperty(
                "m_defaultFontAsset");
            if (defaultFont != null && defaultFont.objectReferenceValue != font)
            {
                defaultFont.objectReferenceValue = font;
                serializedSettings.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(RuntimeFontAssetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(TmpSettingsAssetPath, ImportAssetOptions.ForceUpdate);
        }

        private static void SavePrefab(string path, GameObject root)
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            if (rootRect != null)
            {
                rootRect.localScale = Vector3.one;
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            RectTransform savedRoot = savedPrefab == null
                ? null
                : savedPrefab.GetComponent<RectTransform>();
            if (savedRoot != null && savedRoot.localScale != Vector3.one)
            {
                savedRoot.localScale = Vector3.one;
                EditorUtility.SetDirty(savedRoot);
                AssetDatabase.SaveAssets();
            }
            Object.DestroyImmediate(root);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string name = path.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
