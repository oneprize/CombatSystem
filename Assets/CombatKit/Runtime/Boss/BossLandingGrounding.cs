using UnityEngine;
using CombatSystem.Data.Boss;
namespace CombatSystem.Boss
{
    [DisallowMultipleComponent, RequireComponent(typeof(Animator))]
    public sealed class BossLandingGrounding : MonoBehaviour
    {
        [SerializeField] private BossLandingGroundingConfig config;
        private Animator animator;
        private Transform leftFoot, rightFoot, leftToes, rightToes;
        public float LastCorrection { get; private set; }
        private void Awake()
        {
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BossLandingGroundingConfig>();
                Debug.LogWarning("BossLandingGrounding: no BossLandingGroundingConfig assigned, using built-in defaults.", this);
            }
            animator=GetComponent<Animator>();
            if(!animator.isHuman) return;
            leftFoot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot=animator.GetBoneTransform(HumanBodyBones.RightFoot);
            leftToes=animator.GetBoneTransform(HumanBodyBones.LeftToes);
            rightToes=animator.GetBoneTransform(HumanBodyBones.RightToes);
        }
        private void OnAnimatorIK(int layerIndex)
        {
            LastCorrection=0;
            if(layerIndex!=0 || !animator.isHuman || leftFoot==null || rightFoot==null) return;
            var current=animator.GetCurrentAnimatorStateInfo(0);
            bool landing=current.IsName("JumpEnd");
            if(animator.IsInTransition(0)) landing |= animator.GetNextAnimatorStateInfo(0).IsName("JumpEnd");
            if(!landing)return;
            float lowest=Mathf.Min(leftFoot.position.y,rightFoot.position.y);
            if(leftToes!=null)lowest=Mathf.Min(lowest,leftToes.position.y);
            if(rightToes!=null)lowest=Mathf.Min(lowest,rightToes.position.y);
            // The movement state places the boss pivot on the landing plane.
            // Correct only the animated body; never move the gameplay root or edit clip curves.
            float floor=transform.position.y+config.soleClearance*Mathf.Abs(transform.lossyScale.y);
            LastCorrection=Mathf.Max(0,floor-lowest);
            animator.bodyPosition+=Vector3.up*LastCorrection;
        }
    }
}