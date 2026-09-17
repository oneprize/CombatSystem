using UnityEngine;
namespace ParrySystem.Player
{
    [RequireComponent(typeof(CharacterController), typeof(Animator))]
    public sealed class PlayerAirCombo : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float fallGravity = 18f;
        [SerializeField, Min(.1f)] private float maximumHoldTime = 10f;
        public bool Active { get; private set; }
        public bool Recovering => Active && falling;
        private bool falling, landing;
        private float previousUpperHeight;
        // Foot-height trajectory sampled from the original UpperAttack at 30 fps.
        // Standalone humanoid clip Bake flags do not reliably survive asset reloads.
        private static readonly float[] UpperHeights = { 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.85341f, 1.11443f, 1.16875f, 1.22002f, 1.26748f, 1.30045f, 1.32557f, 1.38839f, 1.50463f, 1.60975f, 1.66568f, 1.71637f, 1.76262f, 1.80183f, 1.83267f, 1.85791f, 1.88061f, 1.90397f, 1.93004f, 1.83954f, 1.66474f, 1.45540f, 1.23242f, 0.66809f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f, 0.00000f };
        public void BeginUpperMotion() { previousUpperHeight = 0; }
        public void ApplyUpperMotion(Vector3 rootDelta)
        {
            if (!character.enabled) return;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("UpperAttack"))
                state = animator.GetNextAnimatorStateInfo(0);
            if (!state.IsName("UpperAttack")) return;
            float sample = Mathf.Clamp01(state.normalizedTime) * (UpperHeights.Length - 1);
            int index = Mathf.Min(Mathf.FloorToInt(sample), UpperHeights.Length - 2);
            float height = Mathf.Lerp(UpperHeights[index], UpperHeights[index + 1], sample - index);
            rootDelta.y = height - previousUpperHeight;
            previousUpperHeight = height;
            character.Move(rootDelta);
        }
        private float verticalSpeed, heldTime;
        private CharacterController character;
        private Animator animator;
        private PlayerCombat combat;
        private void Awake()
        {
            character=GetComponent<CharacterController>(); animator=GetComponent<Animator>();
            combat=GetComponent<PlayerCombat>();
        }
        public bool CanBegin()
        {
            if (Active || !character.enabled || character.isGrounded) return false;
            var bottom=character.bounds.center-Vector3.up*character.bounds.extents.y;
            return !Physics.Raycast(bottom+Vector3.up*.05f,Vector3.down,.2f,~0,QueryTriggerInteraction.Ignore);
        }
        public void Begin()
        {
            Active=true; falling=landing=false; verticalSpeed=heldTime=0;
        }
        public void Fall()
        {
            if(!Active || falling)return;
            falling=true;verticalSpeed=0;
            if(!combat.IsHitReacting)animator.CrossFadeInFixedTime("Base Layer.LinkJumpLoop",.1f,0,0);
        }
        public void Tick(float dt)
        {
            if(!Active)return;
            if(!falling)
            {
                heldTime+=dt;
                if(!combat.IsUsingAirCombo || heldTime>=maximumHoldTime)combat.EndAirCombo();
                return;
            }
            if(landing)
            {
                if(combat.IsHitReacting){Active=false;return;}
                if(!animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).IsName("LinkJumpEnd") &&
                    animator.GetCurrentAnimatorStateInfo(0).normalizedTime>=1)
                { Active=false;animator.CrossFadeInFixedTime("Base Layer.Idle",.1f); }
                return;
            }
            float remaining=dt;
            while(remaining>.00001f)
            {
                float step=Mathf.Min(.02f,remaining);remaining-=step;
                verticalSpeed-=fallGravity*step;
                var collision=character.Move(Vector3.up*verticalSpeed*step);
                if((collision & CollisionFlags.Below)!=0)
                {
                    verticalSpeed=0;landing=true;
                    if(combat.IsHitReacting)Active=false;
                    else animator.CrossFadeInFixedTime("Base Layer.LinkJumpEnd",.1f,0,0);
                    break;
                }
            }
        }
        private void OnDisable(){Active=falling=landing=false;}
    }
}
