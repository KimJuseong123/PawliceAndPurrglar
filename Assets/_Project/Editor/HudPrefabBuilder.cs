using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
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
        private const string RuntimeFontSourcePath =
            "Assets/ThirdParty/DNF_BitBit_v2/TTF/DNFBitBitv2.ttf";
        private const string TmpSettingsAssetPath =
            "Assets/Resources/TMP Settings.asset";
        private const string RuntimeFontPreloadCharacters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~"
            + "\uac00\uac01\uac04\uac10\uac70\uac83\uac8c\uace0\uace8\uad6c\uadf8\uae30\uae4c\ub098\ub2e4\ub2e8\ub300\ub354"
            + "\ub370\ub3c4\ub3d9\ub4e0\ub4dc\ub4e4\ub4ef\ub530\ub54c\ub5a8\ub610\ub77c\ub7f0\ub7ec\ub839\ub85c\ub97c"
            + "\ub9d0\ub9ac\uba85\ubaa9\ubaa8\ubb3c\ubb38\ubbf8\ubc14\ubc18\ubc1b\ubc29\ubc30\ubc84\ubc88\ubcf4\ubd99"
            + "\ube44\uc0ac\uc0c1\uc11c\uc13c\uc138\uc18c\uc190\uc1a1\uc218\uc21c\uc2a4\uc2dc\uc2e4\uc544\uc548\uc54c\uc5b4\uc5c6"
            + "\uc5d0\uc5ec\uc624\uc644\uc6b0\uc6b4\uc6d0\uc704\uc73c\uc74c\uc758\uc774\uc778\uc785\uc788\uc7a1\uc804\uc810\uc815"
            + "\uc81c\uc870\uc8fc\uc911\uc9c1\ucc28\ucc30\ucc98\uccb4\ucd08\ucd94\ucd9c\ucda9\ucfe8\ud0c0\ud0dc\ud14c\ud15c"
            + "\ud3ec\ud45c\ud55c\ud560\ud574\ud589\ud6c4\ud68c\ud69f"
            + "\ub3c4\ub451\uc744 3\ud68c \uccb4\ud3ec\ud558\uc138\uc694"
            + "1000\uace8\ub4dc \ubaa8\uc73c\uae30"
            + "\uace8\ub4dc 0 / 1000"
            + "\ubd99\uc7a1\ud78c \ud69f\uc218"
            + "\ub3d9\ubb3c \uba85\ub839 \uc804\uc1a1 \uc2e4\ud589 \uc2e4\ud328"
            + "\uc544\uc9c1 \uba85\ub839 \ucfe8\ud0c0\uc784"
            + "\ucd08 \ud6c4 \ub2e4\uc2dc \ub9d0\ud560 \uc218 \uc788\uc5b4\uc694"
            + "\uc74c\uc131 \uc785\ub825 \uc900\ube44 \uc911"
            + "\uc74c\uc131 \uba85\ub839 \ub179\uc74c \ucc98\ub9ac \uc644\ub8cc \uc2dc\ub3c4 \uc8fc\uc138\uc694"
            + "5\ucd08 \ub3d9\uc548 \ub4e3\uace0 \uc788\uc5b4\uc694"
            + "\uba85\ub839\uc744 \ud574\uc11d\ud558\uace0 \uc788\uc5b4\uc694"
            // Lobby wording. The atlas is dynamic, so a missing glyph would be
            // fetched at runtime rather than drawn as a box, but baking the
            // screen a player stares at first keeps that work off the first
            // frame.
            + "\uc5ed\ud560 \ubc14\uafb8\uae30 \uac8c\uc784 \uc2dc\uc791 \ub9c8\uc774\ud06c \ud655\uc778 \uc911 \ud638\uc2a4\ud2b8 \ucc38\uac00 \ub098\uac00\uae30 \uc8fc\uc18c \ud3ec\ud2b8"
            + "\ub0b4 \uc5ed\ud560 \ub300\uae30 \uacbd\ucc30 \ub3c4\ub451 \uac19\uc740 PC \uc138\uc158 \uc9c4\ud589 \uac00\ub4dd \ucc38 \uac00\ub2a5"
            + "\ud638\uc2a4\ud2b8\ub85c \uc2dc\uc791\ud558\uac70\ub098 \uc0c1\ub300\uc758 IP\ub85c \ucc38\uac00\ud558\uc138\uc694."
            + "\uc0c1\ub300 \ud50c\ub808\uc774\uc5b4\ub97c \uae30\ub2e4\ub9ac\uace0 \uc788\uc2b5\ub2c8\ub2e4."
            + "\ud638\uc2a4\ud2b8\uc5d0 \uc5f0\uacb0\ud558\ub294 \uc911\uc785\ub2c8\ub2e4."
            + "\uc0c1\ub300 \ud50c\ub808\uc774\uc5b4\uc640 \uc5f0\uacb0\ub418\uc5c8\uc2b5\ub2c8\ub2e4. \uac8c\uc784 \uc2dc\uc791\uc744 \ub204\ub974\uc138\uc694."
            + "\ud638\uc2a4\ud2b8\uac00 \uc2dc\uc791\ud558\uae30\ub97c \uae30\ub2e4\ub9bd\ub2c8\ub2e4."
            + "\uac19\uc740 \ub124\ud2b8\uc6cc\ud06c\uc5d0\uc11c \ubc29\uc744 \ucc3e\ub294 \uc911\uc785\ub2c8\ub2e4. \ud55c \uba85\uc774 \uba3c\uc800 \ub20c\ub7ec\uc57c \ud569\ub2c8\ub2e4."
            + "\uac19\uc740 \ub124\ud2b8\uc6cc\ud06c\uc5d0\uc11c \ubc29 \uac1c\ub97c \ucc3e\uc558\uc2b5\ub2c8\ub2e4. \ub20c\ub7ec\uc11c \ucc38\uac00\ud558\uc138\uc694."
            + "\uac19\uc740 \ub124\ud2b8\uc6cc\ud06c \ubc29 \ucc3e\uae30\ub97c \uc4f8 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4. \uc544\ub798\uc5d0 IP\ub97c \uc9c1\uc811 \uc785\ub825\ud558\uc138\uc694."
            + "\ub9c8\uc774\ud06c \uad8c\ud55c\uc744 \ud655\uc778\ud558\ub294 \uc911\uc785\ub2c8\ub2e4. \uac70\ubd80\ub418\uc5c8\uc2b5\ub2c8\ub2e4."
            + "\uc0ac\uc6a9 \uac00\ub2a5\ud55c \ub9c8\uc774\ud06c\ub97c \ucc3e\uc9c0 \ubabb\ud588\uc2b5\ub2c8\ub2e4."
            + "\ub9c8\uc774\ud06c\ub97c \uc5f4 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4. \ub2e4\ub978 \ud504\ub85c\uadf8\ub7a8\uc5d0\uc11c \uc0ac\uc6a9 \uc911\uc785\ub2c8\ub2e4."
            + "\ub9c8\uc774\ud06c \uc785\ub825\uc744 \ub4e3\uace0 \uc788\uc2b5\ub2c8\ub2e4. \ub9d0\ud574 \ubcf4\uc138\uc694."
            + "\ub9c8\uc774\ud06c \uc785\ub825\uc774 \uc815\uc0c1\uc801\uc73c\ub85c \uac10\uc9c0\ub418\uc5c8\uc2b5\ub2c8\ub2e4. \ucd5c\ub300"
            + "\uc785\ub825\uc774 \uac10\uc9c0\ub418\uc9c0 \uc54a\uc558\uc2b5\ub2c8\ub2e4. \ub9c8\uc774\ud06c \ubcfc\ub968\uc744 \ud655\uc778\ud558\uc138\uc694."
            + "\uc774\ubbf8 \uc138\uc158\uc774 \uc2e4\ud589 \uc911\uc785\ub2c8\ub2e4. \ud3ec\ud2b8\ub294 \uc774\uc0c1\uc758 \uc22b\uc790\uc5ec\uc57c \ud569\ub2c8\ub2e4."
            + "IP \ud615\uc2dd\uc774 \uc62c\ubc14\ub974\uc9c0 \uc54a\uc2b5\ub2c8\ub2e4. \uc608: 192.168.0.10"
            + "\ud638\uc2a4\ud2b8 \ub300\uae30 \uc911 \uc0c1\ub300\uc5d0\uac8c \ub0b4 IP\ub97c \uc54c\ub824\uc8fc\uc138\uc694."
            + "\ud638\uc2a4\ud2b8 \uc2dc\uc791\uc5d0 \uc2e4\ud328\ud588\uc2b5\ub2c8\ub2e4. \uc811\uc18d \ub85c \uc911..."
            + "\uc0c1\ub300\uac00 \uc811\uc18d\uc744 \uc885\ub8cc\ud588\uc2b5\ub2c8\ub2e4. \ud655\uc778 \uc911";

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
            TMP_Text modifier = CreateText(root.transform, "Modifier", "CTRL +");
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
            var progressObject = new GameObject(
                "Hold Progress",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RadialProgressGraphic));
            progressObject.transform.SetParent(root.transform, false);
            RadialProgressGraphic progress =
                progressObject.GetComponent<RadialProgressGraphic>();
            progress.RingThickness = 3.5f;
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
            TMP_Text itemName = CreateText(root.transform, "Item Name", string.Empty);
            TMP_Text price = CreateText(root.transform, "Price", string.Empty);
            TMP_Text glyph = CreateText(root.transform, "Item Glyph", string.Empty);
            Image icon = CreateImage(root.transform, "Item Icon");
            Image priceIcon = CreateImage(root.transform, "Currency Icon");
            Image selected = CreateImage(root.transform, "Selected Frame");
            Image disabled = CreateImage(root.transform, "Disabled");
            root.AddComponent<InventorySlotView>().Configure(
                key,
                quantity,
                icon,
                selected,
                disabled,
                glyph,
                itemName,
                price,
                priceIcon);
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
                text.fontSharedMaterial = font.material;
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

            Font sourceFont = LoadRuntimeSourceFont();
            TMP_FontAsset font = EnsureRuntimeFontAsset(sourceFont);

            ConfigureRuntimeFont(font);

            SerializedObject serializedSettings = new(settings);
            SerializedProperty defaultFont = serializedSettings.FindProperty(
                "m_defaultFontAsset");
            if (defaultFont != null && defaultFont.objectReferenceValue != font)
            {
                defaultFont.objectReferenceValue = font;
                serializedSettings.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
            }

            SerializedProperty clearDynamicData =
                serializedSettings.FindProperty("m_ClearDynamicDataOnBuild");
            if (clearDynamicData != null && clearDynamicData.boolValue)
            {
                clearDynamicData.boolValue = false;
                serializedSettings.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(RuntimeFontAssetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(TmpSettingsAssetPath, ImportAssetOptions.ForceUpdate);
        }

        private static Font LoadRuntimeSourceFont()
        {
            AssetDatabase.ImportAsset(
                RuntimeFontSourcePath,
                ImportAssetOptions.ForceUpdate);

            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(
                RuntimeFontSourcePath);
            if (sourceFont == null)
            {
                throw new System.InvalidOperationException(
                    $"Configured HUD font is missing or could not be imported: {RuntimeFontSourcePath}");
            }

            return sourceFont;
        }

        private static TMP_FontAsset EnsureRuntimeFontAsset(Font sourceFont)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                RuntimeFontAssetPath);
            if (IsRuntimeFontAssetCurrent(font, sourceFont))
            {
                return font;
            }

            if (font != null && !AssetDatabase.DeleteAsset(RuntimeFontAssetPath))
            {
                throw new System.InvalidOperationException(
                    $"Could not replace generated HUD font asset: {RuntimeFontAssetPath}");
            }

            font = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,
                9,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic,
                true);
            if (font == null)
            {
                throw new System.InvalidOperationException(
                    $"Could not create HUD font asset from {RuntimeFontSourcePath}");
            }

            font.name = "PawsAndLootDefaultFont";
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            Texture2D atlasTexture = font.atlasTexture;
            Material fontMaterial = font.material;
            AssetDatabase.CreateAsset(font, RuntimeFontAssetPath);
            if (atlasTexture != null)
            {
                AssetDatabase.AddObjectToAsset(atlasTexture, font);
                EditorUtility.SetDirty(atlasTexture);
            }

            if (fontMaterial != null)
            {
                AssetDatabase.AddObjectToAsset(fontMaterial, font);
                EditorUtility.SetDirty(fontMaterial);
            }

            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                RuntimeFontAssetPath,
                ImportAssetOptions.ForceUpdate);

            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                RuntimeFontAssetPath);
            if (font == null)
            {
                throw new System.InvalidOperationException(
                    $"Could not import generated HUD font asset: {RuntimeFontAssetPath}");
            }

            return font;
        }

        private static bool IsRuntimeFontAssetCurrent(
            TMP_FontAsset font,
            Font sourceFont)
        {
            return font != null
                   && font.sourceFontFile == sourceFont
                   && font.atlasTextures != null
                   && font.atlasTextures.Length > 0
                   && font.atlasTextures[0] != null
                   && font.material != null;
        }

        private static void RepairFontMaterial(TMP_FontAsset font)
        {
            if (font == null || font.material == null)
            {
                return;
            }

            Shader shader =
                Shader.Find("TextMeshPro/Distance Field")
                ?? Shader.Find("TextMeshPro/Mobile/Distance Field")
                ?? Shader.Find("UI/Default")
                ?? Resources.GetBuiltinResource<Shader>("UI/Default.shader");
            if (shader == null)
            {
                throw new System.InvalidOperationException(
                    "Could not locate a build-safe UI font shader.");
            }

            if (font.material.shader != shader)
            {
                font.material.shader = shader;
                EditorUtility.SetDirty(font.material);
            }

            if (font.atlasTexture != null)
            {
                font.material.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);
                EditorUtility.SetDirty(font.material);
            }

            EditorUtility.SetDirty(font);
        }

        private static void ConfigureRuntimeFont(TMP_FontAsset font)
        {
            if (font == null)
            {
                return;
            }

            SerializedObject serializedFont = new(font);
            SerializedProperty clearDynamicData =
                serializedFont.FindProperty("m_ClearDynamicDataOnBuild");
            if (clearDynamicData != null && clearDynamicData.boolValue)
            {
                clearDynamicData.boolValue = false;
                serializedFont.ApplyModifiedPropertiesWithoutUndo();
            }

            if (!font.TryAddCharacters(
                    RuntimeFontPreloadCharacters,
                    out string missingCharacters)
                && !string.IsNullOrEmpty(missingCharacters))
            {
                Debug.LogWarning(
                    "HUD font could not preload some glyphs: "
                    + missingCharacters);
            }

            RepairFontMaterial(font);
            EditorUtility.SetDirty(font);
        }

        private static void SavePrefab(string path, GameObject root)
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            if (rootRect != null)
            {
                rootRect.localScale = Vector3.one;
            }

            ProjectFontSweep.Apply(root);
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
