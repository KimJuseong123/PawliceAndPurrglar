using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.Core
{
    public static class GameSceneLoader
    {
        public static void Load(GameSceneId sceneId)
        {
            string sceneName = GameSceneCatalog.GetName(sceneId);

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"Scene '{sceneName}' is not enabled in Build Settings.");
                return;
            }

            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
}
