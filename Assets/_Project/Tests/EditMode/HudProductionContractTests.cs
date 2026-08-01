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
            Assert.That(prefab.transform.Find("Quick Slots"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Bag Button"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Voice Button"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Voice Button/Label"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Match Timer"), Is.Null);
            Assert.That(prefab.transform.Find("TopRightMinimap"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Objective Text"), Is.Null);
            Assert.That(prefab.transform.Find("Police Catches"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Catch Progress"), Is.Null);
            Assert.That(prefab.transform.Find("Sensor Radar"), Is.Not.Null);
            Assert.That(prefab.transform.Find("ANIMAL COMMANDS"), Is.Not.Null);

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
                prefab.transform.Find("ANIMAL COMMANDS") as RectTransform,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                new Vector2(24f, 126f),
                new Vector2(292f, 214f));
            Assert.That(
                prefab.transform.Find("ANIMAL COMMANDS/Ctrl Command 1"),
                Is.Not.Null);
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
        public void AnimalCommandsAdvertiseCtrlNumberBindings()
        {
            Assert.That(
                GameplayInputRouter.GetAnimalCommandLabel(1),
                Is.EqualTo("CTRL + 1"));
            Assert.That(
                GameplayInputRouter.GetAnimalCommandLabel(4),
                Is.EqualTo("CTRL + 4"));
        }

        [Test]
        public void ProductionGameSceneKeepsAuthoredReferencesAndVisibleCanvas()
        {
            Assert.That(File.Exists(GameScenePath), Is.True, GameScenePath);
            string scene = File.ReadAllText(GameScenePath);
            Assert.That(scene, Does.Not.Contain("Generated/Validation"));
            Assert.That(scene, Does.Not.Contain("m_LocalScale: {x: 0, y: 0, z: 0}"));
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
