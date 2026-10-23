using UnityEngine;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>
    /// IK 패스에서 몸 높이를 보정한다. 지상/공중 콤보 포즈는 캡슐 바닥에 붙여 두고,
    /// 원본 클립이 다른 높이로 제작된 일부 공격/가드 포즈는 아래로 내려 맞춘다.
    /// </summary>
    public sealed class AttackHeightOffsetModule : PlayerModule
    {
        #region 필드

        /// <summary>콤보 포즈에서 바닥에 닿을 수 있는 뼈 목록. 이 중 가장 낮은 뼈를 접지 높이로 본다.</summary>
        private static readonly HumanBodyBones[] ContactBones =
        {
            HumanBodyBones.Hips, HumanBodyBones.Head, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
            HumanBodyBones.LeftToes, HumanBodyBones.RightToes, HumanBodyBones.LeftHand, HumanBodyBones.RightHand
        };
        private PlayerProfile.AttackHeightOffsetSettings config;

        #endregion

        #region 높이 보정

        /// <summary>프로필 설정을 캐싱한다.</summary>
        protected override void OnInitialize()
        {
            config = Player.Profile.attackHeightOffset;
        }

        /// <summary>애니메이터 상태별로 인스펙터에서 지정한 몸 낮춤 값을 돌려준다. 해당 없는 상태는 0.</summary>
        private float GetOffset(AnimatorStateInfo state)
        {
            if (state.IsName("Base Layer.Attack")) return config.heightOffset;
            if (state.IsName("Base Layer.Attack2")) return config.combo2HeightOffset;
            if (state.IsName("Base Layer.Attack3")) return config.combo3HeightOffset;
            if (state.IsName("Base Layer.Guard")) return config.guardHeightOffset;
            if (state.IsName("Base Layer.GuardAccept")) return config.guardAcceptHeightOffset;
            return 0f;
        }

        /// <summary>
        /// PlayerCharacter의 IK 콜백에서 호출된다. 콤보·다운 리액션 중에는 가장 낮은 접촉 뼈를 캡슐 바닥에 맞추고,
        /// 그 외에는 상태별 오프셋(전이 중 보간)만큼 몸 전체를 내린다.
        /// </summary>
        public void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || !animator.isHuman) return;
            var currentState = animator.GetCurrentAnimatorStateInfo(0);
            var nextState = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : default(AnimatorStateInfo);
            bool groundedReaction = currentState.IsName("HighFallRoll") || nextState.IsName("HighFallRoll") || currentState.IsName("BossKnockDown") || currentState.IsName("BossStandUpFront") || currentState.IsName("BossStandUpBack") ||
                nextState.IsName("BossKnockDown") || nextState.IsName("BossStandUpFront") || nextState.IsName("BossStandUpBack");
            // 낙하와 착지 구간 전체에서도 보이는 몸 높이가 어긋나지 않도록 유지한다.
            if (currentState.IsTag("GroundedParry") || nextState.IsTag("GroundedParry") || currentState.IsTag("GroundCombo") || nextState.IsTag("GroundCombo") || groundedReaction || Player.AirCombo.Active ||
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
                    {
                        // CharacterController의 skin 두께 때문에 bounds 하단이 실제 접촉면보다 위에 있다.
                        // 지상 콤보는 접촉면 높이를 써야 하지만, 캡슐의 점프 이동량은 그대로 두어
                        // 공중에 뜬 발을 레이캐스트 바닥으로 끌어내리지 않는다.
                        bool groundCombo = currentState.IsTag("GroundCombo") || nextState.IsTag("GroundCombo");
                        float skin = groundCombo ? character.skinWidth * Mathf.Abs(transform.lossyScale.y) : 0f;
                        float correction = character.bounds.min.y - skin + .04f - lowest;
                        animator.bodyPosition += Vector3.up * correction;
                    }
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

        #endregion
    }
}
