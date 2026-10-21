using UnityEngine;
namespace CombatSystem.Data.Training
{
    /// <summary>콤보 피드백 UI 설정 에셋.</summary>
    [CreateAssetMenu(menuName = "CombatKit/Training/Combo Feedback Config", fileName = "ComboFeedbackConfig")]
    public class ComboFeedbackConfig : ScriptableObject
    {
        /// <summary>마지막 적중 후 이 시간(초) 동안 추가 적중이 없으면 콤보 카운트를 초기화한다.</summary>
        [Min(.5f)] public float comboTimeout = 3f;
    }
}
