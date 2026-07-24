using UnityEngine;
using UnityEngine.SceneManagement;
using PawsAndLoot.Logging;

namespace PawsAndLoot.Core
{
    public static class GameSceneLoader
    {
        public static void Load(GameSceneId sceneId)
        {
            string sceneName = GameSceneCatalog.GetName(sceneId);

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                GameLogger.Error(
                    GameLogCategory.Match,
                    $"Scene '{sceneName}' is not enabled in Build Settings.");
                return;
            }

            GameLogger.Info(
                GameLogCategory.Match,
                $"Loading scene '{sceneName}' from '{SceneManager.GetActiveScene().name}'.");
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
}
