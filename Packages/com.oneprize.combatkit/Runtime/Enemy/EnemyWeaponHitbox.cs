using UnityEngine;
using CombatSystem.Player;
using CombatSystem.Data.Enemy;

namespace CombatSystem.Combat
{
    [RequireComponent(typeof(BoxCollider))]
    public class EnemyWeaponHitbox : MonoBehaviour
    {
        [SerializeField] private EnemyWeaponHitboxConfig config;
        private Animator animator;
        private EnemyStateMachine machine;
        private EnemyCombat combat;
        private BoxCollider hitbox;
        private int lastSequence = -1;
        private int lastCombo = -1;
        private readonly System.Collections.Generic.HashSet<PlayerCharacter> hitPlayers =
            new System.Collections.Generic.HashSet<PlayerCharacter>();

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

        private int FindCombo(AnimatorStateInfo state)
        {
            for (int i = 0; i < combat.ComboStepCount; i++)
                if (state.IsName("Base Layer." + combat.GetComboStep(i).animStateName)) return i;
            return -1;
        }

        private void LateUpdate()
        {
            if (machine == null || machine.CurrentState != machine.AttackState ||
                !hitbox.enabled) return;
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
    }
}
