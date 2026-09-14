using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// AniTest 씬 전용: 키 하나에 애니메이션 하나씩 매핑해서 눌러보며 확인하는 테스트 드라이버.
/// 카테고리 이름(Idle/Walk/Combo3 등)이 Player(Katana)와 Enemy(Big_Sword) 컨트롤러에
/// 공통으로 존재하면 두 캐릭터가 동시에 재생되고, 한쪽에만 있는 파라미터(Combo5, SkillJ~L 등)는
/// 그 파라미터를 가진 애니메이터에서만 재생된다.
/// </summary>
public class AnimationShowcaseDriver : MonoBehaviour
{
    [System.Serializable]
    public struct Binding
    {
        public KeyCode key;
        public string triggerName;
    }

    public Animator playerAnimator;
    [FormerlySerializedAs("monsterAnimator")]
    public Animator enemyAnimator;

    private static readonly Binding[] Bindings = new Binding[]
    {
        new Binding { key = KeyCode.Alpha1, triggerName = "Idle" },
        new Binding { key = KeyCode.Alpha2, triggerName = "Walk" },
        new Binding { key = KeyCode.Alpha3, triggerName = "Jog" },
        new Binding { key = KeyCode.Alpha4, triggerName = "Run" },
        new Binding { key = KeyCode.Alpha5, triggerName = "RunInjured" },
        new Binding { key = KeyCode.Alpha6, triggerName = "Jump" },
        new Binding { key = KeyCode.Alpha7, triggerName = "DoubleJump" },
        new Binding { key = KeyCode.Alpha8, triggerName = "Dodge" },
        new Binding { key = KeyCode.Alpha9, triggerName = "Crouch" },
        new Binding { key = KeyCode.Alpha0, triggerName = "CrouchMove" },

        new Binding { key = KeyCode.Q, triggerName = "IdleToCrouch" },
        new Binding { key = KeyCode.W, triggerName = "WalkToCrouch" },
        new Binding { key = KeyCode.E, triggerName = "IdleToMove" },
        new Binding { key = KeyCode.R, triggerName = "TurnLeft" },
        new Binding { key = KeyCode.T, triggerName = "TurnRight" },
        new Binding { key = KeyCode.Y, triggerName = "Combo3" },
        new Binding { key = KeyCode.U, triggerName = "Combo4" },
        new Binding { key = KeyCode.I, triggerName = "Combo5" },
        new Binding { key = KeyCode.O, triggerName = "Combo7" },
        new Binding { key = KeyCode.P, triggerName = "DashAttack" },

        new Binding { key = KeyCode.A, triggerName = "JumpAttack" },
        new Binding { key = KeyCode.S, triggerName = "UpperAttack" },
        new Binding { key = KeyCode.D, triggerName = "SkillA" },
        new Binding { key = KeyCode.F, triggerName = "SkillB" },
        new Binding { key = KeyCode.G, triggerName = "SkillC" },
        new Binding { key = KeyCode.H, triggerName = "SkillD" },
        new Binding { key = KeyCode.J, triggerName = "SkillE" },
        new Binding { key = KeyCode.K, triggerName = "SkillF" },
        new Binding { key = KeyCode.L, triggerName = "SkillG" },

        new Binding { key = KeyCode.Z, triggerName = "SkillH" },
        new Binding { key = KeyCode.X, triggerName = "SkillI" },
        new Binding { key = KeyCode.C, triggerName = "SkillJ" },
        new Binding { key = KeyCode.V, triggerName = "SkillK" },
        new Binding { key = KeyCode.B, triggerName = "SkillL" },
        new Binding { key = KeyCode.N, triggerName = "DamageFront" },
        new Binding { key = KeyCode.M, triggerName = "DamageBack" },

        new Binding { key = KeyCode.Comma, triggerName = "DamageLeft" },
        new Binding { key = KeyCode.Period, triggerName = "DamageRight" },
        new Binding { key = KeyCode.Slash, triggerName = "KnockDown" },
        new Binding { key = KeyCode.Semicolon, triggerName = "Die" },
        new Binding { key = KeyCode.Quote, triggerName = "GuardStart" },
        new Binding { key = KeyCode.LeftBracket, triggerName = "GuardLoop" },
        new Binding { key = KeyCode.RightBracket, triggerName = "GuardEnd" },
        new Binding { key = KeyCode.Minus, triggerName = "GuardAccept" },
        new Binding { key = KeyCode.Equals, triggerName = "GuardAttack" },
        new Binding { key = KeyCode.BackQuote, triggerName = "StandUp" },
    };

    private void Start()
    {
        Debug.Log("[AnimationShowcase] ===== 키 매핑 목록 (" + Bindings.Length + "개) =====");
        foreach (var b in Bindings)
        {
            Debug.Log("[AnimationShowcase] " + b.key + " -> " + b.triggerName);
        }
    }

    private void Update()
    {
        foreach (var b in Bindings)
        {
            if (Input.GetKeyDown(b.key))
            {
                bool playerFired = Fire(playerAnimator, b.triggerName);
                bool enemyFired = Fire(enemyAnimator, b.triggerName);
                Debug.Log("[AnimationShowcase] " + b.key + " -> " + b.triggerName
                    + " (player=" + playerFired + ", enemy=" + enemyFired + ")");
            }
        }
    }

    private bool Fire(Animator animator, string triggerName)
    {
        if (animator == null)
        {
            return false;
        }

        foreach (var p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == triggerName)
            {
                animator.SetTrigger(triggerName);
                return true;
            }
        }

        return false;
    }
}
