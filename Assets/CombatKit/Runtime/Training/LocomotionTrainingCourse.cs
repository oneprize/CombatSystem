using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using CombatSystem.Player;

namespace CombatSystem.Training
{
    /// <summary>이 훈련 씬의 도착 판정, 추락 시 재시작만 담당한다.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class LocomotionTrainingCourse : MonoBehaviour
    {
        public PlayerCharacter player;
        public BoxCollider finishArea;
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
            if (restarting || player == null) return;
            var keyboard = Keyboard.current;
            if (player.transform.position.y < resetBelowY || player.IsDead ||
                (keyboard != null && keyboard.backspaceKey.wasPressedThisFrame))
            {
                RestartCourse();
                return;
            }
            if (!started && player.transform.position.z > startZ + .5f) started = true;
            if (started && !Completed) Elapsed += Time.deltaTime;
            if (finishArea != null && finishArea.bounds.Contains(player.transform.position)) Completed = true;
        }

        /// <summary>씬을 다시 로드해 점프 횟수, 매달림, 슬라이드, 카메라까지 시작 상태로 복구한다.</summary>
        public void RestartCourse()
        {
            if (restarting) return;
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
