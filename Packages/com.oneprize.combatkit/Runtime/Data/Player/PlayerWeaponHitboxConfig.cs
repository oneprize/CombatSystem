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
        [Header("Air combo")]
        // Attach clips omit recovery frames; preserve the hit times in seconds.
        public Vector2[] airHitWindows = {
            new Vector2(6f/24f,14f/24f), new Vector2(4.5f/18f,10.5f/18f), new Vector2(6.3f/25f,14.7f/25f),
            new Vector2(6f/23f,14f/23f), new Vector2(5.4f/22f,12.6f/22f), new Vector2(6.75f/30f,18f/30f) };
        public Vector3 airImpactCenter = new Vector3(0, -.1f, 1.25f);
        public Vector3 airImpactSize = new Vector3(1.8f, 2.5f, 2.5f);
        [Header("Ground 5 combo")]
        public Vector2[] groundHitWindows = {
            new Vector2(.2f,.55f), new Vector2(.2f,.55f), new Vector2(.2f,.55f),
            new Vector2(.2f,.55f), new Vector2(.2f,.55f) };
        [Header("Ground combo 3 - right-foot kick")]
        public Vector2 groundKickWindow = new Vector2(.25f, .46f);
        [Min(.01f)] public float groundKickRadius = .3f;
        [Header("Skill C launcher")]
        public Vector2 skillCHitWindow = new Vector2(.30f, .44f);
        public Vector3 skillCImpactCenter = new Vector3(0f, -.1f, 1.1f);
        public Vector3 skillCImpactSize = new Vector3(1.6f, 2.4f, 2.2f);
    }
}
