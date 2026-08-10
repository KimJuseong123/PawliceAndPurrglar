using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawliceAndPurrglar.Editor
{
    /// <summary>
    /// Gives every label the project font, just before whatever holds it is
    /// written to disk.
    ///
    /// A TMP label with no font asset does not fail and does not warn. It falls
    /// back to TMP's built-in LiberationSans, which has no Hangul at all, so
    /// the label renders as a row of boxes. The whole main menu came out that
    /// way and the only clue anywhere was a contract test naming one label out
    /// of thirty-six.
    ///
    /// Applied as a sweep at save time rather than at each label, because there
    /// is no label in this game that wants a different font, and remembering to
    /// set it on the next one is a promise nobody keeps. The lobby and result
    /// screens are prefabs rather than scene objects, which is why sweeping the
    /// scenes alone left the menu exactly as broken as before.
    /// </summary>
    internal static class ProjectFontSweep
    {
        public const string FontPath =
            "Assets/Resources/PawliceAndPurrglarDefaultFont.asset";

        public static void Apply(Scene scene)
        {
            TMP_FontAsset font = Load();
            if (font == null)
            {
                return;
            }

            int fixedUp = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                fixedUp += Sweep(root, font);
            }

            Report(fixedUp, scene.name);
        }

        public static void Apply(GameObject root)
        {
            TMP_FontAsset font = Load();
            if (font == null || root == null)
            {
                return;
            }

            Report(Sweep(root, font), root.name);
        }

        private static TMP_FontAsset Load()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                Debug.LogWarning(
                    $"[UI] No font at {FontPath}, so labels keep whatever they "
                    + "have. Korean will not render.");
            }

            return font;
        }

        private static int Sweep(GameObject root, TMP_FontAsset font)
        {
            int fixedUp = 0;
            foreach (TMP_Text label in
                root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.font == font)
                {
                    continue;
                }

                label.font = font;
                EditorUtility.SetDirty(label);
                fixedUp++;
            }

            return fixedUp;
        }

        private static void Report(int fixedUp, string where)
        {
            if (fixedUp > 0)
            {
                Debug.Log(
                    $"[UI] {fixedUp} labels in '{where}' pointed at the "
                    + "project font.");
            }
        }
    }
}
