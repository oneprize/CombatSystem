using UnityEngine;

namespace CombatSystem.Player
{
    /// <summary>
    /// 애니메이터의 루트 모션을 현재 이동을 소유한 동작(공중 대시, 관통 돌진, 회피, 올려베기, 추격 공격,
    /// 지상 콤보 피니시, 파워 공격, 공중 콤보, 일반 공격/피격 리액션)에 맞게 분배한다.
    /// </summary>
    public sealed class RootMotionModule : PlayerModule
    {
        #region 지상 콤보 피니시

        // Player_Combo04_4의 가장 낮은 발 높이 궤적을 제작된 57프레임마다 샘플링한 값.
        // humanScale 단위로 저장해 아바타를 리타깃해도 점프 높이가 일정하게 유지된다.
        private static readonly float[] GroundFinisherHeights = { 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.135146f, 0.264373f, 0.391571f, 0.516572f, 0.635510f, 0.695620f, 0.734403f, 0.719226f, 0.570821f, 0.307487f, 0.008205f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f };
        private int finisherSequence = -1;
        private float previousFinisherHeight;

        /// <summary>
        /// 지상 콤보 피니시(Attack4)의 수평 루트 모션은 그대로 쓰고, 높이는 샘플링한 발 궤적으로 덮어써
        /// 클립 높이 커브 대신 코드가 점프 높이를 제어하게 한다.
        /// </summary>
        private void ApplyGroundFinisher(Vector3 delta)
        {
            if (!animator.applyRootMotion || character == null || !character.enabled) return;
            var combat = Player.Combat;
            if (finisherSequence != combat.AttackSequence)
            {
                finisherSequence = combat.AttackSequence;
                previousFinisherHeight = 0f;
            }
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                if (next.IsName("Base Layer.Attack4")) state = next;
            }
            if (!state.IsName("Base Layer.Attack4")) return;
            float sample = Mathf.Clamp01(combat.SourceAttackProgress(state)) * (GroundFinisherHeights.Length - 1);
            int index = Mathf.Min(Mathf.FloorToInt(sample), GroundFinisherHeights.Length - 2);
            float height = Mathf.Lerp(GroundFinisherHeights[index], GroundFinisherHeights[index + 1],
                sample - index) * animator.humanScale;
            delta.y = height - previousFinisherHeight;
            previousFinisherHeight = height;
            character.Move(delta);
        }

        #endregion

        #region 루트 모션 분배

        /// <summary>PlayerCharacter의 OnAnimatorMove에서 호출되어, 우선순위대로 현재 동작에 루트 모션 이동량을 넘긴다.</summary>
        public void Apply(Vector3 delta)
        {
            if (Player.AirCombo.IsAirDashing) { Player.AirCombo.ApplyAirDashRootMotion(delta); return; }
            var combat = Player.Combat;
            if (combat.IsParryAttempt || (combat.IsParryCounter && !combat.IsCounterAttacking)) return;
            if (combat.IsUsingPiercingSkill)
            {
                Player.PiercingDash.TickAnimation();
                return;
            }
            if (Player.IsDodging)
            {
                if (animator.applyRootMotion) Player.Dodge.ApplyRootMotion(delta);
                return;
            }
            if (combat.IsUsingUpperAttack)
            {
                Player.AirCombo.ApplyUpperMotion(delta);
                return;
            }
            if (combat.IsUsingChaseAttack)
            {
                Player.AirCombo.ApplyChaseMotion();
                return;
            }
            if (combat.IsGroundComboFinisher)
            {
                ApplyGroundFinisher(delta);
                return;
            }
            if (combat.IsReleasingPower)
            {
                // 파워 공격의 돌진과 도약(높이 포함)은 클립의 모션 커브가 그대로 만든다.
                if (animator.applyRootMotion && character != null && character.enabled) character.Move(delta);
                return;
            }
            if (combat.IsUsingAirCombo)
            {
                // 공중 공격의 수평 이동은 클립을 따르고, 높이는 계속 AirComboModule이 담당한다.
                if (!animator.applyRootMotion || character == null || !character.enabled) return;
                delta.y = 0f;
                character.Move(PushTarget(delta, combat.AttackTarget, combat.AirAttackContactDistance));
                return;
            }
            if (Player.IsLinkJumping || Player.IsAirComboActive) return;
            if (Player.Locomotion.IsRunTurning)
            {
                // 달리기 턴은 클립의 루트 모션 회전까지 적용해야 몸과 캐릭터 정면이 함께 돈다.
                if (animator.applyRootMotion) Player.Locomotion.ApplyRunTurnRootMotion(delta, animator.deltaRotation);
                return;
            }
            if (!animator.applyRootMotion || (!Player.IsHitReacting && !Player.IsAttacking)) return;
            // 중력은 이동 모듈이 담당하고 정면 회전은 전투 코드가 담당한다.
            delta.y = 0f;
            if (character != null && character.enabled)
                character.Move(delta);
            else
                transform.position += delta;
        }

        /// <summary>
        /// 플레이어는 클립의 루트 모션을 그대로 따르되, 공중에 뜬 대상과의 거리가 <paramref name="contactDistance"/>보다
        /// 가까워지면 초과분만큼 같은 프레임에 대상을 밀어낸다. 대상이 벽 등에 막혀 밀리지 못한 만큼은 플레이어 이동에서 뺀다.
        /// </summary>
        private Vector3 PushTarget(Vector3 delta, CombatSystem.Combat.EnemyDamageReaction target, float contactDistance)
        {
            if (target == null || !target.isActiveAndEnabled || !target.IsAirborneForCombo) return delta;
            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance < .0001f) return delta;
            Vector3 direction = toTarget / distance;
            float excess = Vector3.Dot(delta, direction) - Mathf.Max(0f, distance - contactDistance);
            if (excess <= 0f) return delta;
            float pushed = Vector3.Dot(target.PushAirborne(direction * excess), direction);
            delta -= direction * Mathf.Max(0f, excess - pushed);
            return delta;
        }

        #endregion
    }
}
