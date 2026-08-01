using NUnit.Framework;
using System.IO;
using PawsAndLoot.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class HudProductionContractTests
    {
        private const string HudPrefabPath = "Assets/Resources/HudCanvas.prefab";
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";

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
            Assert.That(prefab.transform.Find("TopRightMinimap"), Is.Not.Null);
            Assert.That(prefab.transform.Find("ANIMAL COMMANDS"), Is.Null);
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
            Assert.That(scene, Does.Contain("3949b154060094045993ae7b38f21d9d"));
            Assert.That(scene, Does.Contain("2340804867759a540896c8a7fd5ab8cb"));
        }
    }
}
