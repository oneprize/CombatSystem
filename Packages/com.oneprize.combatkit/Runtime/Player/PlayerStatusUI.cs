using UnityEngine;
using UnityEngine.UI;
using CombatSystem.UI;
namespace CombatSystem.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCharacter))]
    public sealed class PlayerStatusUI : MonoBehaviour
    {
        private PlayerCharacter player;
        private Canvas canvas;
        private Text nameLabel, hpLabel, stateLabel;
        private Image fill;
        private Text Label(Transform parent,string title,float y,int size)
        {
            var t=LabUIKit.Clip(LabUIKit.Label(parent,title,"",size,LabUIKit.TextMain));
            LabUIKit.TopLeft(t.rectTransform,20,-y,320,32);
            t.fontSize=size*3; t.rectTransform.sizeDelta*=3; t.rectTransform.localScale=Vector3.one/3;
            t.raycastTarget=false;
            return t;
        }
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
        private void OnEnable() { if(canvas!=null) canvas.gameObject.SetActive(true); }
        private void OnDisable() { if(canvas!=null) canvas.gameObject.SetActive(false); }
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
        private void LateUpdate()
        {
            var health=player.Health;
            nameLabel.text=health.PlayerName;
            fill.fillAmount=health.CurrentHealth/health.MaxHealth;
            fill.color=fill.fillAmount<=.3f?LabUIKit.Bad:LabUIKit.Good;
            hpLabel.text=string.Format("HP  {0:0} / {1:0}",health.CurrentHealth,health.MaxHealth);
            stateLabel.text="STATE  "+State();
        }
    }
}