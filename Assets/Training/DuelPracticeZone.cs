using UnityEngine;
using UnityEngine.InputSystem;
using ParrySystem.Combat;
namespace ParrySystem.Training
{
    [DefaultExecutionOrder(-100)]
    public sealed class DuelPracticeZone : MonoBehaviour
    {
        public Transform player;
        public EnemyStateMachine opponent;
        public PracticeMonitor dummyMonitor;
        public AILearning.LabUI enemyMonitor;
        public Vector2 halfSize = new Vector2(14,14);
        private EnemyCombat combat;
        private EnemyDamageReaction health;
        private EnemyWeaponHitbox[] weapons;
        private Vector3 spawn;
        private Quaternion facing;
        private bool engaged;
        private float defeatTime = -1;
        public bool Engaged => engaged;
        private void Start()
        {
            combat=opponent.GetComponent<EnemyCombat>();
            health=opponent.GetComponent<EnemyDamageReaction>();
            weapons=opponent.GetComponentsInChildren<EnemyWeaponHitbox>(true);
            spawn=opponent.transform.position; facing=opponent.transform.rotation;
            SetEngaged(false);
        }
        private bool Inside(Vector3 p)
        {
            p-=transform.position;
            return Mathf.Abs(p.x)<halfSize.x && Mathf.Abs(p.z)<halfSize.y;
        }
        private void SetEngaged(bool value)
        {
            engaged=value;
            if(enemyMonitor!=null) enemyMonitor.gameObject.SetActive(value);
            opponent.enabled=value;
            foreach(var w in weapons) w.enabled=value;
            if(dummyMonitor!=null) dummyMonitor.enabled=!value;
        }
        public void ResetOpponent()
        {
            opponent.ChangeState(opponent.IdleState);
            combat.SetParryWindowOpen(false); combat.ResetGroggyGauge();
            opponent.transform.SetPositionAndRotation(spawn,facing);
            health.RestoreHealth(); defeatTime=-1;
            Physics.SyncTransforms();
        }
        private void Update()
        {
            if(player==null || opponent==null || combat==null) return;
            bool inside=Inside(player.position);
            if(inside!=engaged) { ResetOpponent(); SetEngaged(inside); }
            if(!engaged) return;
            if(!Inside(opponent.transform.position) || opponent.transform.position.y < -3
                || (Keyboard.current!=null && Keyboard.current.f5Key.wasPressedThisFrame))
                ResetOpponent();
            if(health.CurrentHealth<=0) {
                if(defeatTime<0) defeatTime=Time.time;
                if(Time.time-defeatTime>=2) ResetOpponent();
            }
        }
        private void OnDisable() { if(dummyMonitor!=null) dummyMonitor.enabled=true; if(enemyMonitor!=null) enemyMonitor.gameObject.SetActive(false); }
        private void OnGUI()
        {
            var style=new GUIStyle(GUI.skin.label){fontSize=18};
            if(!engaged) {
                GUI.Label(new Rect(20,Screen.height-40,700,30),"1 vs 1 arena: follow the passage to the RIGHT (+X)",style);
                return;
            }
            GUI.Label(new Rect(20,Screen.height-40,700,30),"F5: reset opponent   /   Leave arena to end practice",style);
        }
    }
}
