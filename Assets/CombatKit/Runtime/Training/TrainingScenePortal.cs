using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CombatSystem.Core;
using CombatSystem.Player;
using CombatSystem.UI;

namespace CombatSystem.Training
{
    /// <summary>제단 근처에서 F로 씬을 이동한다. 코스 완료 시에는 거리 검사 없이 같은 전환을 사용한다.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-200)]
    public sealed class TrainingScenePortal : MonoBehaviour
    {
        [SerializeField] private string destinationScene = "Assets/CombatKit/Content/Scenes/LocomotionTraining.unity";
        [SerializeField] private string destinationLabel = "이동 훈련장";
        [SerializeField] private string actionLabel = "입장";
        [SerializeField] private string portalId = "ComboLocomotionAltar";
        [SerializeField] private string destinationPortalId;
        [SerializeField] private Transform arrivalPoint;
        [SerializeField, Min(.5f)] private float interactionRadius = 2.6f;
        [SerializeField, Min(.2f)] private float maximumHeightDifference = 2f;
        [SerializeField, Min(0f)] private float fadeDuration = .2f;
        private PlayerCharacter player;
        private PlayerInputReader input;
        private Canvas canvas;
        private CanvasGroup prompt;
        private Image fade;
        private Text promptText;
        private float nextPlayerLookup;
        private bool awaitingKeyRelease = true;
        private static UnityAction<Scene, LoadSceneMode> pendingArrival;
        public bool IsLoading { get; private set; }

        public bool CanInteract
        {
            get
            {
                if (IsLoading || Time.timeScale <= 0f || player == null || !player.isActiveAndEnabled || player.IsDead) return false;
                Vector3 delta = player.transform.position - transform.position;
                if (Mathf.Abs(delta.y) > maximumHeightDifference) return false;
                delta.y = 0f;
                return delta.sqrMagnitude <= interactionRadius * interactionRadius;
            }
        }

