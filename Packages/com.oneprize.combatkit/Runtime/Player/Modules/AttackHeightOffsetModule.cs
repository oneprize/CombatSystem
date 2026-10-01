using UnityEngine;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>
    /// IK-pass body height correction: keeps ground/air combo poses on the capsule floor and
    /// lowers specific attack/guard poses whose source clips were authored at another height.
    /// </summary>
    public sealed class AttackHeightOffsetModule : PlayerModule
    {
        private static readonly HumanBodyBones[] ContactBones =
        {
            HumanBodyBones.Hips, HumanBodyBones.Head, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
            HumanBodyBones.LeftToes, HumanBodyBones.RightToes, HumanBodyBones.LeftHand, HumanBodyBones.RightHand
        };
        private PlayerProfile.AttackHeightOffsetSettings config;

        protected override void OnInitialize()
        {
            config = Player.Profile.attackHeightOffset;
        }

        private float GetOffset(AnimatorStateInfo state)
        {
            if (state.IsName("Base Layer.Attack")) return config.heightOffset;
            if (state.IsName("Base Layer.Attack2")) return config.combo2HeightOffset;
            if (state.IsName("Base Layer.Attack3")) return config.combo3HeightOffset;
            if (state.IsName("Base Layer.Guard")) return config.guardHeightOffset;
            if (state.IsName("Base Layer.GuardAccept")) return config.guardAcceptHeightOffset;
            return 0f;
        }

        public void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || !animator.isHuman) return;
            var currentState = animator.GetCurrentAnimatorStateInfo(0);
            var nextState = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : default(AnimatorStateInfo);
            bool groundedReaction = currentState.IsName("BossKnockDown") || currentState.IsName("BossStandUpFront") || currentState.IsName("BossStandUpBack") ||
                nextState.IsName("BossKnockDown") || nextState.IsName("BossStandUpFront") || nextState.IsName("BossStandUpBack");
            // Keep the visual body aligned during the entire fall and landing, too.
            if (currentState.IsTag("GroundCombo") || nextState.IsTag("GroundCombo") || groundedReaction || Player.AirCombo.Active ||
                currentState.IsTag("AirCombo") || nextState.IsTag("AirCombo") ||
                currentState.IsName("UpperAttack") || nextState.IsName("UpperAttack"))
            {
                if (character != null)
                {
                    float lowest = float.PositiveInfinity;
                    foreach (var bone in ContactBones)
                    {
                        var joint = animator.GetBoneTransform(bone);
                        if (joint != null) lowest = Mathf.Min(lowest, joint.position.y);
                    }
                    if (!float.IsPositiveInfinity(lowest))
                        animator.bodyPosition += Vector3.up * (character.bounds.min.y + .04f - lowest);
                    return;
                }
            }
            float weight = GetOffset(currentState);
            if (animator.IsInTransition(0))
            {
                float next = GetOffset(nextState);
                weight = Mathf.Lerp(weight, next, Mathf.Clamp01(animator.GetAnimatorTransitionInfo(0).normalizedTime));
            }
            weight += config.visualLowering;
            // 관절 회전이나 클립 곡선은 바꾸지 않고, 평가된 몸 전체의 높이만 평행 이동한다.
            if (weight > 0f)
                animator.bodyPosition -= transform.up * (transform.lossyScale.y * weight);
        }
    }
}
