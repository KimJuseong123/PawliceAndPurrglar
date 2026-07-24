using System;
using System.Collections.Generic;

namespace PawsAndLoot.Core
{
    public static class GameSceneCatalog
    {
        public const string SceneRoot = "Assets/_Project/Scenes";

        private static readonly IReadOnlyList<GameSceneId> BuildOrderValues =
            Array.AsReadOnly(
                new[]
                {
                    GameSceneId.Bootstrap,
                    GameSceneId.Game,
                    GameSceneId.Result
                });

        public static IReadOnlyList<GameSceneId> BuildOrder => BuildOrderValues;

        public static string GetName(GameSceneId sceneId)
        {
            return sceneId switch
            {
                GameSceneId.Bootstrap => "Bootstrap",
                GameSceneId.Game => "Game",
                GameSceneId.Result => "Result",
                _ => throw new ArgumentOutOfRangeException(nameof(sceneId), sceneId, "Unknown game scene.")
            };
        }

        public static string GetPath(GameSceneId sceneId)
        {
            return $"{SceneRoot}/{GetName(sceneId)}.unity";
        }
    }
}
