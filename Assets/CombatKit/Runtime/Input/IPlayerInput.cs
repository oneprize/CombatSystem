using UnityEngine;

namespace CombatSystem.Player
{
    /// <summary>
    /// 플레이어 모듈들이 읽는 조작 입력 전체를 정의하는 인터페이스. PlayerCharacter는 자기 GameObject에서
    /// 구현체를 찾으므로, 게임은 <see cref="PlayerInputReader"/> 대신 자체 입력 계층(또는 AI/컷신)으로 캐릭터를 조종할 수 있다.
    /// "Pressed" 멤버는 버튼이 눌린 그 프레임에만 true이고, "Held" 멤버는 누르고 있는 동안 계속 true이다.
    /// </summary>
    public interface IPlayerInput
    {
        /// <summary>이동 입력. x = 오른쪽, y = 앞. 키보드는 축마다 -1/0/1을 주며 정규화하지 않는다.</summary>
        Vector2 Move { get; }
        bool RunHeld { get; }
        bool JumpPressed { get; }
        bool DodgePressed { get; }
        /// <summary>기본 공격 입력.</summary>
        bool AttackPressed { get; }
        /// <summary>공격 버튼을 계속 누르고 있음(파워 공격 차지에 사용).</summary>
        bool AttackHeld { get; }
        bool GuardHeld { get; }
        bool SkillGPressed { get; }
        bool SkillAPressed { get; }
        bool SkillBPressed { get; }
        bool UpperAttackPressed { get; }
        /// <summary>공격과 함께 누르고 있으면 Skill C가 발동되는 보조 키.</summary>
        bool SkillCModifierHeld { get; }
        /// <summary>락온을 켜고 끈다.</summary>
        bool LockOnPressed { get; }
        /// <summary>락온 대상을 플레이어 기준 시계 방향의 다음 적으로 바꾼다.</summary>
        bool LockOnNextPressed { get; }
    }

    /// <summary>아무 입력도 내지 않는 구현. 캐릭터에 IPlayerInput 컴포넌트가 없을 때 대신 쓰인다.</summary>
    public sealed class NullPlayerInput : IPlayerInput
    {
        public static readonly NullPlayerInput Instance = new NullPlayerInput();
        public Vector2 Move => Vector2.zero;
        public bool RunHeld => false;
        public bool JumpPressed => false;
        public bool DodgePressed => false;
        public bool AttackPressed => false;
        public bool AttackHeld => false;
        public bool GuardHeld => false;
        public bool SkillGPressed => false;
        public bool SkillAPressed => false;
        public bool SkillBPressed => false;
        public bool UpperAttackPressed => false;
        public bool SkillCModifierHeld => false;
        public bool LockOnPressed => false;
        public bool LockOnNextPressed => false;
    }
}
