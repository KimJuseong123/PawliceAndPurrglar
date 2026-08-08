using UnityEngine;

namespace PawsAndLoot.Integration.Voice
{
    [CreateAssetMenu(
        menuName = "PawliceAndPurrglar/Voice/Dog Behavior Profile",
        fileName = "DogBehaviorProfile")]
    public sealed class DogBehaviorProfile : ScriptableObject
    {
        [SerializeField, Range(0f, 1f)] private float distractionChance = 0.25f;
        [SerializeField, Range(0f, 1f)] private float catChaseChance = 0.2f;
        [SerializeField, Range(0f, 1f)] private float longCommandPartialChance = 0.2f;
        [SerializeField, Range(0f, 1f)] private float urgentCommandReduction = 0.5f;
        [SerializeField, Min(0f)] private float distractionRadius = 4f;

        public float DistractionChance => distractionChance;
        public float CatChaseChance => catChaseChance;
        public float LongCommandPartialChance => longCommandPartialChance;
        public float UrgentCommandReduction => urgentCommandReduction;
        public float DistractionRadius => distractionRadius;
    }
}
