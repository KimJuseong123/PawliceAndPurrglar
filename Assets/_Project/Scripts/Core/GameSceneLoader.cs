using UnityEngine;
using UnityEngine.SceneManagement;
using PawliceAndPurrglar.Logging;

namespace PawliceAndPurrglar.Core
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

            // In a session the server drives the load so both machines end up
            // in the same scene and NGO can match the in-scene NetworkObjects.
            // A client must never load on its own: doing so desynchronises the
            // object lists and the connection is dropped.
            if (NetworkSceneBridge.TryLoad(sceneName))
            {
                return;
            }

            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
}
