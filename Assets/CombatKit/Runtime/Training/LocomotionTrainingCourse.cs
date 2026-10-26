using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using CombatSystem.Player;

namespace CombatSystem.Training
{
    /// <summary>이동 훈련의 완료 복귀와 추락 시 재시작을 담당한다.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class LocomotionTrainingCourse : MonoBehaviour
    {
        public PlayerCharacter player;
        public BoxCollider finishArea;
        [SerializeField] private TrainingScenePortal returnPortal;
        [SerializeField, Min(0f)] private float completionMessageDuration = 1.2f;
        public float resetBelowY = -5f;
        public float startZ = 3f;
        public bool Completed { get; private set; }
        public float Elapsed { get; private set; }
        private bool started, restarting;

        private IEnumerator Start()
        {
            // 플레이어의 Start(Idle) 이후 기존 납도 동작을 사용한다. 철봉은 양손이 비어 있어야 잡는다.
            yield return null;
            if (player != null && player.WeaponStance != null && player.WeaponStance.IsDrawn)
                player.WeaponStance.StartTransition(false);
        }

        private void Update()
        {
            if (restarting || player == null || (returnPortal != null && returnPortal.IsLoading)) return;
            var keyboard = Keyboard.current;
            if (player.transform.position.y < resetBelowY || player.IsDead ||
                (keyboard != null && keyboard.backspaceKey.wasPressedThisFrame))
            {
                RestartCourse();
                return;
            }
            if (!started && player.transform.position.z > startZ + .5f) started = true;
            if (started && !Completed) Elapsed += Time.deltaTime;
            if (!Completed && finishArea != null && finishArea.bounds.Contains(player.transform.position))
            {
                Completed = true;
                if (returnPortal != null)
                    restarting = returnPortal.BeginTravel(player, "훈련 완료!  콤보 훈련장으로 복귀합니다.", completionMessageDuration);
                else Debug.LogError("LocomotionTrainingCourse: return altar is not assigned.", this);
            }
        }

        /// <summary>씬을 다시 로드해 점프 횟수, 매달림, 슬라이드, 카메라까지 시작 상태로 복구한다.</summary>
        public void RestartCourse()
        {
            if (restarting || (returnPortal != null && returnPortal.IsLoading)) return;
            restarting = true;
            Time.timeScale = 1f;
            var scene = gameObject.scene;
#if UNITY_EDITOR
            if (scene.buildIndex < 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path,
                    new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            SceneManager.LoadScene(scene.buildIndex);
        }
    }
}
