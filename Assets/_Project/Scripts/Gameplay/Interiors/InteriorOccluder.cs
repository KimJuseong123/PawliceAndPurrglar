using UnityEngine;

namespace PawsAndLoot.Gameplay.Interiors
{
    /// <summary>
    /// A wall or a big piece of furniture that is allowed to get out of the way.
    ///
    /// It exists because the collider and the thing you see are not the same object
    /// in these rooms. The walls have door-shaped holes, so they are made solid with
    /// mesh colliders built as separate objects rather than by adding components to
    /// the model's own parts — a component added to a prefab instance's child is
    /// written into the scene as an override, and there are 335 parts per room.
    ///
    /// So a raycast from the camera hits a collider that draws nothing, and something
    /// has to say which renderer that collider was cut from. That link is recorded
    /// here at build time, as a single object reference: matching them up by name at
    /// runtime would be one more thing that fails silently the day a part is renamed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteriorOccluder : MonoBehaviour
    {
        [SerializeField]
        private Renderer view;

        /// <summary>
        /// True while hidden, so the view that hides these can put them back without
        /// keeping its own bookkeeping in step.
        /// </summary>
        public bool IsHidden { get; private set; }

        public Renderer View => view;

        public void Configure(Renderer configuredView)
        {
            view = configuredView;
        }

        public void SetHidden(bool hidden)
        {
            if (view == null || IsHidden == hidden)
            {
                return;
            }

            IsHidden = hidden;
            view.enabled = !hidden;
        }

        /// <summary>
        /// Put back on the way out, whatever state it was left in. A wall that stays
        /// invisible because the camera stopped asking is not something a player can
        /// work around.
        /// </summary>
        private void OnDisable()
        {
            SetHidden(false);
        }
    }
}
