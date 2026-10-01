using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using ParrySystem.Combat;
namespace ParrySystem.Training
{
    public sealed class ComboFeedbackUI : MonoBehaviour
    {
        public Text number, caption;
        public RectTransform lettering;
        public CanvasGroup visibility;
        [Min(.5f)] public float comboTimeout=3f;
        public int Count { get; private set; }
        private float sinceHit=99f;
        private Vector2 origin;
        private readonly List<EnemyDamageReaction> targets=new List<EnemyDamageReaction>();
        void Awake() { origin=lettering.anchoredPosition;visibility.alpha=0; }
        void OnEnable()
        {
            foreach(var root in gameObject.scene.GetRootGameObjects())
                foreach(var target in root.GetComponentsInChildren<EnemyDamageReaction>(true)) {
                    targets.Add(target);target.DamageReceived+=OnHit;
                }
        }
        void OnDisable()
        {
            foreach(var target in targets)if(target!=null)target.DamageReceived-=OnHit;
            targets.Clear();ResetCombo();
        }
        void OnHit(float damage)
        {
            if(damage<=0)return;
            if(sinceHit>=comboTimeout)Count=0;
            Count++;sinceHit=0;
            number.text=Count.ToString();
            caption.text=Count==1?"HIT":"HITS";
            Color tint=Count>=20?new Color(1,.35f,.25f):Count>=10?new Color(1,.67f,.2f):
                Count>=5?new Color(.35f,.95f,1):Color.white;
            number.color=tint;caption.color=tint;
            visibility.alpha=1;
        }
        public void ResetCombo()
        {
            Count=0;sinceHit=comboTimeout;
            if(visibility!=null)visibility.alpha=0;
        }
        void Update()
        {
            if(Keyboard.current!=null&&Keyboard.current.f5Key.wasPressedThisFrame)ResetCombo();
            if(Count==0)return;
            sinceHit+=Time.deltaTime;
            if(sinceHit>=comboTimeout){ResetCombo();return;}
            float t=Mathf.Clamp01(sinceHit/.32f);
            float pulse=.42f*Mathf.Exp(-9f*t)*Mathf.Cos(12f*t);
            lettering.localScale=Vector3.one*(1+pulse);
            lettering.localRotation=Quaternion.Euler(0,0,-5f*(1-t)*(1-t));
            lettering.anchoredPosition=origin+new Vector2(18f*(1-t)*(1-t),8f*Mathf.Sin(t*Mathf.PI));
            visibility.alpha=1-Mathf.Clamp01((sinceHit-(comboTimeout-.55f))/.55f);
        }
    }
}
