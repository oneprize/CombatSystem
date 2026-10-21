using UnityEngine;
using CombatSystem.Data.Boss;
namespace CombatSystem.Boss
{
    /// <summary>
    /// 점프 착지(JumpEnd) 애니메이션 중 발이 바닥 아래로 파고들지 않도록 IK 단계에서 몸통 높이만 올려 보정한다.
    /// 게임플레이 루트 위치나 클립 커브는 건드리지 않는다.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Animator))]
    public sealed class BossLandingGrounding : MonoBehaviour
    {
        [SerializeField] private BossLandingGroundingConfig config;
        private Animator animator;
        private Transform leftFoot, rightFoot, leftToes, rightToes;
        /// <summary>마지막 IK 패스에서 몸통을 들어 올린 높이. 디버그 모니터 표시용.</summary>
        public float LastCorrection { get; private set; }
        /// <summary>설정이 없으면 기본값으로 대체하고, 휴머노이드일 때 발/발가락 본을 캐시한다.</summary>
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
        /// <summary>착지 애니메이션 중 가장 낮은 발 지점이 바닥 기준선보다 아래면 그만큼 몸통 위치를 올린다.</summary>
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
            // 이동 상태가 보스 피벗을 착지 평면에 맞춰 둔다.
            // 여기서는 애니메이션된 몸통만 보정하고, 게임플레이 루트를 옮기거나 클립 커브를 수정하지 않는다.
            float floor=transform.position.y+config.soleClearance*Mathf.Abs(transform.lossyScale.y);
            LastCorrection=Mathf.Max(0,floor-lowest);
            animator.bodyPosition+=Vector3.up*LastCorrection;
        }
    }
}