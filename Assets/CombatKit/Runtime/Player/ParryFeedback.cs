using UnityEngine;
using Unity.Cinemachine;
using CombatSystem.Data.Player;
namespace CombatSystem.Player
{
    /// <summary>
    /// 패리·저스트 회피 성공 순간에 시간을 느리게 하고 카메라를 잠깐 줌인/줌아웃했다가 원상 복구하는 연출 컴포넌트.
    /// timeScale을 직접 바꾸므로 비활성화/파괴 시 반드시 원래 값으로 되돌린다.
    /// 두 연출이 timeScale 복구 기준값을 서로 덮어쓰지 않도록 한 컴포넌트가 함께 담당한다.
    /// </summary>
    public class ParryFeedback : MonoBehaviour
    {
        #region 필드

        [SerializeField] private Camera targetCamera;
        [SerializeField] private ParryFeedbackConfig config;
        [SerializeField] private ParryFeedbackConfig perfectDodgeConfig;
        /// <summary>현재 재생 중인 연출의 설정(패리 또는 저스트 회피).</summary>
        private ParryFeedbackConfig current;
        private Camera activeCamera;
        private CinemachineCamera activeVirtualCamera;
        private float originalVirtualZoom;
        private float elapsed, originalScale, originalFixedStep, originalZoom;
        private bool active, orthographic;

        #endregion

        #region 공개 API

        /// <summary>설정 에셋이 비어 있으면 경고를 남기고 기본값 인스턴스로 대체한다.</summary>
        private void Awake()
        {
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<ParryFeedbackConfig>();
                Debug.LogWarning("ParryFeedback: no ParryFeedbackConfig assigned, using built-in defaults.", this);
            }
            if (perfectDodgeConfig == null)
            {
                // 저스트 회피 기본값: 패리보다 약하고 길게 느려지며, 줌인 대신 줌아웃한다.
                perfectDodgeConfig = ScriptableObject.CreateInstance<ParryFeedbackConfig>();
                perfectDodgeConfig.slowMotionScale = 0.3f;
                perfectDodgeConfig.zoomInDuration = 0.1f;
                perfectDodgeConfig.holdDuration = 0.45f;
                perfectDodgeConfig.recoveryDuration = 0.4f;
                perfectDodgeConfig.zoomMultiplier = 1.25f;
                Debug.LogWarning("ParryFeedback: no perfect dodge config assigned, using built-in defaults.", this);
            }
        }

        /// <summary>패리 성공 연출(강한 슬로 모션 + 줌인)을 시작한다.</summary>
        public void Play()
        {
            Play(config);
        }

        /// <summary>
        /// 저스트 회피 연출(슬로 모션 + 줌아웃)을 시작한다.
        /// 다른 시스템(보스 투사체 패리 창 등)이 이미 timeScale을 낮춰 둔 상태라면, 그 느려진 값을 원래 값으로
        /// 잘못 저장해 시간이 느린 채로 남을 수 있으므로 재생하지 않는다.
        /// </summary>
        /// <returns>연출을 재생했으면 true.</returns>
        public bool PlayPerfectDodge()
        {
            if (!active && Time.timeScale < .99f) return false;
            return Play(perfectDodgeConfig);
        }

