using UnityEngine;
namespace ParrySystem.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerDamageReaction))]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private string playerName = "Player";
        [SerializeField, Min(1)] private float maxHealth = 100f;
        [SerializeField, Min(0)] private float weakAttackDamage = 10f;
        [SerializeField, Min(0)] private float strongAttackDamage = 20f;
        public string PlayerName => playerName;
        public float MaxHealth => Mathf.Max(1f, maxHealth);
        public float CurrentHealth { get; private set; }
        private PlayerDamageReaction reaction;
        private void Awake() { reaction = GetComponent<PlayerDamageReaction>(); CurrentHealth = MaxHealth; }
        private void OnEnable() { reaction.DamageReceived += OnDamage; }
        private void OnDisable() { reaction.DamageReceived -= OnDamage; }
        private void OnDamage(int comboIndex)
        { CurrentHealth = Mathf.Clamp(CurrentHealth - (comboIndex == 3 ? strongAttackDamage : weakAttackDamage), 0, MaxHealth); }
    }
}