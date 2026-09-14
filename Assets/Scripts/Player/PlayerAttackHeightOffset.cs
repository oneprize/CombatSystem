using UnityEngine;
namespace ParrySystem.Player
{
    [RequireComponent(typeof(Animator))]
    public class PlayerAttackHeightOffset : MonoBehaviour
    {
        [Tooltip("Combo1 원본의 발 기준과 플레이어 몸 중심 기준 사이 높이 차이(m)")]
        [SerializeField] private float heightOffset = 0.95f;
        [SerializeField] private float combo2HeightOffset = 0.95f;
        [SerializeField] private float combo3HeightOffset = 0.19f;
        [Tooltip("플레이어 외형 전체를 아래로 내리는 높이(m)")]
        [SerializeField, Min(0f)] private float visualLowering = 0.12f;
        [SerializeField, Min(0f)] private float guardHeightOffset = 0.23f;
        [SerializeField, Min(0f)] private float guardAcceptHeightOffset = 0.25f;
        private float GetOffset(AnimatorStateInfo state)
        {
            if (state.IsName("Base Layer.Attack")) return heightOffset;
            if (state.IsName("Base Layer.Attack2")) return combo2HeightOffset;
            if (state.IsName("Base Layer.Attack3")) return combo3HeightOffset;
            if (state.IsName("Base Layer.Guard")) return guardHeightOffset;
            if (state.IsName("Base Layer.GuardAccept")) return guardAcceptHeightOffset;
            return 0f;
        }
        private Animator animator;
        private void Awake() { animator = GetComponent<Animator>(); }
        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || !animator.isHuman) return;
            var currentState=animator.GetCurrentAnimatorStateInfo(0);
            var nextState=animator.IsInTransition(0)?animator.GetNextAnimatorStateInfo(0):default(AnimatorStateInfo);
            bool groundedReaction=currentState.IsName("BossKnockDown") || currentState.IsName("BossStandUpFront") || currentState.IsName("BossStandUpBack") ||
                nextState.IsName("BossKnockDown") || nextState.IsName("BossStandUpFront") || nextState.IsName("BossStandUpBack");
            if(groundedReaction)
            {
                var character=GetComponent<CharacterController>();
                if(character!=null)
                {
                    float lowest=float.PositiveInfinity;
                    foreach(var bone in new[]{HumanBodyBones.Hips,HumanBodyBones.Head,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,
                        HumanBodyBones.LeftToes,HumanBodyBones.RightToes,HumanBodyBones.LeftHand,HumanBodyBones.RightHand})
                    {
                        var joint=animator.GetBoneTransform(bone);
                        if(joint!=null)lowest=Mathf.Min(lowest,joint.position.y);
                    }
                    if(!float.IsPositiveInfinity(lowest))
                        animator.bodyPosition+=Vector3.up*(character.bounds.min.y+.04f-lowest);
                    return;
                }
            }
            float weight = GetOffset(animator.GetCurrentAnimatorStateInfo(0));
            if (animator.IsInTransition(0))
            {
                float next = GetOffset(animator.GetNextAnimatorStateInfo(0));
                weight = Mathf.Lerp(weight, next, Mathf.Clamp01(animator.GetAnimatorTransitionInfo(0).normalizedTime));
            }
            weight += visualLowering;
            // 관절 회전이나 클립 곡선은 바꾸지 않고, 평가된 몸 전체의 높이만 평행 이동한다.
            if (weight > 0f)
                animator.bodyPosition -= transform.up * (transform.lossyScale.y * weight);
        }
    }
}