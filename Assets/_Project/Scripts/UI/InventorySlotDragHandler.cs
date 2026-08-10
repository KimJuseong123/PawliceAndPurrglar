using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// Makes one inventory cell draggable onto another.
    ///
    /// Wired by finding its own controller at runtime rather than by an assigned
    /// callback. An editor script cannot hand a delegate to a prefab — the same
    /// reason <c>onClick.AddListener</c> from an editor script leaves six dead
    /// buttons in the lobby (<c>ISSUE-017</c>): a non-persistent listener is gone
    /// the moment the prefab is written to disk, and the editor keeps working, so
    /// only the build is broken. A serialized enum and a parent lookup both
    /// survive the round trip.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventorySlotDragHandler : MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        public enum SlotGroup
        {
            /// <summary>A numbered loot cell in the bag grid.</summary>
            Bag = 0,

            /// <summary>One of the four prop slots along the bottom.</summary>
            QuickSlot = 1,

            /// <summary>A cell in a searched container.</summary>
            Container = 2
        }

        [SerializeField] private SlotGroup group = SlotGroup.Bag;
        [SerializeField] private int index = -1;

        private RoleAwareHudController _controller;
        private GameObject _ghost;

        public SlotGroup Group => group;
        public int Index => index;

        public void Configure(SlotGroup configuredGroup, int configuredIndex)
        {
            group = configuredGroup;
            index = configuredIndex;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            // Asked of the cell, not of the artwork. Deciding by "is there an icon
            // sprite" meant the twenty-odd loot kinds with no icon yet — drawn as a
            // letter — could not be dragged at all, and an empty-handed cursor over
            // a full cell reads as the drag being broken.
            if (!HoldsSomething())
            {
                return;
            }

            _ghost = BuildGhost(FindIcon());
            MoveGhost(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            MoveGhost(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_ghost != null)
            {
                Destroy(_ghost);
                _ghost = null;
            }

            InventorySlotDragHandler target = eventData.pointerCurrentRaycast
                .gameObject == null
                ? null
                : eventData.pointerCurrentRaycast.gameObject
                    .GetComponentInParent<InventorySlotDragHandler>();
            if (target == null || target == this)
            {
                return;
            }

            ResolveController()?.HandleSlotDrop(this, target);
        }

        private void MoveGhost(PointerEventData eventData)
        {
            if (_ghost != null)
            {
                _ghost.transform.position = eventData.position;
            }
        }

        /// <summary>
        /// The icon that follows the cursor.
        ///
        /// Parented to the root canvas so it draws over every panel, and with
        /// raycasts off so it does not become the thing the drop lands on — a
        /// ghost that can be hit is a cell that can be dropped onto itself.
        /// </summary>
        private bool HoldsSomething()
        {
            var view = GetComponent<InventorySlotView>();
            return view != null && view.HasContent;
        }

        private GameObject BuildGhost(Sprite icon)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Transform root = canvas != null
                ? canvas.rootCanvas.transform
                : transform.root;
            var ghost = new GameObject(
                "Drag Ghost",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(Image));
            ghost.transform.SetParent(root, false);
            var rect = ghost.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(64f, 64f);
            var image = ghost.GetComponent<Image>();
            image.sprite = icon;
            image.preserveAspect = icon != null;
            image.raycastTarget = false;

            // A tinted square when the piece has no artwork. Something has to
            // follow the cursor: a drag with nothing under the hand is
            // indistinguishable from a drag that never started.
            image.color = icon != null
                ? Color.white
                : new Color(0.25f, 0.85f, 1f, 0.75f);
            var fade = ghost.GetComponent<CanvasGroup>();
            fade.alpha = 0.85f;
            fade.blocksRaycasts = false;
            fade.interactable = false;
            return ghost;
        }

        private Sprite FindIcon()
        {
            foreach (Image image in GetComponentsInChildren<Image>(false))
            {
                if (image != null
                    && image.enabled
                    && image.sprite != null
                    && string.Equals(
                        image.gameObject.name,
                        "Item Icon",
                        System.StringComparison.Ordinal))
                {
                    return image.sprite;
                }
            }

            return null;
        }

        private RoleAwareHudController ResolveController()
        {
            if (_controller == null)
            {
                _controller = GetComponentInParent<RoleAwareHudController>();
            }

            return _controller;
        }

        private void OnDisable()
        {
            if (_ghost != null)
            {
                Destroy(_ghost);
                _ghost = null;
            }
        }
    }
}