        private void Awake()
        {
            var go = new GameObject("Altar Interaction Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            canvas.pixelPerfect = true;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;

            var panel = new GameObject("Enter Training Prompt", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            panel.transform.SetParent(go.transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
            rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 100f);
            rect.sizeDelta = new Vector2(440f, 64f);
            var bg = panel.GetComponent<Image>();
            bg.color = new Color(.035f, .075f, .11f, .94f);
            bg.raycastTarget = false;
            prompt = panel.GetComponent<CanvasGroup>();
            prompt.alpha = 0f;
            prompt.interactable = prompt.blocksRaycasts = false;
            var text = new GameObject("Interaction Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            promptText = text;
            text.transform.SetParent(panel.transform, false);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.font = LabUIKit.Font;
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(.8f, .95f, 1f);
            text.text = "[" + InteractionInput.DisplayKey + "]  " + destinationLabel + " " + actionLabel;
            text.raycastTarget = false;

            fade = new GameObject("Scene Fade", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            fade.transform.SetParent(go.transform, false);
            fade.rectTransform.anchorMin = Vector2.zero;
            fade.rectTransform.anchorMax = Vector2.one;
            fade.rectTransform.offsetMin = fade.rectTransform.offsetMax = Vector2.zero;
            fade.color = Color.clear;
            fade.raycastTarget = false;
        }

        private void Update()
        {
            if (IsLoading) return;
            // 씬을 넘어오면서 아직 누르고 있는 F로 곧바로 반대편 제단을 재사용하지 않는다.
            if (!InteractionInput.Held) awaitingKeyRelease = false;
            if (Time.unscaledTime >= nextPlayerLookup)
            {
                nextPlayerLookup = Time.unscaledTime + .25f;
                var nearest = CombatRegistry.FindNearest(CombatTeam.Player, transform.position);
                player = nearest != null ? nearest.Transform.GetComponent<PlayerCharacter>() : null;
            }
            bool available = CanInteract;
            prompt.alpha = available ? 1f : 0f;
            if (available && !awaitingKeyRelease && InteractionInput.Pressed) TryEnter();
        }

        public bool TryEnter()
        {
            return CanInteract && BeginTravel(player);
        }

        /// <summary>자동 클리어 복귀도 이 메서드를 사용한다. 거리나 공격/이동 상태로 제한하지 않는다.</summary>
        public bool BeginTravel(PlayerCharacter traveller, string message = null, float messageDuration = 0f)
        {
            if (IsLoading || traveller == null || !traveller.isActiveAndEnabled || traveller.IsDead) return false;
            if (SceneUtility.GetBuildIndexByScenePath(destinationScene) < 0 || !Application.CanStreamedLevelBeLoaded(destinationScene))
            {
                Debug.LogError("Training altar destination must be enabled in Build Settings: " + destinationScene, this);
                return false;
            }
            player = traveller;
            input = player.GetComponent<PlayerInputReader>();
            IsLoading = true;
            prompt.alpha = string.IsNullOrEmpty(message) ? 0f : 1f;
            if (!string.IsNullOrEmpty(message)) promptText.text = message;
            // 씬을 읽는 동안 캐릭터가 이동하거나 다른 동작을 시작하지 않게 한다.
            bool inputWasEnabled = input != null && input.enabled;
            if (inputWasEnabled) input.enabled = false;
            player.enabled = false;
            Time.timeScale = 1f;
            StartCoroutine(EnterScene(inputWasEnabled, Mathf.Max(0f, messageDuration)));
            return true;
        }

        private IEnumerator EnterScene(bool inputWasEnabled, float messageDuration)
        {
            if (messageDuration > 0f) yield return new WaitForSecondsRealtime(messageDuration);
            prompt.alpha = 0f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(elapsed / fadeDuration));
                yield return null;
            }
            fade.color = Color.black;
            PrepareArrival(destinationScene, destinationPortalId);
            AsyncOperation loading = null;
            try { loading = SceneManager.LoadSceneAsync(destinationScene, LoadSceneMode.Single); }
            catch (System.Exception exception) { Debug.LogException(exception, this); }
            if (loading != null) yield return loading;
            else
            {
                ClearPendingArrival();
                IsLoading = false;
                fade.color = Color.clear;
                if (player != null) player.enabled = true;
                if (input != null && inputWasEnabled) input.enabled = true;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearPendingArrival()
        {
            if (pendingArrival != null) SceneManager.sceneLoaded -= pendingArrival;
            pendingArrival = null;
        }

        private static void PrepareArrival(string scenePath, string targetId)
        {
            ClearPendingArrival();
            if (string.IsNullOrEmpty(targetId)) return; // 목적지 씬에 배치된 기본 시작점을 사용한다.
            pendingArrival = (scene, mode) =>
            {
                if (scene.path != scenePath) return;
                ClearPendingArrival();
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var portal in root.GetComponentsInChildren<TrainingScenePortal>(true))
                    {
                        if (portal.portalId != targetId || portal.arrivalPoint == null) continue;
                        // sceneLoaded는 Start 이전이므로 새 카메라도 복귀 위치를 기준으로 초기화된다.
                        foreach (var playerRoot in scene.GetRootGameObjects())
                        {
                            var arrived = playerRoot.GetComponentInChildren<PlayerCharacter>();
                            if (arrived == null || !arrived.isActiveAndEnabled) continue;
                            var controller = arrived.GetComponent<CharacterController>();
                            bool enabledBefore = controller != null && controller.enabled;
                            if (enabledBefore) controller.enabled = false;
                            arrived.transform.SetPositionAndRotation(portal.arrivalPoint.position, portal.arrivalPoint.rotation);
                            if (enabledBefore) controller.enabled = true;
                            Physics.SyncTransforms();
                            return;
                        }
                    }
                Debug.LogWarning("Training return point not found: " + targetId + "; using scene spawn.");
            };
            SceneManager.sceneLoaded += pendingArrival;
        }

        private void OnEnable() { if (canvas != null) canvas.gameObject.SetActive(true); }
        private void OnDisable() { if (canvas != null) canvas.gameObject.SetActive(false); }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.1f, .85f, 1f, .7f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * .5f, interactionRadius);
        }
    }
}
