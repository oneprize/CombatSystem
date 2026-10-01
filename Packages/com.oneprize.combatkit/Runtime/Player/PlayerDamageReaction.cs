using UnityEngine;

namespace ParrySystem.Player
{
    [RequireComponent(typeof(Animator), typeof(CharacterController))]
    public class PlayerDamageReaction : MonoBehaviour
    {
        // Connect player HP here later. Blocked/parried hits never raise this event.
        public event System.Action<int> DamageReceived;
        public enum HitResult { None, Blocked, Parried, Damaged, Evaded }
        public HitResult LastResult { get; private set; }
        public bool IsReacting { get; private set; }
        public int LastComboIndex { get; private set; } = -1;
        private Animator animator;
        private PlayerCombat combat;
        private string stateName;
        private bool entered;

        private bool bossDown, awaitingStandUp;
        public bool IsKnockedDown => bossDown;
        public bool IsAwaitingStandUp => awaitingStandUp;
        public void PlayProjectileParry()
        {
            if(combat==null || bossDown)return;
            combat.CancelForDamage();
            stateName="Base Layer.ProjectileParry";
            IsReacting=true; entered=false;
            animator.CrossFadeInFixedTime(stateName,.05f,0,0);
        }
        private void ReadStandUpInput()
        {
            Vector3 input=new Vector3(Input.GetAxisRaw("Horizontal"),0,Input.GetAxisRaw("Vertical"));
            if(input.sqrMagnitude<.01f)return;
            var controller=GetComponent<PlayerController>();
            Transform basis=controller!=null && controller.cam!=null?controller.cam:transform;
            Vector3 forward=basis.forward;forward.y=0;forward.Normalize();
            Vector3 right=basis.right;right.y=0;right.Normalize();
            TryStandUp(forward*input.z+right*input.x);
        }
        public bool TryStandUp(Vector3 worldDirection)
        {
            if(!awaitingStandUp || worldDirection.sqrMagnitude<.01f)return false;
            worldDirection.y=0;worldDirection.Normalize();
            Vector3 local=transform.InverseTransformDirection(worldDirection);
            bool backward=local.z<0 && Mathf.Abs(local.z)>=Mathf.Abs(local.x);
            transform.rotation=Quaternion.LookRotation(backward?-worldDirection:worldDirection);
            stateName=backward?"Base Layer.BossStandUpBack":"Base Layer.BossStandUpFront";
            awaitingStandUp=false; entered=false;
            animator.CrossFadeInFixedTime(stateName,.1f,0,0);
            return true;
        }


        private void Awake()
        {
            animator = GetComponent<Animator>();
            combat = GetComponent<PlayerCombat>();
        }

        public bool ReceiveHit(int comboIndex, Transform attacker, bool allowParry = true, bool knockDown = false)
        {
            if (comboIndex < 0 || comboIndex > 3 || combat == null)
                return false;
            // A dodged contact is consumed, so the same swing cannot hit again after recovery.
            if (combat.IsDodging)
            {
                LastResult = HitResult.Evaded;
                return true;
            }
            var enemy = attacker != null ? attacker.GetComponent<ParrySystem.Combat.EnemyCombat>() : null;
            LastComboIndex = comboIndex;
            if (allowParry && combat.TryConsumeParry())
            {
                LastResult = HitResult.Parried;
                if (enemy != null) enemy.RegisterParry();
                var boss = attacker != null ? attacker.GetComponentInParent<ParrySystem.Boss.BossCombat>() : null;
                if (boss != null) boss.RegisterParry();
                var feedback = GetComponent<ParryFeedback>();
                if (feedback != null) feedback.Play();
                Debug.Log($"[Parry Success] 패링 성공 | Enemy: {(attacker != null ? attacker.name : "Unknown")} | Combo: {comboIndex + 1}", this);
                return true;
            }
            if (combat.IsGuarding)
            {
                LastResult = HitResult.Blocked;
                combat.PlayGuardAccept();
                return true;
            }
            LastResult = HitResult.Damaged;
            DamageReceived?.Invoke(comboIndex);
            if(bossDown)return true;
            // Only an ongoing player attack resists interruption by weak hits.
            if (comboIndex < 3 && combat.isAttacking) return true;
            bossDown=knockDown; awaitingStandUp=false;
            stateName = knockDown ? "Base Layer.BossKnockDown" : "Base Layer.Damage" + (comboIndex + 1);
            if (!animator.HasState(0, Animator.StringToHash(stateName))) return false;
            combat.CancelForDamage();
            Vector3 towardAttacker = attacker.position - transform.position;
            towardAttacker.y = 0f;
            if (towardAttacker.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(towardAttacker);
            LastComboIndex = comboIndex;
            IsReacting = true;
            entered = false;
            animator.CrossFadeInFixedTime(stateName, 0.05f, 0, 0f);
            return true;
        }

        private void Update()
        {
            if (!IsReacting) return;
            if(awaitingStandUp){ReadStandUpInput();return;}
            var current = animator.GetCurrentAnimatorStateInfo(0);
            var next = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : default(AnimatorStateInfo);
            bool transitioningToHit = next.IsName(stateName);
            if (transitioningToHit) entered = true;
            if (current.IsName(stateName) && !transitioningToHit)
            {
                entered = true;
                if (current.normalizedTime >= 1f)
                {
                    if(bossDown && stateName=="Base Layer.BossKnockDown"){awaitingStandUp=true;return;}
                    bossDown=false;
                    IsReacting = false;
                    animator.CrossFadeInFixedTime("Base Layer.Idle", 0.1f);
                }
            }
            else if (entered && !transitioningToHit)
            {
                IsReacting = false;
            }
        }

        private void OnDisable() { IsReacting = false; bossDown=false; awaitingStandUp=false; }
    }
}
