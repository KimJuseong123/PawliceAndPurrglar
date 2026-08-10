using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Items
{
    /// <summary>
    /// Owns only the timing of a charged throw. Gameplay resolution remains in
    /// <see cref="ToolUseAction"/> so the network host still decides the hit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThrowChargeController : MonoBehaviour
    {
        [SerializeField, Min(0.05f)]
        private float fullChargeSeconds = 1f;

        public bool IsCharging { get; private set; }
        public float Charge01 { get; private set; }

        public void Begin()
        {
            IsCharging = true;
            Charge01 = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (!IsCharging)
            {
                return;
            }

            Charge01 = Mathf.Clamp01(
                Charge01 + Mathf.Max(0f, deltaTime)
                / Mathf.Max(0.05f, fullChargeSeconds));
        }

        public float Release()
        {
            float value = Charge01;
            IsCharging = false;
            Charge01 = 0f;
            return value;
        }

        public void Cancel()
        {
            IsCharging = false;
            Charge01 = 0f;
        }
    }
}
