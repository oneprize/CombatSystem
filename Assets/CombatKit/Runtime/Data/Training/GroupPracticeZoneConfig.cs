using UnityEngine;
namespace CombatSystem.Data.Training
{
    /// <summary>다수 적 연습 구역 설정 에셋.</summary>
    [CreateAssetMenu(menuName = "CombatKit/Training/Group Practice Zone Config", fileName = "GroupPracticeZoneConfig")]
    public class GroupPracticeZoneConfig : ScriptableObject
    {
        /// <summary>구역 시작 시 배치할 적 수.</summary>
        [Range(0, 20)] public int initialCount = 3;
        /// <summary>구역 중심 기준 X/Z 방향 절반 크기. 구역 진입 판정과 적 배치 범위에 쓰인다.</summary>
        public Vector2 halfSize = new Vector2(14, 14);
    }
}
