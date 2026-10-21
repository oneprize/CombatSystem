using UnityEngine;
using Unity.Cinemachine;
using CombatSystem.Data.Player;
namespace CombatSystem.Player
{
    /// <summary>
    /// 패리 성공 순간에 시간을 느리게 하고 카메라를 잠깐 줌인했다가 원상 복구하는 연출 컴포넌트.
    /// timeScale을 직접 바꾸므로 비활성화/파괴 시 반드시 원래 값으로 되돌린다.
    /// </summary>
    public class ParryFeedback : MonoBehaviour
    {
        #region 필드

        [SerializeField] private Camera targetCamera;
        [SerializeField] private ParryFeedbackConfig config;
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
        }

        /// <summary>
        /// 패리 연출을 시작한다. 첫 호출 때만 원래 timeScale/줌 값을 저장하고,
        /// 연출 중 다시 호출되면 복구 기준값은 유지한 채 타이머만 처음부터 다시 돌린다.
        /// </summary>
        public void Play()
        {
            if (!isActiveAndEnabled || Time.timeScale <= 0f) return;
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
            elapsed = 0f;
            Time.timeScale = originalScale * config.slowMotionScale;
            Time.fixedDeltaTime = originalFixedStep * config.slowMotionScale;
        }

        #endregion

        #region 연출 진행과 복구

        /// <summary>실제 시간 기준으로 줌인 → 유지 → 복귀 구간을 진행하며 시간 배율과 카메라 줌을 보간한다.</summary>
        private void Update()
        {
            if (!active) return;
            elapsed += Time.unscaledDeltaTime;
            float enter = Mathf.Max(0.01f, config.zoomInDuration);
            float recovery = Mathf.Max(0.01f, config.recoveryDuration);
            float endHold = enter + config.holdDuration;
            float zoomWeight = elapsed < enter ? Mathf.SmoothStep(0f, 1f, elapsed / enter) :
                1f - Mathf.SmoothStep(0f, 1f, (elapsed - endHold) / recovery);
            float restore = Mathf.SmoothStep(0f, 1f, (elapsed - endHold) / recovery);
            Time.timeScale = originalScale * Mathf.Lerp(config.slowMotionScale, 1f, restore);
            Time.fixedDeltaTime = originalFixedStep * Mathf.Lerp(config.slowMotionScale, 1f, restore);
            if (activeCamera != null)
            {
                float zoom = Mathf.Lerp(originalZoom, originalZoom * config.zoomMultiplier, zoomWeight);
                if (orthographic) activeCamera.orthographicSize = zoom;
                else activeCamera.fieldOfView = zoom;
            }
            // Cinemachine 렌즈도 함께 바꿔야 Brain이 카메라 줌을 덮어쓰지 못한다.
            if (activeVirtualCamera != null)
            {
                float zoom = Mathf.Lerp(originalVirtualZoom, originalVirtualZoom * config.zoomMultiplier, zoomWeight);
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
