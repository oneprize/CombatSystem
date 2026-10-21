using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem.Core
{
    /// <summary>
    /// 활성 전투 참가자를 팀별로 모아 두는 경량 런타임 목록이다. 인스펙터에서 직접 끌어다 놓는 참조나
    /// 씬 전체 FindObjectOfType 탐색을 대신한다. 스폰된 액터는 OnEnable/OnDisable에서 스스로 등록·해제하고,
    /// "플레이어"나 "적"을 찾아야 하는 쪽은 특정 GameObject를 알 필요 없이 여기를 조회한다.
    /// 스폰 순서나 팀별 액터 수와 관계없이 동작한다.
    /// </summary>
    public static class CombatRegistry
    {
        #region 필드

        private static readonly List<ICombatant> players = new List<ICombatant>();
        private static readonly List<ICombatant> enemies = new List<ICombatant>();

        #endregion

        #region 공개 API

        /// <summary>팀에 해당하는 내부 등록 목록을 돌려준다.</summary>
        private static List<ICombatant> ListFor(CombatTeam team) => team == CombatTeam.Player ? players : enemies;

        /// <summary>전투 참가자를 소속 팀 목록에 중복 없이 등록한다. 보통 OnEnable에서 호출된다.</summary>
        public static void Register(ICombatant combatant)
        {
            if (combatant == null) return;
            var list = ListFor(combatant.Team);
            if (!list.Contains(combatant)) list.Add(combatant);
        }

        /// <summary>전투 참가자를 소속 팀 목록에서 제거한다. 보통 OnDisable에서 호출된다.</summary>
        public static void Unregister(ICombatant combatant)
        {
            if (combatant == null) return;
            ListFor(combatant.Team).Remove(combatant);
        }

        /// <summary>
        /// 팀에 현재 등록된 모든 전투 참가자를 돌려준다.
        /// Transform이 파괴된 항목은 조회 시점에 지연 정리된다.
        /// </summary>
        public static IReadOnlyList<ICombatant> GetAll(CombatTeam team)
        {
            var list = ListFor(team);
            list.RemoveAll(c => c == null || c.Transform == null);
            return list;
        }

        /// <summary>지정 위치에서 가장 가까운 해당 팀 전투 참가자를 찾는다. 대상이 없으면 null을 돌려준다.</summary>
        public static ICombatant FindNearest(CombatTeam team, Vector3 from)
        {
            ICombatant nearest = null;
            float nearestSqr = float.MaxValue;
            foreach (var combatant in GetAll(team))
            {
                float sqr = (combatant.Transform.position - from).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = combatant;
                }
            }
            return nearest;
        }

        #endregion
    }
}
