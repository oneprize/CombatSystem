using UnityEngine;
using UnityEngine.InputSystem;
using CombatSystem.Combat;
using CombatSystem.Core;
using CombatSystem.Data.Training;
namespace CombatSystem.Training
{
    /// <summary>
    /// 1:1 대결 연습 구역. 플레이어가 구역에 들어오면 상대 적의 AI와 무기 히트박스를 켜고 적 모니터를 표시하며,
    /// 나가면 상대를 초기 위치로 되돌리고 비활성화한다. 상대가 구역을 벗어나거나 쓰러지면 자동으로 재배치한다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class DuelPracticeZone : MonoBehaviour
    {
        #region 필드

        public Transform player;
        public EnemyStateMachine opponent;
        public PracticeMonitor dummyMonitor;
        public CombatSystem.UI.LabUI enemyMonitor;
        [SerializeField] private DuelPracticeZoneConfig config;
        private EnemyCombat combat;
        private EnemyDamageReaction health;
        private EnemyWeaponHitbox[] weapons;
        private Vector3 spawn;
        private Quaternion facing;
        private bool engaged;
        private float defeatTime = -1;
        /// <summary>플레이어가 구역 안에 있어 대결이 진행 중인지 여부.</summary>
        public bool Engaged => engaged;

        #endregion

        #region 초기화와 참조 연결

        /// <summary>설정이 없으면 기본값 인스턴스로 대체한다.</summary>
        private void Awake()
        {
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<DuelPracticeZoneConfig>();
                Debug.LogWarning("DuelPracticeZone: no DuelPracticeZoneConfig assigned, using built-in defaults.", this);
            }
        }
        /// <summary>플레이어가 지정되지 않았으면 구역에서 가장 가까운 플레이어 팀 전투원을 찾아 연결한다.</summary>
        private void EnsurePlayerLinked()
        {
            if (player != null) return;
            var nearest = CombatRegistry.FindNearest(CombatTeam.Player, transform.position);
            if (nearest != null) player = nearest.Transform;
        }
        /// <summary>상대가 없으면 가장 가까운 적을 찾아 연결하고, 처음 연결될 때 전투 컴포넌트와 무기, 재배치용 초기 위치/방향을 캐시한다.</summary>
        private void EnsureOpponentLinked()
        {
            if (opponent == null)
            {
                var nearest = CombatRegistry.FindNearest(CombatTeam.Enemy, transform.position);
                if (nearest != null) opponent = nearest.Transform.GetComponent<EnemyStateMachine>();
            }
            if (opponent != null && combat == null)
            {
                combat=opponent.GetComponent<EnemyCombat>();
                health=opponent.GetComponent<EnemyDamageReaction>();
                weapons=opponent.GetComponentsInChildren<EnemyWeaponHitbox>(true);
                spawn=opponent.transform.position; facing=opponent.transform.rotation;
            }
        }
        /// <summary>참조를 연결하고 대결이 시작되지 않은 상태(상대 비활성)로 시작한다.</summary>
        private void Start()
        {
            EnsurePlayerLinked();
            EnsureOpponentLinked();
            SetEngaged(false);
        }

        #endregion

        #region 대결 진행

        /// <summary>월드 좌표가 구역의 X/Z 범위 안에 있는지 판정한다(높이는 무시).</summary>
        private bool Inside(Vector3 p)
        {
            p-=transform.position;
            return Mathf.Abs(p.x)<config.halfSize.x && Mathf.Abs(p.z)<config.halfSize.y;
        }
        /// <summary>대결 상태에 맞춰 상대 AI·무기 히트박스·적 모니터를 켜고, 허수아비 모니터는 반대로 토글한다.</summary>
        private void SetEngaged(bool value)
        {
            engaged=value;
            if(enemyMonitor!=null) enemyMonitor.gameObject.SetActive(value);
            if(opponent!=null) opponent.enabled=value;
            if(weapons!=null) foreach(var w in weapons) w.enabled=value;
            if(dummyMonitor!=null) dummyMonitor.enabled=!value;
        }
        /// <summary>상대를 대기 상태로 돌리고 초기 위치/방향으로 옮긴 뒤 체력을 회복시킨다.</summary>
        public void ResetOpponent()
        {
            opponent.ChangeState(opponent.IdleState);

            opponent.transform.SetPositionAndRotation(spawn,facing);
            health.RestoreHealth(); defeatTime=-1;
            Physics.SyncTransforms();
        }
        /// <summary>
        /// 플레이어의 구역 진입/이탈에 맞춰 대결을 켜고 끈다. 대결 중 상대가 구역을 벗어나거나 추락하거나 F5를 누르면 즉시,
        /// 쓰러지면 2초 뒤 상대를 재배치한다.
        /// </summary>
        private void Update()
        {
            EnsurePlayerLinked();
            EnsureOpponentLinked();
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
        /// <summary>구역이 꺼지면 허수아비 모니터를 되살리고 적 모니터를 숨긴다.</summary>
        private void OnDisable() { if(dummyMonitor!=null) dummyMonitor.enabled=true; if(enemyMonitor!=null) enemyMonitor.gameObject.SetActive(false); }

        #endregion

        #region 안내 표시

        /// <summary>화면 하단에 구역 진입 안내 또는 대결 중 조작 안내를 표시한다.</summary>
        private void OnGUI()
        {
            var style=new GUIStyle(GUI.skin.label){fontSize=18};
            if(!engaged) {
                GUI.Label(new Rect(20,Screen.height-40,700,30),"1 vs 1 arena: follow the passage to the RIGHT (+X)",style);
                return;
            }
            GUI.Label(new Rect(20,Screen.height-40,700,30),"F5: reset opponent   /   Leave arena to end practice",style);
        }

        #endregion
    }
}
