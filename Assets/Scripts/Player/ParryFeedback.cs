using UnityEngine;
namespace ParrySystem.Player
{
    public class ParryFeedback : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField, Range(0.05f, 1f)] private float slowMotionScale = 0.2f;
        [SerializeField, Min(0.01f)] private float zoomInDuration = 0.08f;
        [SerializeField, Min(0f)] private float holdDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float recoveryDuration = 0.25f;
        [SerializeField, Range(0.3f, 1f)] private float zoomMultiplier = 0.85f;
        private Camera activeCamera;
        private float elapsed, originalScale, originalFixedStep, originalZoom;
        private bool active, orthographic;

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
                }
                active = true;
            }
            // Repeated parries restart the effect without overwriting the restore values.
            elapsed = 0f;
            Time.timeScale = originalScale * slowMotionScale;
            Time.fixedDeltaTime = originalFixedStep * slowMotionScale;
        }
        private void LateUpdate()
        {
            if (!active) return;
            elapsed += Time.unscaledDeltaTime;
            float enter = Mathf.Max(0.01f, zoomInDuration);
            float recovery = Mathf.Max(0.01f, recoveryDuration);
            float endHold = enter + holdDuration;
            float zoomWeight = elapsed < enter ? Mathf.SmoothStep(0f, 1f, elapsed / enter) :
                1f - Mathf.SmoothStep(0f, 1f, (elapsed - endHold) / recovery);
            float restore = Mathf.SmoothStep(0f, 1f, (elapsed - endHold) / recovery);
            Time.timeScale = originalScale * Mathf.Lerp(slowMotionScale, 1f, restore);
            Time.fixedDeltaTime = originalFixedStep * Mathf.Lerp(slowMotionScale, 1f, restore);
            if (activeCamera != null)
            {
                float zoom = Mathf.Lerp(originalZoom, originalZoom * zoomMultiplier, zoomWeight);
                if (orthographic) activeCamera.orthographicSize = zoom;
                else activeCamera.fieldOfView = zoom;
            }
            if (elapsed >= endHold + recovery) Restore();
        }
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
            active = false;
        }
        private void OnDisable() { Restore(); }
        private void OnDestroy() { Restore(); }
    }
}