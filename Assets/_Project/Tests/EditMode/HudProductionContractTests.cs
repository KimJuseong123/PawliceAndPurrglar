using NUnit.Framework;
using System.IO;
using PawsAndLoot.Input;
using PawsAndLoot.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class HudProductionContractTests
    {
        private const string HudPrefabPath = "Assets/Resources/HudCanvas.prefab";
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string BootstrapScenePath =
            "Assets/_Project/Scenes/Bootstrap.unity";
        private const string RuntimeFontPath =
            "Assets/Resources/PawsAndLootDefaultFont.asset";
        private const string RuntimeFontSourcePath =
            "Assets/ThirdParty/DNF_BitBit_v2/TTF/DNFBitBitv2.ttf";
        private const string ThrowTrajectoryMaterialPath =
            "Assets/_Project/Resources/ThrowTrajectoryMaterial.mat";
        private const string ThrowLandingMaterialPath =
            "Assets/_Project/Resources/ThrowLandingMarkerMaterial.mat";
        private const string CurrencyCoinPath =
            "Assets/_Project/Resources/UI/CurrencyCoin.png";
        private static readonly string[] ItemIconPaths =
        {
            "Assets/_Project/Resources/UI/ItemIcons/rock.png",
            "Assets/_Project/Resources/UI/ItemIcons/banana.png",
            "Assets/_Project/Resources/UI/ItemIcons/catnip pouch.png",
            "Assets/_Project/Resources/UI/ItemIcons/police lantern alarm.png",
            "Assets/_Project/Resources/UI/ItemIcons/bone.png",
            "Assets/_Project/Resources/UI/ItemIcons/fish can.png",
            "Assets/_Project/Resources/UI/ItemIcons/yellow chicken.png",
            "Assets/_Project/Resources/UI/ItemIcons/can.png",
            "Assets/_Project/Resources/UI/ItemIcons/gold medal.png",
            "Assets/_Project/Resources/UI/ItemIcons/golden watch.png",
            "Assets/_Project/Resources/UI/ItemIcons/blue gemstone.png"
        };

        [Test]
        public void RoleAwareHudPrefabIsVisibleAndComplete()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            Assert.That(prefab, Is.Not.Null, HudPrefabPath);

            RectTransform root = prefab.GetComponent<RectTransform>();
            Assert.That(root, Is.Not.Null);
            Assert.That(root.localScale, Is.EqualTo(Vector3.one));

            Canvas canvas = prefab.GetComponent<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(canvas.sortingOrder, Is.EqualTo(100));

            CanvasScaler scaler = prefab.GetComponent<CanvasScaler>();
            Assert.That(scaler, Is.Not.Null);
            Assert.That(
                scaler.uiScaleMode,
                Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
            Assert.That(
                scaler.screenMatchMode,
                Is.EqualTo(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight));

            Assert.That(prefab.GetComponent<RoleAwareHudController>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<CanvasGroup>(), Is.Not.Null);
            Assert.That(prefab.transform.Find("Voice Command Feed"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Voice Command Feed/Interpretation Paw"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Quick Slots"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Bag Button"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Voice Button"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Voice Button/Label"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Match Timer"), Is.Not.Null);
            Assert.That(prefab.transform.Find("TopRightMinimap"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Objective Text"), Is.Null);
            Assert.That(prefab.transform.Find("Police Catches"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Catch Progress"), Is.Null);
            Assert.That(prefab.transform.Find("Inventory/Grid/Inventory Slot 25"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Inventory/Grid/Inventory Slot 5/Price"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Inventory/Grid/Inventory Slot 5/Currency Icon"), Is.Not.Null);
            // The cat's bag is two by two beside the thief's own bag, not a
            // window that redraws the thief's twenty-five cells next to the
            // cat's four. A second copy of a screen the player just looked at is
            // a second place for the same bug.
            Assert.That(prefab.transform.Find("Cat Exchange/Player Bag"), Is.Null);
            Assert.That(prefab.transform.Find("Cat Exchange/Grid/Cat Bag Slot 4"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Context Interaction/Hold Progress"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Sensor Radar"), Is.Not.Null);

            // "ANIMAL COMMANDS" was here. It listed `CTRL + 1..4`, and both the
            // panel and the keys went on 2026-08-10 — the animals are told what
            // to do out loud. Asserted absent rather than dropped, so a rebuilt
            // prefab that still carries the old panel fails instead of quietly
            // covering the table that replaced it in the same corner.
            Assert.That(prefab.transform.Find("ANIMAL COMMANDS"), Is.Null);

            Assert.That(
                prefab.GetComponentsInChildren<RoleAwareHudController>(true),
                Has.Length.EqualTo(1));
            Assert.That(
                prefab.GetComponentsInChildren<PoliceCatchProgressView>(true),
                Has.Length.EqualTo(1));
            Assert.That(
                prefab.GetComponentsInChildren<SensorRadarPresenter>(true),
                Has.Length.EqualTo(1));
            Assert.That(
                prefab.GetComponentsInChildren<SensorArcGraphic>(true),
                Has.Length.EqualTo(7));
            Assert.That(
                prefab.GetComponentsInChildren<RadialProgressGraphic>(true),
                Has.Length.EqualTo(1));

            AssertRect(
                prefab.transform.Find("TopRightMinimap") as RectTransform,
                Vector2.one,
                Vector2.one,
                Vector2.one,
                new Vector2(-24f, -24f),
                new Vector2(250f, 250f));
            Image minimapBackground =
                prefab.transform.Find("TopRightMinimap").GetComponent<Image>();
            Assert.That(minimapBackground, Is.Not.Null);
            Assert.That(minimapBackground.color.a, Is.Zero);
            Assert.That(minimapBackground.raycastTarget, Is.False);
            AssertRect(
                prefab.transform.Find("Quick Slots") as RectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 24f),
                new Vector2(286f, 70f));
            AssertRect(
                prefab.transform.Find("Voice Command Feed") as RectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 104f),
                new Vector2(420f, 68f));
            AssertRect(
                prefab.transform.Find("Police Catches") as RectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -16f),
                new Vector2(330f, 108f));
            Assert.That(
                prefab.transform.Find("Police Catches/Catch Slot 1"),
                Is.Not.Null);
            Assert.That(
                prefab.transform.Find("Police Catches/Catch Slot 2"),
                Is.Not.Null);
            Assert.That(
                prefab.transform.Find("Police Catches/Catch Slot 3"),
                Is.Not.Null);
            AssertRect(
                prefab.transform.Find("Match Timer") as RectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -128f),
                new Vector2(170f, 32f));
            GridLayoutGroup inventoryGrid =
                prefab.transform.Find("Inventory/Grid").GetComponent<GridLayoutGroup>();
            Assert.That(inventoryGrid, Is.Not.Null);
            Assert.That(inventoryGrid.cellSize, Is.EqualTo(new Vector2(70f, 70f)));
            Assert.That(
                inventoryGrid.constraint,
                Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
            Assert.That(inventoryGrid.constraintCount, Is.EqualTo(5));
            AssertRect(
                prefab.transform.Find("Inventory") as RectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -260f),
                new Vector2(470f, 780f));
            // Immediately right of the bag, which ends at x = 494. Overlapping
            // them would hide the half the player is moving things out of.
            var catBag = prefab.transform.Find("Cat Exchange") as RectTransform;
            AssertRect(
                catBag,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(510f, -260f),
                new Vector2(240f, 300f));
            Assert.That(
                catBag.anchoredPosition.x,
                Is.GreaterThanOrEqualTo(24f + 470f),
                "The cat's bag overlaps the thief's.");
            Image catExchangeBackground =
                prefab.transform.Find("Cat Exchange").GetComponent<Image>();
            Assert.That(catExchangeBackground, Is.Not.Null);
            Assert.That(catExchangeBackground.color.a, Is.GreaterThan(0.4f));
            Assert.That(catExchangeBackground.raycastTarget, Is.True);
        }

        [Test]
        public void HudTextAndThrowPreviewUseBuildSafeMaterials()
        {
            TMP_FontAsset font =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RuntimeFontPath);
            Assert.That(font, Is.Not.Null, RuntimeFontPath);
            Font sourceFont =
                AssetDatabase.LoadAssetAtPath<Font>(RuntimeFontSourcePath);
            Assert.That(sourceFont, Is.Not.Null, RuntimeFontSourcePath);
            Assert.That(font.sourceFontFile, Is.EqualTo(sourceFont));
            Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Dynamic));
            Assert.That(font.characterTable.Count, Is.GreaterThan(0));
            SerializedObject serializedFont = new(font);
            SerializedProperty clearDynamicData =
                serializedFont.FindProperty("m_ClearDynamicDataOnBuild");
            Assert.That(
                clearDynamicData == null || clearDynamicData.boolValue,
                Is.False,
                "HUD font must keep its preloaded atlas data in player builds.");
            AssertBuildSafeMaterial(font.material, RuntimeFontPath);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<Sprite>(CurrencyCoinPath),
                Is.Not.Null,
                "The currency icon must import as a Sprite so runtime HUD binding can load it.");
            foreach (string path in ItemIconPaths)
            {
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<Sprite>(path),
                    Is.Not.Null,
                    $"The HUD item icon must import as a Sprite: {path}");
            }

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            Assert.That(prefab, Is.Not.Null, HudPrefabPath);
            foreach (TMP_Text text in prefab.GetComponentsInChildren<TMP_Text>(true))
            {
                Assert.That(text.font, Is.Not.Null, text.name);
                Assert.That(text.font, Is.EqualTo(font), text.name);
                AssertBuildSafeMaterial(text.fontSharedMaterial, text.name);
            }

            AssertBuildSafeMaterial(
                AssetDatabase.LoadAssetAtPath<Material>(
                    ThrowTrajectoryMaterialPath),
                ThrowTrajectoryMaterialPath);
            AssertBuildSafeMaterial(
                AssetDatabase.LoadAssetAtPath<Material>(
                    ThrowLandingMaterialPath),
                ThrowLandingMaterialPath);
        }

        [Test]
        public void ProductionGameSceneKeepsAuthoredReferencesAndVisibleCanvas()
        {
            Assert.That(File.Exists(GameScenePath), Is.True, GameScenePath);
            string scene = File.ReadAllText(GameScenePath);
            Assert.That(scene, Does.Not.Contain("Generated/Validation"));
            // Nothing but a canvas may be scaled to nothing.
            //
            // This used to forbid a zero scale anywhere in the file, to catch a
            // HUD that had been scaled away. It cannot stay that way: Unity
            // writes a screen-space overlay canvas with a zero scale whenever
            // the canvas has never rendered, and a scene built by an editor
            // script and saved has never rendered. Batch mode or windowed,
            // graphics or not — the file always says zero, and it is always
            // recomputed the moment the game runs.
            //
            // So the check asks the scene instead of the text. A zero scale on
            // a canvas root is the serialiser talking; a zero scale on anything
            // else is somebody's UI that will not be there.
            AssertOnlyCanvasesAreUnscaled(GameScenePath);
            Assert.That(scene, Does.Contain("9a7b02d3845aed24d8e4dde4734911bb"));
            Assert.That(scene, Does.Contain("6961fc7c68093c5479876c83455dcc7e"));
            Assert.That(scene, Does.Contain("7df0df2c17208144480fcd43b5ce3548"));

            Assert.That(
                File.Exists(BootstrapScenePath),
                Is.True,
                BootstrapScenePath);
            string bootstrap = File.ReadAllText(BootstrapScenePath);
            Assert.That(bootstrap, Does.Contain("3949b154060094045993ae7b38f21d9d"));
            Assert.That(bootstrap, Does.Contain("2340804867759a540896c8a7fd5ab8cb"));
        }

        /// <summary>
        /// Opens a scene and fails on any object scaled to nothing, except the
        /// canvas roots whose scale the canvas system owns.
        /// </summary>
        private static void AssertOnlyCanvasesAreUnscaled(string scenePath)
        {
            UnityEngine.SceneManagement.Scene scene =
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    scenePath,
                    UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Transform part in
                        root.GetComponentsInChildren<Transform>(true))
                    {
                        if (part.localScale != Vector3.zero
                            || part.GetComponent<Canvas>() != null)
                        {
                            continue;
                        }

                        Assert.Fail(
                            $"'{part.name}' in {scenePath} is scaled to "
                            + "nothing, so nothing under it will be seen.");
                    }
                }
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(
                    scene,
                    true);
            }
        }

        private static void AssertBuildSafeMaterial(
            Material material,
            string context)
        {
            Assert.That(material, Is.Not.Null, context);
            Assert.That(material.shader, Is.Not.Null, context);
            Assert.That(material.shader.isSupported, Is.True, context);
            Assert.That(
                material.shader.name,
                Does.Not.Contain("InternalError"),
                context);
        }

        private static void AssertRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            Assert.That(rect, Is.Not.Null);
            Assert.That(rect.anchorMin, Is.EqualTo(anchorMin));
            Assert.That(rect.anchorMax, Is.EqualTo(anchorMax));
            Assert.That(rect.pivot, Is.EqualTo(pivot));
            Assert.That(rect.anchoredPosition, Is.EqualTo(anchoredPosition));
            Assert.That(rect.sizeDelta, Is.EqualTo(sizeDelta));
            Assert.That(rect.localScale, Is.EqualTo(Vector3.one));
        }
    }
}
