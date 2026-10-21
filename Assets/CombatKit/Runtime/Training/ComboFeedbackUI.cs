using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using CombatSystem.Combat;
using CombatSystem.Data.Training;
namespace CombatSystem.Training
{
    /// <summary>
    /// 씬의 모든 적 피격을 구독해 연속 적중 수를 세고, 적중마다 튀는 애니메이션과 단계별 색상으로 "N HITS" 표시를 띄운다.
    /// 일정 시간 적중이 없거나 F5를 누르면 콤보를 초기화한다.
    /// </summary>
    public sealed class ComboFeedbackUI : MonoBehaviour
    {
        #region 필드

        public Text number, caption;
        public RectTransform lettering;
        public CanvasGroup visibility;
        [SerializeField] private ComboFeedbackConfig config;
        /// <summary>현재 이어지고 있는 콤보 적중 수.</summary>
        public int Count { get; private set; }
        private float sinceHit=99f;
        private Vector2 origin;
        private readonly List<EnemyDamageReaction> targets=new List<EnemyDamageReaction>();

        #endregion

        #region Unity 생명주기

        /// <summary>설정이 없으면 기본값으로 대체하고, 흔들림 애니메이션의 기준 위치를 저장한 뒤 표시를 숨긴다.</summary>
        void Awake()
        {
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<ComboFeedbackConfig>();
                Debug.LogWarning("ComboFeedbackUI: no ComboFeedbackConfig assigned, using built-in defaults.", this);
            }
            origin=lettering.anchoredPosition;visibility.alpha=0;
        }
        /// <summary>씬 안의 모든 적(비활성 포함) 피격 이벤트를 구독한다.</summary>
        void OnEnable()
        {
            foreach(var root in gameObject.scene.GetRootGameObjects())
                foreach(var target in root.GetComponentsInChildren<EnemyDamageReaction>(true)) {
                    targets.Add(target);target.DamageReceived+=OnHit;
                }
        }
        /// <summary>구독을 모두 해제하고 콤보 표시를 초기화한다.</summary>
        void OnDisable()
        {
            foreach(var target in targets)if(target!=null)target.DamageReceived-=OnHit;
            targets.Clear();ResetCombo();
        }

        #endregion

        #region 콤보 집계와 표시

        /// <summary>실제 피해가 들어간 적중만 콤보로 센다. 제한 시간이 지났으면 새 콤보로 시작하고, 5/10/20 단계마다 색을 바꾼다.</summary>
        void OnHit(float damage)
        {
            if(damage<=0)return;
            if(sinceHit>=config.comboTimeout)Count=0;
            Count++;sinceHit=0;
            number.text=Count.ToString();
            caption.text=Count==1?"HIT":"HITS";
            Color tint=Count>=20?new Color(1,.35f,.25f):Count>=10?new Color(1,.67f,.2f):
                Count>=5?new Color(.35f,.95f,1):Color.white;
            number.color=tint;caption.color=tint;
            visibility.alpha=1;
        }
        /// <summary>콤보 수를 0으로 되돌리고 표시를 즉시 숨긴다.</summary>
        public void ResetCombo()
        {
            Count=0;sinceHit=config.comboTimeout;
            if(visibility!=null)visibility.alpha=0;
        }
        /// <summary>적중 직후 감쇠 진동 형태의 크기·회전·위치 튐 효과를 주고, 제한 시간이 다가오면 서서히 사라지게 한다.</summary>
        void Update()
        {
            if(Keyboard.current!=null&&Keyboard.current.f5Key.wasPressedThisFrame)ResetCombo();
            if(Count==0)return;
            sinceHit+=Time.deltaTime;
            if(sinceHit>=config.comboTimeout){ResetCombo();return;}
            float t=Mathf.Clamp01(sinceHit/.32f);
            float pulse=.42f*Mathf.Exp(-9f*t)*Mathf.Cos(12f*t);
            lettering.localScale=Vector3.one*(1+pulse);
            lettering.localRotation=Quaternion.Euler(0,0,-5f*(1-t)*(1-t));
            lettering.anchoredPosition=origin+new Vector2(18f*(1-t)*(1-t),8f*Mathf.Sin(t*Mathf.PI));
            visibility.alpha=1-Mathf.Clamp01((sinceHit-(config.comboTimeout-.55f))/.55f);
        }

        #endregion
    }
}
