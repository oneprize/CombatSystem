using UnityEngine;
using UnityEngine.InputSystem;
using CombatSystem.Core;
using CombatSystem.Player;
namespace CombatSystem.Training
{
    /// <summary>
    /// 허수아비 연습 구역용 IMGUI 모니터. 플레이어의 현재 공격 상태와 히트 판정 창, 허수아비 적중 통계와 공중 여부를 표시하고,
    /// 적중 직후 허수아비 머리 위에 피해 수치를 띄운다. F5로 허수아비와 통계를 초기화한다.
    /// </summary>
    public sealed class PracticeMonitor : MonoBehaviour
    {
        [SerializeField] private PlayerCharacter player;
        [SerializeField] private PracticeDummy dummy;
        [SerializeField] private PlayerWeaponHitbox hitbox;
        private GUIStyle heading, text;
        /// <summary>시작 시 플레이어 참조를 연결해 둔다.</summary>
        private void Awake() { EnsurePlayerLinked(); }
        /// <summary>플레이어가 지정되지 않았으면 가장 가까운 플레이어 팀 전투원을 찾아 연결한다.</summary>
        private void EnsurePlayerLinked()
        {
            if (player != null) return;
            var nearest = CombatRegistry.FindNearest(CombatTeam.Player, transform.position);
            if (nearest != null) player = nearest.Transform.GetComponent<PlayerCharacter>();
        }
        /// <summary>F5 입력으로 허수아비와 적중 통계를 초기화한다.</summary>
        private void Update()
        {
            EnsurePlayerLinked();
            if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame && dummy != null)
                dummy.ResetDummy();
        }
        /// <summary>화면 높이에 비례해 크기를 맞춘 우측 상단 패널에 연습 정보를 그리고, 최근 0.8초 안의 피해 수치를 허수아비 위에 표시한다.</summary>
        private void OnGUI()
        {
            EnsurePlayerLinked();
            if (player == null || dummy == null || hitbox == null) return;
            if (heading == null)
            {
                heading = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
                heading.normal.textColor = new Color(.45f, .9f, 1);
                text = new GUIStyle(GUI.skin.label) { fontSize = 17 };
                text.normal.textColor = Color.white;
            }
            float scale = Mathf.Clamp(Screen.height / 900f, .7f, 1.6f);
            var saved = GUI.matrix; GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float x = Screen.width / scale - 395;
            GUI.Box(new Rect(x, 20, 375, 320), GUIContent.none);
            GUI.Label(new Rect(x+18, 32, 340, 35), "M1 / COMBO PRACTICE", heading);
            string attack = player.IsAttacking ? player.Combat.CurrentAttackStateName : "Idle";
            string rows = "Attack: " + attack + "\nHit window: " + (hitbox.HitWindowActive ? "ACTIVE" : "closed")
                + "\nTotal hits: " + dummy.TotalHits
                + "\nLast damage: " + Mathf.FloorToInt(dummy.LastDamage) + "  /  Total: " + Mathf.FloorToInt(dummy.TotalDamage)
                + "\nDummy: " + (dummy.GetComponent<CombatSystem.Combat.EnemyDamageReaction>().IsAirborneForCombo ? "AIRBORNE" : "GROUND")
                + "\n\nQ / R: skills   E: parry   Ctrl: dodge\nF5: reset dummy and counters";
            GUI.Label(new Rect(x+18, 78, 345, 250), rows, text);
            if (Time.time - dummy.LastHitTime < .8f && Camera.main != null)
            {
                Vector3 point = Camera.main.WorldToScreenPoint(dummy.transform.position + Vector3.up * 2.4f);
                if (point.z > 0) GUI.Label(new Rect(point.x/scale, (Screen.height-point.y)/scale, 130, 40), "-" + Mathf.FloorToInt(dummy.LastDamage), heading);
            }
            GUI.matrix = saved;
        }
    }
}