        /// <summary>
        /// 지정한 설정으로 연출을 시작한다. 첫 호출 때만 원래 timeScale/줌 값을 저장하고,
        /// 연출 중 다시 호출되면 복구 기준값은 유지한 채 타이머만 처음부터 다시 돌린다.
        /// </summary>
        private bool Play(ParryFeedbackConfig profile)
        {
            if (!isActiveAndEnabled || Time.timeScale <= 0f) return false;
            if (!active)
            {
                activeCamera = targetCamera != null ? targetCamera : Camera.main;
                originalScale = Time.timeScale;
                originalFixedStep = Time.fixedDeltaTime;
                if (activeCamera != null)
                {
                    orthographic = activeCamera.orthographic;
                    originalZoom = orthographic ? activeCamera.orthographicSize : activeCamera.fieldOfView;
                    var brain = activeCamera.GetComponent<CinemachineBrain>();
                    activeVirtualCamera = brain != null ? brain.ActiveVirtualCamera as CinemachineCamera : null;
                    if (activeVirtualCamera != null)
                        originalVirtualZoom = orthographic ? activeVirtualCamera.Lens.OrthographicSize : activeVirtualCamera.Lens.FieldOfView;
                }
                active = true;
            }
            // 연속 패리는 복구용 원래 값을 덮어쓰지 않고 연출만 처음부터 다시 시작한다.
            current = profile;
            elapsed = 0f;
            Time.timeScale = originalScale * current.slowMotionScale;
            Time.fixedDeltaTime = originalFixedStep * current.slowMotionScale;
            return true;
        }

        #endregion

        #region 연출 진행과 복구

        /// <summary>실제 시간 기준으로 줌 진입 → 유지 → 복귀 구간을 진행하며 시간 배율과 카메라 줌을 보간한다.</summary>
        private void Update()
        {
            if (!active) return;
            elapsed += Time.unscaledDeltaTime;
            float enter = Mathf.Max(0.01f, current.zoomInDuration);
            float recovery = Mathf.Max(0.01f, current.recoveryDuration);
            float endHold = enter + current.holdDuration;
            float zoomWeight = elapsed < enter ? Mathf.SmoothStep(0f, 1f, elapsed / enter) :
                1f - Mathf.SmoothStep(0f, 1f, (elapsed - endHold) / recovery);
            float restore = Mathf.SmoothStep(0f, 1f, (elapsed - endHold) / recovery);
            Time.timeScale = originalScale * Mathf.Lerp(current.slowMotionScale, 1f, restore);
            Time.fixedDeltaTime = originalFixedStep * Mathf.Lerp(current.slowMotionScale, 1f, restore);
            if (activeCamera != null)
            {
                float zoom = Mathf.Lerp(originalZoom, originalZoom * current.zoomMultiplier, zoomWeight);
                if (orthographic) activeCamera.orthographicSize = zoom;
                else activeCamera.fieldOfView = zoom;
            }
            // Cinemachine 렌즈도 함께 바꿔야 Brain이 카메라 줌을 덮어쓰지 못한다.
            if (activeVirtualCamera != null)
            {
                float zoom = Mathf.Lerp(originalVirtualZoom, originalVirtualZoom * current.zoomMultiplier, zoomWeight);
                if (orthographic) activeVirtualCamera.Lens.OrthographicSize = zoom;
                else activeVirtualCamera.Lens.FieldOfView = zoom;
            }
            if (elapsed >= endHold + recovery) Restore();
        }
        /// <summary>저장해 둔 timeScale, fixedDeltaTime, 카메라 줌을 원래대로 되돌리고 연출을 종료한다.</summary>
        private void Restore()
        {
            if (!active) return;
            Time.timeScale = originalScale;
            Time.fixedDeltaTime = originalFixedStep;
            if (activeCamera != null)
            {
                if (orthographic) activeCamera.orthographicSize = originalZoom;
                else activeCamera.fieldOfView = originalZoom;
            }
            if (activeVirtualCamera != null)
            {
                if (orthographic) activeVirtualCamera.Lens.OrthographicSize = originalVirtualZoom;
                else activeVirtualCamera.Lens.FieldOfView = originalVirtualZoom;
            }
            activeVirtualCamera = null;
            active = false;
        }
        /// <summary>연출 도중 꺼져도 느려진 시간이 남지 않도록 복구한다.</summary>
        private void OnDisable() { Restore(); }
        /// <summary>연출 도중 파괴되어도 느려진 시간이 남지 않도록 복구한다.</summary>
        private void OnDestroy() { Restore(); }

        #endregion
    }
}
