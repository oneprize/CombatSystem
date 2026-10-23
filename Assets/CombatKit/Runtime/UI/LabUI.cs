using UnityEngine;
using UnityEngine.UI;
using CombatSystem.Combat;
using CombatSystem.Core;
using System.Text;

namespace CombatSystem.UI
{
    /// <summary>
    /// 일반 적 AI의 체력, 현재 상태와 지속 시간, 판단 조건(탐지/공격/관찰 거리, 플레이어 가드·공격 여부), 상태 전이 로그를
    /// 화면 우측 패널로 보여 주는 디버그 모니터. 패널은 런타임에 코드로 생성되며 0.1초 간격으로 갱신된다.
    /// </summary>
    public class LabUI : MonoBehaviour
    {
        #region 필드와 대상 연결

        [SerializeField] private EnemyStateMachine target;
        /// <summary>대상 적이 지정되지 않았으면 가장 가까운 적 팀 전투원을 찾아 연결한다.</summary>
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

        #endregion

        #region UI 구성

        /// <summary>패널 좌측 상단 기준 y 위치에 텍스트 라벨을 만든다. 글자가 선명하도록 3배 해상도로 그린 뒤 축소한다.</summary>
        private Text Label(Transform parent, string name, float y, float height, int size, Color color)
        {
            var text = LabUIKit.Clip(LabUIKit.Label(parent, name, "", size, color));
            LabUIKit.TopLeft(text.rectTransform, 20, -y, 360, height);
            // 레이아웃 크기는 유지한 채 더 높은 해상도로 래스터화해 작은 글자가 뭉개지지 않게 한다.
            const int rasterScale = 3;
            text.fontSize = size * rasterScale;
            text.rectTransform.sizeDelta *= rasterScale;
            text.rectTransform.localScale = Vector3.one / rasterScale;
            if (color == LabUIKit.TextDim) text.color = new Color(0.76f, 0.81f, 0.87f);
            return text;
        }

        #endregion

        #region Unity 생명주기

        /// <summary>대상 적을 연결하고, 오버레이 캔버스와 모니터 패널의 모든 라벨·체력 바를 생성한다.</summary>
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
        /// <summary>컴포넌트가 켜지면 모니터 캔버스도 함께 표시한다.</summary>
        private void OnEnable() { if (canvas != null) canvas.gameObject.SetActive(true); }
        /// <summary>컴포넌트가 꺼지면 모니터 캔버스도 숨긴다. 대결 구역이 이 방식으로 모니터를 켜고 끈다.</summary>
        private void OnDisable() { if (canvas != null) canvas.gameObject.SetActive(false); }

        #endregion

        #region 표시 갱신

        /// <summary>조건 값을 색이 입혀진 TRUE/FALSE 리치 텍스트로 변환한다.</summary>
        private static string Flag(bool value) => value ? "<color=#66D980>TRUE</color>" : "<color=#949FAC>FALSE</color>";
        /// <summary>0.1초마다 대상 적의 상태·체력·판단 조건·최근 로그 6줄을 패널에 반영한다.</summary>
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
                groggyFill.fillAmount = 0; groggyText.text = "GROGGY —";
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
            groggyFill.fillAmount = combat != null ? combat.GroggyNormalized : 0;
            groggyText.text = combat == null ? "GROGGY —" : combat.IsLargeGroggy
                ? string.Format("BIG GROGGY  {0:0.0}s", combat.GroggyRemaining)
                : string.Format("GROGGY  {0} / {1}  ({2:0}%)", combat.ParryCount, combat.ParriesForLargeGroggy, combat.GroggyNormalized * 100);
            if (combat != null)
            {
                bool hasPlayer = combat.PlayerTransform != null;
                Vector3 delta = hasPlayer ? combat.PlayerTransform.position - target.transform.position : Vector3.zero;
                delta.y = 0f;
                float distance = hasPlayer ? delta.magnitude : float.PositiveInfinity;
                conditions.text = string.Format(
                    "Player distance     {0}\nDetected (≤ {1:0.0}m)     {2}\nAttack range (≤ {3:0.0}m)     {4}\nObserve distance     {5:0.0} – {6:0.0}m\nPlayer guarding     {7}\nPlayer attacking     {8}\nLow HP (≤ 30%)     {9}",
                    hasPlayer ? distance.ToString("0.00") + "m" : "N/A",
                    combat.DetectionRange, Flag(hasPlayer && distance <= combat.DetectionRange),
                    combat.AttackRange, Flag(hasPlayer && distance <= combat.AttackRange),
                    combat.CombatMinDistance, combat.CombatMaxDistance,
                    Flag(combat.IsPlayerGuarding), Flag(combat.IsPlayerAttacking),
                    Flag(health != null && hpFill.fillAmount <= .3f));
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

        #endregion
    }
}
