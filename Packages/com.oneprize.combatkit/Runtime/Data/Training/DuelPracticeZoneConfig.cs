using UnityEngine;
namespace CombatSystem.Data.Training
{
    [CreateAssetMenu(menuName = "CombatKit/Training/Duel Practice Zone Config", fileName = "DuelPracticeZoneConfig")]
    public class DuelPracticeZoneConfig : ScriptableObject
    {
        public Vector2 halfSize = new Vector2(14, 14);
    }
}
