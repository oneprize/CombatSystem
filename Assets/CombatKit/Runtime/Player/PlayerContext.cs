using UnityEngine;

namespace CombatSystem.Player
{
    /// <summary>
    /// 모든 플레이어 모듈이 공유하는 참조 묶음. 모듈끼리는 GetComponent 대신
    /// <see cref="Character"/>를 통해 서로에게 접근한다.
    /// </summary>
    public sealed class PlayerContext
    {
        public PlayerCharacter Character { get; }
        public Transform Transform { get; }
        public Animator Animator { get; }
        public CharacterController Controller { get; }

        /// <summary>캐릭터에서 트랜스폼, 애니메이터, CharacterController를 한 번만 찾아 캐시한다.</summary>
        public PlayerContext(PlayerCharacter character)
        {
            Character = character;
            Transform = character.transform;
            Animator = character.GetComponent<Animator>();
            Controller = character.GetComponent<CharacterController>();
        }
    }
}
