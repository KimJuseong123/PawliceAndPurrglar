using PawliceAndPurrglar.Gameplay.Players;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    [DisallowMultipleComponent]
    public sealed class PoliceCatchProgressView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text counterLabel;
        [SerializeField] private Image[] slotBackplates = new Image[3];
        [SerializeField] private CircleGraphic[] slotRings = new CircleGraphic[3];
        [SerializeField] private CircleGraphic[] slotFills = new CircleGraphic[3];

        private static readonly Color LitFill =
            new(0.35f, 1f, 0.18f, 0.92f);
        private static readonly Color LitRing =
            new(0.12f, 1f, 0.95f, 1f);
        private static readonly Color EmptyFill =
            new(0.05f, 0.08f, 0.10f, 0.85f);
        private static readonly Color EmptyRing =
            new(0.20f, 0.28f, 0.34f, 0.9f);

        public int CurrentCatchCount { get; private set; }
        public int RequiredCatchCount { get; private set; }

        public void Configure(
            TMP_Text configuredTitleLabel,
            TMP_Text configuredCounterLabel,
            Image[] configuredSlotBackplates,
            CircleGraphic[] configuredSlotRings,
            CircleGraphic[] configuredSlotFills)
        {
            titleLabel = configuredTitleLabel;
            counterLabel = configuredCounterLabel;
            slotBackplates = configuredSlotBackplates ?? new Image[0];
            slotRings = configuredSlotRings ?? new CircleGraphic[0];
            slotFills = configuredSlotFills ?? new CircleGraphic[0];
        }

        public void Bind(
            int currentCatchCount,
            int requiredCatchCount,
            PlayerRole viewerRole)
        {
            RequiredCatchCount = Mathf.Max(1, requiredCatchCount);
            CurrentCatchCount = Mathf.Clamp(
                currentCatchCount,
                0,
                RequiredCatchCount);

            if (titleLabel != null)
            {
                titleLabel.text = "POLICE CATCHES";
            }

            if (counterLabel != null)
            {
                counterLabel.text =
                    $"{CurrentCatchCount} / {RequiredCatchCount}";
            }

            int visibleSlots = Mathf.Min(
                RequiredCatchCount,
                Mathf.Max(slotFills.Length, slotRings.Length));
            for (int index = 0; index < slotBackplates.Length; index++)
            {
                bool visible = index < visibleSlots;
                if (slotBackplates[index] != null)
                {
                    slotBackplates[index].gameObject.SetActive(visible);
                }
            }

            for (int index = 0; index < slotRings.Length; index++)
            {
                bool visible = index < visibleSlots;
                bool lit = index < CurrentCatchCount;
                if (slotRings[index] == null)
                {
                    continue;
                }

                slotRings[index].gameObject.SetActive(visible);
                slotRings[index].color = lit ? LitRing : EmptyRing;
            }

            for (int index = 0; index < slotFills.Length; index++)
            {
                bool visible = index < visibleSlots;
                bool lit = index < CurrentCatchCount;
                if (slotFills[index] == null)
                {
                    continue;
                }

                slotFills[index].gameObject.SetActive(visible);
                slotFills[index].color = lit ? LitFill : EmptyFill;
            }
        }
    }
}
