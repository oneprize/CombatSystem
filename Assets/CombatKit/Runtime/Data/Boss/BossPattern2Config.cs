using UnityEngine;
namespace CombatSystem.Data.Boss
{
    [CreateAssetMenu(menuName = "CombatKit/Boss/Boss Pattern 2 Config", fileName = "BossPattern2Config")]
    public class BossPattern2Config : ScriptableObject
    {
        [Header("Pattern 2 - Charge and Jump")]
        [Min(0)] public float chargeWindup = .7f;
        [Min(.1f)] public float chargeSpeed = 12f;
        [Min(0)] public float jumpStartDistance = 3f;
        [Min(.1f)] public float jumpDuration = .8f;
        [Min(0)] public float jumpHeight = 3f;
        [Min(0)] public float landingRecovery = 1f;
        [Min(.1f)] public float impactRadius = 3f;
        [Min(.1f)] public float impactHeight = 2.5f;
        [Header("Charge Tracking")]
        [Min(.1f)] public float maxChargeDistance = 20f;
        [Min(.1f)] public float minTurnRadius = 12f;
        [Min(.1f)] public float trackingStopDistance = 5f;
        [Min(.1f)] public float maxChargeDuration = 1.5f;
        [Header("Pattern 2 Animation")]
        [Range(0, .9f)] public float jumpTakeoffTime = .2f;
        [Range(.1f, 1)] public float jumpLandingTime = .75f;
        [Min(.1f)] public float chargeHitRadius = 2f;
        public float chargeHitHeight = 2f;
    }
}
