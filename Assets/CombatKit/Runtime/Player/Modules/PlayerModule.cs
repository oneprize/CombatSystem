using UnityEngine;

namespace CombatSystem.Player
{
    /// <summary>
    /// 플레이어 기능 모듈의 기반 클래스. 컴포넌트가 아닌 일반 C# 객체로, PlayerCharacter가
    /// 생성·수명 관리를 맡고 정해진 순서대로 호출한다.
    /// </summary>
    public abstract class PlayerModule
    {
        /// <summary>모듈이 공유하는 플레이어 참조(캐릭터, Transform, 애니메이터, CharacterController) 묶음.</summary>
        protected PlayerContext Ctx { get; private set; }
        protected PlayerCharacter Player => Ctx.Character;
        // 모듈 코드가 원래 MonoBehaviour 멤버를 기준으로 작성되었으므로 같은 이름으로 노출한다.
        protected Transform transform => Ctx.Transform;
        protected Animator animator => Ctx.Animator;
        protected CharacterController character => Ctx.Controller;

        /// <summary>PlayerCharacter가 공용 컨텍스트를 주입하고 모듈별 초기화를 실행하게 하는 진입점.</summary>
        internal void Bind(PlayerContext context)
        {
            Ctx = context;
            OnInitialize();
        }

        /// <summary>PlayerCharacter.Awake에서 모든 모듈이 생성된 뒤 한 번 호출되어 설정값·참조를 준비한다.</summary>
        protected virtual void OnInitialize() { }

        /// <summary>PlayerCharacter가 비활성화될 때 호출되며, 진행 중인 동작을 정리해 중단한다.</summary>
        public virtual void OnDisable() { }
    }
}
