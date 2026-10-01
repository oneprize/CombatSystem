using UnityEngine;
using UnityEngine.UI;
using CombatSystem.Combat;
using CombatSystem.Core;
using System.Text;

namespace CombatSystem.UI
{
    public class LabUI : MonoBehaviour
    {
        [SerializeField] private EnemyStateMachine target;
        private void EnsureTargetLinked()
        {
            if (target != null) return;
            var nearest = CombatRegistry.FindNearest(CombatTeam.Enemy, transform.position);
            if (nearest != null) target = nearest.Transform.GetComponent<EnemyStateMachine>();
        }
        private Canvas canvas;
        private Text enemyName, state, conditions, logs, hpText, duration;
        private Image hpFill, groggyFill;
        private Text groggyText;
        private float refreshAt;

        private Text Label(Transform parent, string name, float y, float height, int size, Color color)
        {
            var text = LabUIKit.Clip(LabUIKit.Label(parent, name, "", size, color));
            LabUIKit.TopLeft(text.rectTransform, 20, -y, 360, height);
            // Rasterize at higher resolution while preserving the layout size.
            const int rasterScale = 3;
            text.fontSize = size * rasterScale;
            text.rectTransform.sizeDelta *= rasterScale;
            text.rectTransform.localScale = Vector3.one / rasterScale;
            if (color == LabUIKit.TextDim) text.color = new Color(0.76f, 0.81f, 0.87f);
            return text;
        }
        private void Awake()
        {
            EnsureTargetLinked();
            var root = new GameObject("AI Monitor Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.pixelPerfect = true;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var panel = LabUIKit.Panel(root.transform, "AI MONITOR", LabUIKit.PanelBg);
            LabUIKit.TopRight(panel, 18, -18, 400, 780);
            Label(panel, "Title", 16, 36, LabUIKit.FsHead, LabUIKit.Accent).text = "AI MONITOR";
            enemyName = Label(panel, "Enemy", 55, 32, LabUIKit.FsBody, LabUIKit.TextMain);
            hpFill = LabUIKit.Bar(panel, "Health", 20, -96, 360, 12, LabUIKit.PanelEdge, LabUIKit.Good);
            hpText = Label(panel, "HP", 112, 28, LabUIKit.FsSmall, LabUIKit.TextDim);
            Label(panel, "State Heading", 150, 28, LabUIKit.FsSmall, LabUIKit.TextDim).text = "CURRENT STATE";
            state = Label(panel, "State", 178, 40, LabUIKit.FsTitle, LabUIKit.TextMain);
            duration = Label(panel, "Duration", 219, 28, LabUIKit.FsSmall, LabUIKit.TextDim);
            LabUIKit.HLine(panel, 20, -258, 360, LabUIKit.PanelEdge, 1);
            Label(panel, "Conditions Heading", 272, 32, LabUIKit.FsHead, LabUIKit.TextMain).text = "CONDITION";
            conditions = Label(panel, "Conditions", 310, 205, LabUIKit.FsSmall, LabUIKit.TextMain);
            groggyFill = LabUIKit.Bar(panel, "Groggy", 20, -526, 360, 10, LabUIKit.PanelEdge, LabUIKit.AccentWarm);
            groggyText = Label(panel, "Groggy Value", 540, 28, LabUIKit.FsSmall, LabUIKit.TextMain);
            Label(panel, "Log Heading", 577, 32, LabUIKit.FsBody, LabUIKit.AccentWarm).text = "ENTER / EXECUTE / EXIT";
            logs = Label(panel, "Logs", 617, 148, LabUIKit.FsSmall, LabUIKit.TextDim);
        }
        private void OnEnable() { if (canvas != null) canvas.gameObject.SetActive(true); }
        private void OnDisable() { if (canvas != null) canvas.gameObject.SetActive(false); }
        private static string Flag(bool value) => value ? "<color=#66D980>TRUE</color>" : "<color=#949FAC>FALSE</color>";
        private void Update()
        {
            if (Time.unscaledTime < refreshAt) return;
            refreshAt = Time.unscaledTime + 0.1f;
            EnsureTargetLinked();
            if (target == null)
            {
                enemyName.text = "No Enemy assigned";
                state.text = "-"; duration.text = ""; conditions.text = ""; logs.text = "";
                hpFill.fillAmount = 0; hpText.text = "HP —";
                return;
            }
            var combat = target.GetComponent<EnemyCombat>();
            var health = target.GetComponent<EnemyDamageReaction>();
            enemyName.text = target.name;
            state.text = EnemyStateMachine.StateName(target.CurrentState);
            state.color = LabUIKit.LabelColor(state.text);
            duration.text = string.Format("{0:0.0}s   Previous: {1}", Time.time - target.StateEnteredAt, target.PreviousStateName);
            hpFill.fillAmount = health != null ? health.CurrentHealth / health.MaxHealth : 0;
            hpFill.color = hpFill.fillAmount <= .3f ? LabUIKit.Bad : LabUIKit.Good;
            hpText.text = health != null ? string.Format("HP  {0:0} / {1:0}   ({2:0}%)", health.CurrentHealth, health.MaxHealth, hpFill.fillAmount * 100) : "HP —";
            if (combat != null)
            {
                groggyFill.fillAmount = combat.GroggyGauge / combat.MaxGroggyGauge;
                groggyText.text = string.Format("GROGGY  {0:0} / {1:0}", combat.GroggyGauge, combat.MaxGroggyGauge);
                bool hasPlayer = combat.PlayerTransform != null;
                Vector3 delta = hasPlayer ? combat.PlayerTransform.position - target.transform.position : Vector3.zero;
                delta.y = 0f;
                float distance = hasPlayer ? delta.magnitude : float.PositiveInfinity;
                conditions.text = string.Format(
                    "Player distance     {0}\nDetected (≤ {1:0.0}m)     {2}\nAttack range (≤ {3:0.0}m)     {4}\nObserve distance     {5:0.0} – {6:0.0}m\nPlayer guarding     {7}\nPlayer attacking     {8}\nParry window     {9}\nLow HP (≤ 30%)     {10}",
                    hasPlayer ? distance.ToString("0.00") + "m" : "N/A",
                    combat.DetectionRange, Flag(hasPlayer && distance <= combat.DetectionRange),
                    combat.AttackRange, Flag(hasPlayer && distance <= combat.AttackRange),
                    combat.CombatMinDistance, combat.CombatMaxDistance,
                    Flag(combat.IsPlayerGuarding), Flag(combat.IsPlayerAttacking),
                    Flag(combat.IsParryWindowOpen), Flag(health != null && hpFill.fillAmount <= .3f));
            }
            var sb = new StringBuilder();
            var entries = target.MonitorLog;
            for (int i = Mathf.Max(0, entries.Count - 6); i < entries.Count; i++)
            {
                var entry = entries[i];
                sb.AppendFormat("{0:0.0}  {1}  {2}", entry.Time, entry.State, entry.Phase);
                if (entry.Phase == "EXECUTE") sb.Append(" × ").Append(entry.Count);
                sb.AppendLine();
            }
            logs.text = sb.ToString();
        }
    }
}
