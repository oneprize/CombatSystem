using UnityEngine;
namespace CombatSystem.Data.Training
{
    [CreateAssetMenu(menuName = "CombatKit/Training/Group Practice Zone Config", fileName = "GroupPracticeZoneConfig")]
    public class GroupPracticeZoneConfig : ScriptableObject
    {
        [Range(0, 20)] public int initialCount = 3;
        public Vector2 halfSize = new Vector2(14, 14);
    }
}
