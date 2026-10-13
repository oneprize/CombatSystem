using UnityEngine;
namespace CombatSystem.Data.Training
{
    [CreateAssetMenu(menuName = "CombatKit/Training/Combo Feedback Config", fileName = "ComboFeedbackConfig")]
    public class ComboFeedbackConfig : ScriptableObject
    {
        [Min(.5f)] public float comboTimeout = 3f;
    }
}
