using UnityEngine;

namespace CombatSystem.Data.Player
{
    [CreateAssetMenu(menuName = "CombatKit/Player/Player Weapon Hitbox Config", fileName = "PlayerWeaponHitboxConfig")]
    public class PlayerWeaponHitboxConfig : ScriptableObject
    {
        public Vector2 hitWindow = new Vector2(0.25f, 0.75f);
        public Vector2 combo2HitWindow = new Vector2(0.1f, 0.25f);
        public Vector2 combo3HitWindow = new Vector2(0.25f, 0.5f);
        public Vector2 skillFirstHitWindow = new Vector2(0.10f, 0.18f);
        public Vector2 skillHitWindow = new Vector2(0.47f, 0.57f);
        public Vector3 skillImpactCenter = new Vector3(0f, -0.1f, 1.2f);
        public Vector3 skillImpactSize = new Vector3(1.8f, 2.2f, 2.4f);
        public Vector2 skillAHitWindow = new Vector2(15f / 45f, 16f / 45f);
        [Header("Upper attack")]
        public Vector2 upperHitWindow = new Vector2(9f / 75f, 16f / 75f);
        public Vector3 upperImpactCenter = new Vector3(0, -.15f, 1.2f);
        public Vector3 upperImpactSize = new Vector3(1.4f, 2.2f, 2f);
        [Header("Air combo (Combo_Attack_Air_06_01-04)")]
        // Normalized time per step. Estimated from the right-arm swing in each clip; tune in play mode.
        public Vector2[] airHitWindows = {
            new Vector2(.12f, .32f), new Vector2(.02f, .30f), new Vector2(.18f, .32f), new Vector2(.02f, .36f) };
        public Vector3 airImpactCenter = new Vector3(0, -.1f, 1.25f);
        public Vector3 airImpactSize = new Vector3(1.8f, 2.5f, 2.5f);
        [Header("Ground combo (Combo_Attack_04_01-04)")]
        // Normalized time per step. Estimated from the right-arm swing in each clip; tune in play mode.
        public Vector2[] groundHitWindows = {
            new Vector2(.05f, .20f), new Vector2(.12f, .25f), new Vector2(.02f, .15f), new Vector2(.18f, .35f) };
        [Header("Ground combo right-foot kick")]
        [Tooltip("Ground combo step (0-based) that hits with the right foot instead of the sword. -1 = none.")]
        public int groundKickComboIndex = -1;
        public Vector2 groundKickWindow = new Vector2(.25f, .46f);
        [Min(.01f)] public float groundKickRadius = .3f;
        [Header("Skill C chase leap (Attack_Up_Floor_To_Air_02)")]
        // Normalized time of the rising slash; uses the air combo impact box.
        public Vector2 chaseHitWindow = new Vector2(.45f, .80f);
        [Header("Skill C launcher")]
        public Vector2 skillCHitWindow = new Vector2(.30f, .44f);
        public Vector3 skillCImpactCenter = new Vector3(0f, -.1f, 1.1f);
        public Vector3 skillCImpactSize = new Vector3(1.6f, 2.4f, 2.2f);
    }
}
