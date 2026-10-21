using UnityEngine;
namespace CombatSystem.Player
{
    /// <summary>
    /// 이미 직렬화된 씬/프리팹의 컴포넌트 참조가 깨지지 않도록 남겨 둔 빈 호환용 껍데기.
    /// 타격으로 채우던 파워 게이지 UI는 더 이상 쓰지 않는다.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class PlayerPowerGaugeUI : MonoBehaviour { }
}
