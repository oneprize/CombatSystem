using UnityEngine;
using CombatSystem.Player;
using CombatSystem.Data.Enemy;

namespace CombatSystem.Combat
{
    /// <summary>
    /// 적 무기에 붙는 히트박스. 공격 상태에서 콤보 단계별 활성 구간 동안 무기 박스와 플레이어의 겹침을 검사해 피격을 전달하고,
    /// 오프닝 점프 공격은 별도의 구 범위 판정으로 처리한다. 한 번의 공격에서 같은 플레이어는 한 번만 맞는다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class EnemyWeaponHitbox : MonoBehaviour
    {
        #region 필드

        [SerializeField] private EnemyWeaponHitboxConfig config;
        /// <summary>효과음도 실제 무기 판정과 같은 애니메이션 구간을 사용한다.</summary>
        public EnemyWeaponHitboxConfig Config => config;
        private Animator animator;
        private EnemyStateMachine machine;
        private EnemyCombat combat;
        private BoxCollider hitbox;
        /// <summary>마지막으로 판정한 공격 시퀀스 번호와 콤보 단계(-2는 오프닝 공격). 바뀌면 중복 타격 목록을 비운다.</summary>
        private int lastSequence = -1;
        private int lastCombo = -1;
        /// <summary>현재 공격 한 번에서 이미 맞은 플레이어 목록(중복 타격 방지).</summary>
        private readonly System.Collections.Generic.HashSet<PlayerCharacter> hitPlayers =
            new System.Collections.Generic.HashSet<PlayerCharacter>();

        #endregion

        #region Unity 생명주기

        /// <summary>config가 비어 있으면 기본값으로 대체하고, 부모 계층에서 애니메이터·상태 머신·EnemyCombat 참조를 찾는다.</summary>
        private void Awake()
        {
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<EnemyWeaponHitboxConfig>();
                Debug.LogWarning("EnemyWeaponHitbox: no EnemyWeaponHitboxConfig assigned, using built-in defaults.", this);
            }

            animator = GetComponentInParent<Animator>();
            machine = GetComponentInParent<EnemyStateMachine>();
            combat = GetComponentInParent<EnemyCombat>();
            hitbox = GetComponent<BoxCollider>();
        }

        #endregion

        #region 히트 판정

        /// <summary>애니메이터 상태가 몇 번째 콤보 단계의 공격 상태인지 찾는다. 콤보 상태가 아니면 -1을 돌려준다.</summary>
        private int FindCombo(AnimatorStateInfo state)
        {
            for (int i = 0; i < combat.ComboStepCount; i++)
                if (state.IsName("Base Layer." + combat.GetComboStep(i).animStateName)) return i;
            return -1;
        }

        /// <summary>
        /// 오프닝 점프 공격의 히트 구간 동안 적 전방 구 범위 안의 플레이어를 찾아 강공격으로 피격시킨다.
        /// 사이에 다른 물체가 가로막으면 맞지 않는다.
        /// </summary>
        private void CheckOpeningHit()
        {
            AnimatorStateInfo state;
            if (!machine.AttackState.TryGetOpeningProgress(out state)) return;
            Vector2 window = combat.OpeningHitWindow;
            if (state.normalizedTime < window.x || state.normalizedTime > window.y) return;
            int sequence = machine.AttackState.AttackSequence;
            if (lastSequence != sequence || lastCombo != -2)
            {
                hitPlayers.Clear();
                lastSequence = sequence;
                lastCombo = -2;
            }
            Vector3 center = machine.transform.position + Vector3.up + machine.transform.forward * .8f;
            foreach (var contact in Physics.OverlapSphere(center, combat.OpeningHitRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!(contact is CharacterController)) continue;
                var player = contact.GetComponent<PlayerCharacter>();
                if (player == null || hitPlayers.Contains(player)) continue;
                bool blocked = false;
                Vector3 destination = player.transform.position + Vector3.up;
                foreach (var obstruction in Physics.RaycastAll(center, destination - center,
                    Vector3.Distance(center, destination), ~0, QueryTriggerInteraction.Ignore))
                    if (!obstruction.transform.IsChildOf(machine.transform) &&
                        !obstruction.transform.IsChildOf(player.transform)) { blocked = true; break; }
                if (!blocked && player.Health.ReceiveHit(3, machine.transform)) hitPlayers.Add(player);
            }
        }

        /// <summary>
        /// 애니메이션 평가가 끝난 뒤 무기 위치 기준으로 판정하기 위해 LateUpdate에서 실행한다.
        /// 현재(전환 중이면 다음) 콤보 상태의 활성 구간 안이면 무기 박스와 겹친 플레이어에게 피격을 전달한다.
        /// </summary>
        private void LateUpdate()
        {
            if (machine == null || machine.CurrentState != machine.AttackState ||
                !hitbox.enabled) return;
            if (machine.AttackState.IsOpeningAttack)
            {
                CheckOpeningHit();
                return;
            }
            int sequence = machine.AttackState.AttackSequence;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            int combo = FindCombo(state);
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                int nextCombo = FindCombo(next);
                if (nextCombo >= 0) { state = next; combo = nextCombo; }
            }
            if (combo < 0) { return; }
            Vector2 window = combo == 0 ? config.combo1HitWindow : combo == 1 ? config.combo2HitWindow :
                combo == 2 ? config.combo3HitWindow : config.combo4HitWindow;
            bool active = state.normalizedTime >= window.x && state.normalizedTime <= window.y;
            if (!active) return;
            if (sequence != lastSequence || combo != lastCombo)
            {
                hitPlayers.Clear();
                lastSequence = sequence;
                lastCombo = combo;
            }
            // 애니메이션 평가 후 검의 실제 박스와 플레이어 CharacterController의 겹침을 검사한다.
            Vector3 scale = transform.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            var contacts = Physics.OverlapBox(transform.TransformPoint(hitbox.center),
                Vector3.Scale(hitbox.size * 0.5f, scale), transform.rotation,
                ~0, QueryTriggerInteraction.Ignore);
            foreach (var contact in contacts)
            {
                if (machine.CurrentState != machine.AttackState) break;
                if (!(contact is CharacterController)) continue;
                var player = contact.GetComponent<PlayerCharacter>();
                if (player != null && !hitPlayers.Contains(player) &&
                    player.Health.ReceiveHit(combo, machine.transform))
                    hitPlayers.Add(player);
            }
        }

        #endregion
    }
}
