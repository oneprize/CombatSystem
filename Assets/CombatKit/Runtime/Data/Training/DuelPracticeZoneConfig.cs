using UnityEngine;
namespace CombatSystem.Data.Training
{
    /// <summary>1:1 대결 연습 구역 설정 에셋.</summary>
    [CreateAssetMenu(menuName = "CombatKit/Training/Duel Practice Zone Config", fileName = "DuelPracticeZoneConfig")]
    public class DuelPracticeZoneConfig : ScriptableObject
    {
        /// <summary>구역 중심 기준 X/Z 방향 절반 크기. 플레이어가 이 범위 안에 있는지로 구역 진입을 판정한다.</summary>
        public Vector2 halfSize = new Vector2(14, 14);
    }
}
