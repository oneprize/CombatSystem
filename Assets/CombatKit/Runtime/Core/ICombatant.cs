using UnityEngine;

namespace CombatSystem.Core
{
    /// <summary>
    /// 스폰된 액터(플레이어, 적, 보스)의 최상위 전투 컴포넌트가 구현하는 인터페이스다.
    /// <see cref="CombatRegistry"/>에 등록되어, 씬에서 직접 연결한 참조 없이도 다른 액터가 찾을 수 있게 한다.
    /// </summary>
    public interface ICombatant
    {
        Transform Transform { get; }
        CombatTeam Team { get; }
    }
}
