using UnityEngine;
using UnityEngine.UI;
using CombatSystem.UI;
namespace CombatSystem.Player
{
    /// <summary>
    /// 화면 왼쪽 위에 플레이어 이름, HP 바, 현재 상태를 보여 주는 디버그용 상태 패널.
    /// 실행 시 코드로 Canvas를 직접 생성하므로 별도 UI 프리팹이 필요 없다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCharacter))]
    public sealed class PlayerStatusUI : MonoBehaviour
    {
        #region 필드

        private PlayerCharacter player;
        private Canvas canvas;
        private Text nameLabel, hpLabel, stateLabel;
        private Image fill;

        #endregion

        #region UI 구성

        /// <summary>
        /// 패널 안에 텍스트 라벨을 만든다. 글자를 3배 크기로 렌더링한 뒤 1/3로 축소해
        /// 작은 글씨도 선명하게 보이도록 한다.
        /// </summary>
        private Text Label(Transform parent,string title,float y,int size)
        {
            var t=LabUIKit.Clip(LabUIKit.Label(parent,title,"",size,LabUIKit.TextMain));
            LabUIKit.TopLeft(t.rectTransform,20,-y,320,32);
            t.fontSize=size*3; t.rectTransform.sizeDelta*=3; t.rectTransform.localScale=Vector3.one/3;
            t.raycastTarget=false;
            return t;
        }
        /// <summary>오버레이 Canvas와 상태 패널(이름, HP 바, HP 수치, 상태 라벨)을 생성하고 클릭을 가로채지 않게 설정한다.</summary>
        private void Awake()
        {
            player=GetComponent<PlayerCharacter>();
            var root=new GameObject("Player Status Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            root.transform.SetParent(transform,false);
            canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder=100; canvas.pixelPerfect=true;
            var scaler=root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
            var panel=LabUIKit.Panel(root.transform,"Player Status",LabUIKit.PanelBg);
            LabUIKit.TopLeft(panel,18,-18,360,168);
            nameLabel=Label(panel,"Player Name",14,24);
            fill=LabUIKit.Bar(panel,"HP Bar",20,-57,320,16,LabUIKit.PanelEdge,LabUIKit.Good);
            hpLabel=Label(panel,"HP",80,18);
            stateLabel=Label(panel,"Current State",120,18);
            foreach(var graphic in root.GetComponentsInChildren<Graphic>()) graphic.raycastTarget=false;
        }
        /// <summary>컴포넌트가 켜지면 상태 패널도 함께 보이게 한다.</summary>
        private void OnEnable() { if(canvas!=null) canvas.gameObject.SetActive(true); }
        /// <summary>컴포넌트가 꺼지면 상태 패널도 함께 숨긴다.</summary>
        private void OnDisable() { if(canvas!=null) canvas.gameObject.SetActive(false); }

        #endregion

        #region 표시 갱신

        /// <summary>현재 플레이어 상태를 패널에 표시할 짧은 영문 문자열로 변환한다. 공격 중이면 스킬/공중 콤보 단계까지 구분한다.</summary>
        private string State()
        {
            var combat=player.Combat;
            switch(player.CurrentState)
            {
                case PlayerState.HitReact: return "HIT";
                case PlayerState.Dodge: return "DODGE";
                case PlayerState.LinkJump: return "LINK JUMP";
                case PlayerState.Attack:
                    if(combat.IsUsingSkillA) return "SKILL A";
                    if(combat.IsUsingSkill) return "SKILL G";
                    if(combat.IsUsingAirCombo) return "AIR COMBO " + (combat.AirComboIndex + 1);
                    return "ATTACK";
                case PlayerState.AirRecovery: return "AIR RECOVERY";
                case PlayerState.Guard: return "GUARD";
                case PlayerState.Jump: return "JUMP";
                case PlayerState.Run: return "RUN";
                case PlayerState.Walk: return "WALK";
                default: return "IDLE";
            }
        }
        /// <summary>매 프레임 이름, HP 바(30% 이하이면 경고색), HP 수치, 상태 문자열을 갱신한다.</summary>
        private void LateUpdate()
        {
            var health=player.Health;
            nameLabel.text=health.PlayerName;
            fill.fillAmount=health.CurrentHealth/health.MaxHealth;
            fill.color=fill.fillAmount<=.3f?LabUIKit.Bad:LabUIKit.Good;
            hpLabel.text=string.Format("HP  {0:0} / {1:0}",health.CurrentHealth,health.MaxHealth);
            stateLabel.text="STATE  "+State();
        }

        #endregion
    }
}