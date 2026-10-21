using UnityEngine;
using CombatSystem.Core;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>
    /// 락온 대상 선택. Z 키로 가장 가까운 적에게 락온을 켜고 끄며, 마우스 휠로 플레이어 기준 시계 방향으로
    /// 대상을 순환한다. 대상이 쓰러지거나 범위를 벗어나면 남은 적 중 가장 가까운 적으로 넘긴다.
    /// 카메라와 공격 방향 전환이 <see cref="Target"/>을 읽는다.
    /// </summary>
    public sealed class LockOnModule : PlayerModule
    {
        #region 필드

        private PlayerProfile.LockOnSettings config;

        /// <summary>현재 락온된 적. 락온이 꺼져 있으면 null.</summary>
        public ICombatant Target { get; private set; }
        public bool IsLocked => Target != null;

        #endregion

        #region 대상 선택

        /// <summary>프로필 설정을 캐싱한다.</summary>
        protected override void OnInitialize()
        {
            config = Player.Profile.lockOn;
        }

        /// <summary>매 프레임 락온 토글·대상 순환 입력을 처리하고, 무효해진 대상을 자동으로 교체한다.</summary>
        public void Tick()
        {
            var controls = Player.Controls;
            if (controls.LockOnPressed)
            {
                Target = IsLocked ? null : FindNearest(null, config.acquireRange);
                return;
            }
            if (!IsLocked) return;
            if (!IsValid(Target, config.keepRange))
            {
                // 쓰러졌거나 비활성화됐거나 너무 멀어졌다면, 남은 적 중 가장 가까운 적으로 전투를 이어간다.
                Target = FindNearest(Target, config.acquireRange);
                return;
            }
            if (controls.LockOnNextPressed) Target = FindClockwise(Target) ?? Target;
        }

        /// <summary>범위 안의 유효한 적 중 수평 거리가 가장 가까운 적을 찾는다. exclude는 후보에서 제외한다.</summary>
        private ICombatant FindNearest(ICombatant exclude, float range)
        {
            ICombatant best = null;
            float bestSqr = range * range;
            foreach (var enemy in CombatRegistry.GetAll(CombatTeam.Enemy))
            {
                if (enemy == exclude || !IsValid(enemy, range)) continue;
                float sqr = Flat(enemy.Transform.position - transform.position).sqrMagnitude;
                if (sqr > bestSqr) continue;
                bestSqr = sqr;
                best = enemy;
            }
            return best;
        }

        /// <summary>
        /// 플레이어를 중심으로 위에서 봤을 때 현재 대상에서 시계 방향 각도가 가장 작은 적을 찾는다.
        /// 반복 입력 시 모든 적을 일정한 순서로 돌아가며 선택하게 된다.
        /// </summary>
        private ICombatant FindClockwise(ICombatant current)
        {
            Vector3 from = Flat(current.Transform.position - transform.position);
            ICombatant best = null;
            float bestAngle = float.PositiveInfinity;
            foreach (var enemy in CombatRegistry.GetAll(CombatTeam.Enemy))
            {
                if (enemy == current || !IsValid(enemy, config.acquireRange)) continue;
                float angle = Vector3.SignedAngle(from, Flat(enemy.Transform.position - transform.position), Vector3.up);
                if (angle <= 0f) angle += 360f; // Unity의 왼손 좌표계(y-up)에서는 양수 각도가 시계 방향이다.
                if (angle >= bestAngle) continue;
                bestAngle = angle;
                best = enemy;
            }
            return best;
        }

        /// <summary>적이 활성 상태이고 살아 있으며(일반 적·보스 모두) 지정 범위 안에 있어 락온 가능한지 판정한다.</summary>
        private bool IsValid(ICombatant enemy, float range)
        {
            if (enemy == null || enemy.Transform == null || !enemy.Transform.gameObject.activeInHierarchy) return false;
            var health = enemy.Transform.GetComponent<CombatSystem.Combat.EnemyDamageReaction>();
            if (health != null && health.CurrentHealth <= 0f) return false;
            var boss = enemy as CombatSystem.Boss.BossCombat;
            if (boss != null && boss.CurrentHealth <= 0f) return false;
            return Flat(enemy.Transform.position - transform.position).sqrMagnitude <= range * range;
        }

        /// <summary>높이 성분을 제거해 수평 거리·각도 계산에 쓸 벡터로 만든다.</summary>
        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>플레이어 비활성화 시 락온을 해제한다.</summary>
        public override void OnDisable() { Target = null; }

        #endregion
    }
}
