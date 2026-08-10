using System;
using System.Collections.Generic;
using PawliceAndPurrglar.Core;
using UnityEngine.SceneManagement;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// Runs a screen's installer every time the match scene loads, rather than
    /// once when the game starts.
    ///
    /// This exists because of a trap that had already been written down in
    /// <see cref="RoleAwareHudController"/> and then walked into three more
    /// times. <c>RuntimeInitializeOnLoadMethod(AfterSceneLoad)</c> fires **once
    /// per run of the game**, after the *first* scene loads — and the first
    /// scene of a real build is <c>Bootstrap</c>, the lobby. So an installer
    /// hung off that callback:
    ///
    /// 1. runs while the lobby is open, where the thing it is looking for does
    ///    not exist yet, and
    /// 2. never runs again, so the match scene never gets it, and
    /// 3. if it did build something, that object belongs to the lobby scene and
    ///    is destroyed by the single-mode load that opens the match.
    ///
    /// Every one of those is silent. Nothing throws, nothing logs, and in the
    /// editor it all works — pressing Play with <c>Game</c> already open fires
    /// the callback after *that* scene loaded, so the screen appears and the
    /// tests pass. It is missing only in the build, which is the one place
    /// nobody can attach a debugger to.
    ///
    /// The installers are kept in a list and driven from one
    /// <see cref="SceneManager.sceneLoaded"/> subscription so the subscription
    /// cannot be duplicated per screen, and each installer is expected to check
    /// for its own component before building anything — a scene load is not a
    /// promise that the previous one is gone.
    /// </summary>
    internal static class MatchSceneInstaller
    {
        private static readonly List<Action> Installers = new();
        private static bool _subscribed;

        /// <summary>
        /// Registers <paramref name="install"/> and runs it now if the match
        /// scene is already the active one.
        ///
        /// Running it immediately is what keeps the editor's "press Play inside
        /// Game" workflow — and every Play Mode test — working: in that case the
        /// scene load already happened and no further event is coming.
        /// </summary>
        public static void Register(Action install)
        {
            if (install == null)
            {
                return;
            }

            // Compared by target and method rather than by reference, which is
            // what delegate equality does, so registering the same static twice
            // across a domain reload does not install twice.
            if (!Installers.Contains(install))
            {
                Installers.Add(install);
            }

            if (!_subscribed)
            {
                _subscribed = true;
                SceneManager.sceneLoaded += HandleSceneLoaded;
            }

            if (IsMatchScene(SceneManager.GetActiveScene().name))
            {
                install();
            }
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsMatchScene(scene.name))
            {
                return;
            }

            // Indexed rather than foreach: an installer is allowed to register
            // another one, and a collection modified during a foreach throws.
            for (int index = 0; index < Installers.Count; index++)
            {
                Installers[index]?.Invoke();
            }
        }

        /// <summary>
        /// Asked of the catalogue rather than compared against the literal
        /// "Game", so renaming the scene does not quietly switch every one of
        /// these screens off.
        /// </summary>
        private static bool IsMatchScene(string sceneName)
        {
            return string.Equals(
                sceneName,
                GameSceneCatalog.GetName(GameSceneId.Game),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
