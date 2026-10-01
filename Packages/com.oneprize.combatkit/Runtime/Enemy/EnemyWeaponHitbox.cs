using UnityEngine;
using ParrySystem.Player;

namespace ParrySystem.Combat
{
    [RequireComponent(typeof(BoxCollider))]
    public class EnemyWeaponHitbox : MonoBehaviour
    {
        [SerializeField] private Vector2 combo1HitWindow = new Vector2(0.38f, 0.52f);
        [SerializeField] private Vector2 combo2HitWindow = new Vector2(0.18f, 0.30f);
        [SerializeField] private Vector2 combo3HitWindow = new Vector2(0.03f, 0.16f);
        [SerializeField] private Vector2 combo4HitWindow = new Vector2(0.20f, 0.58f);
        private Animator animator;
        private EnemyStateMachine machine;
        private EnemyCombat combat;
        private BoxCollider hitbox;
        private int lastSequence = -1;
        private int lastCombo = -1;
        private readonly System.Collections.Generic.HashSet<PlayerDamageReaction> hitPlayers =
            new System.Collections.Generic.HashSet<PlayerDamageReaction>();

        private void Awake()
        {
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
            if (combo < 0) { combat.SetParryWindowOpen(false); return; }
            Vector2 window = combo == 0 ? combo1HitWindow : combo == 1 ? combo2HitWindow :
                combo == 2 ? combo3HitWindow : combo4HitWindow;
            bool active = state.normalizedTime >= window.x && state.normalizedTime <= window.y;
            combat.SetParryWindowOpen(active);
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
                var player = contact.GetComponent<PlayerDamageReaction>();
                if (player != null && !hitPlayers.Contains(player) &&
                    player.ReceiveHit(combo, machine.transform))
                    hitPlayers.Add(player);
            }
        }
    }
}
