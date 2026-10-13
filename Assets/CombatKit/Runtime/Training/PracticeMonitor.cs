using UnityEngine;
using UnityEngine.InputSystem;
using CombatSystem.Core;
using CombatSystem.Player;
namespace CombatSystem.Training
{
    public sealed class PracticeMonitor : MonoBehaviour
    {
        [SerializeField] private PlayerCharacter player;
        [SerializeField] private PracticeDummy dummy;
        [SerializeField] private PlayerWeaponHitbox hitbox;
        private GUIStyle heading, text;
        private void Awake() { EnsurePlayerLinked(); }
        private void EnsurePlayerLinked()
        {
            if (player != null) return;
            var nearest = CombatRegistry.FindNearest(CombatTeam.Player, transform.position);
            if (nearest != null) player = nearest.Transform.GetComponent<PlayerCharacter>();
        }
        private void Update()
        {
            EnsurePlayerLinked();
            if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame && dummy != null)
                dummy.ResetDummy();
        }
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
                + "\n\nLMB: combo 1-5 / W + LMB: Skill C\nC then LMB: upper + air combo 1-6\nQ / E / R: skills   Ctrl: dodge\nF5: reset dummy and counters";
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
