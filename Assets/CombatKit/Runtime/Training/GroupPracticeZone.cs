using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using CombatSystem.Combat;
using CombatSystem.Core;
using CombatSystem.Data.Training;
namespace CombatSystem.Training
{
    /// <summary>
    /// 다수 적 연습 구역. 미리 배치된 적들을 슬롯으로 관리하며, 플레이어가 구역에 들어오면 활성 슬롯의 적 AI와 무기를 켠다.
    /// Tab으로 게임을 멈추고 적 수(최대 20)를 조절하는 편집 모드에 들어갈 수 있고, 구역을 벗어나거나 쓰러진 적은 자동으로 재배치한다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GroupPracticeZone : MonoBehaviour
    {
        #region 필드

        public Transform player;
        public List<EnemyStateMachine> opponents = new List<EnemyStateMachine>();
        public PracticeMonitor dummyMonitor;
        public GameObject controls;
        public Button addButton, removeButton;
        public Text countLabel, helpLabel;
        [SerializeField] private GroupPracticeZoneConfig config;
        /// <summary>현재 활성화된 적 슬롯 수. 앞쪽 슬롯부터 Count개가 활성화된다.</summary>
        public int Count { get; private set; }
        /// <summary>플레이어가 구역 안에 있어 적들이 전투 중인지 여부.</summary>
        public bool Engaged { get; private set; }
        /// <summary>게임을 멈추고 적 수를 편집하는 중인지 여부.</summary>
        public bool Editing { get; private set; }
        private Vector3[] spawns;
        private Quaternion[] rotations;
        private float[] defeatedAt;
        private float previousTimeScale;
        private CombatSystem.Player.PlayerCursorController cursorController;

        /// <summary>편집 모드 동안 꺼 둔 입력 관련 컴포넌트와 원래 활성 상태. 편집을 끝낼 때 그대로 복원한다.</summary>
        private readonly Dictionary<Behaviour,bool> inputStates=new Dictionary<Behaviour,bool>();
        private bool initialized;

        #endregion

        #region 초기화와 참조 연결

        /// <summary>설정이 없으면 기본값 인스턴스로 대체한다.</summary>
        void Awake()
        {
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GroupPracticeZoneConfig>();
                Debug.LogWarning("GroupPracticeZone: no GroupPracticeZoneConfig assigned, using built-in defaults.", this);
            }
        }
        /// <summary>플레이어가 지정되지 않았으면 구역에서 가장 가까운 플레이어 팀 전투원을 찾아 연결한다.</summary>
        void EnsurePlayerLinked()
        {
            if (player != null) return;
            var nearest = CombatRegistry.FindNearest(CombatTeam.Player, transform.position);
            if (nearest != null) player = nearest.Transform;
        }
        /// <summary>적 목록이 비어 있으면 레지스트리에 등록된 모든 적 상태 머신으로 채운다.</summary>
        void EnsureOpponentsLinked()
        {
            if (opponents != null && opponents.Count > 0) return;
            if (opponents == null) opponents = new List<EnemyStateMachine>();
            foreach (var combatant in CombatRegistry.GetAll(CombatTeam.Enemy))
            {
                var machine = combatant.Transform.GetComponent<EnemyStateMachine>();
                if (machine != null) opponents.Add(machine);
            }
        }
        /// <summary>각 적의 초기 위치/방향을 슬롯별로 저장하고, 설정된 초기 수만큼 활성화한 뒤 +/- 버튼을 연결한다.</summary>
        void Start()
        {
            EnsurePlayerLinked();
            EnsureOpponentsLinked();
            spawns=new Vector3[opponents.Count];rotations=new Quaternion[opponents.Count];
            defeatedAt=new float[opponents.Count];
            for(int i=0;i<opponents.Count;i++) {
                spawns[i]=opponents[i].transform.position;rotations[i]=opponents[i].transform.rotation;
                defeatedAt[i]=-1;
            }
            initialized=true;
            Count=Mathf.Clamp(config.initialCount,0,Mathf.Min(20,opponents.Count));
            for(int i=0;i<opponents.Count;i++) ApplySlot(i);
            addButton.onClick.AddListener(AddEnemy);
            removeButton.onClick.AddListener(RemoveEnemy);
            controls.SetActive(false);
            RefreshUI();
        }

        #endregion

        #region 슬롯 관리

        /// <summary>월드 좌표가 구역의 X/Z 범위 안에 있는지 판정한다(높이는 무시).</summary>
        bool Inside(Vector3 point)
        {
            var p=point-transform.position;
            return Mathf.Abs(p.x)<config.halfSize.x && Mathf.Abs(p.z)<config.halfSize.y;
        }
        /// <summary>슬롯 번호가 활성 수 밖이면 적을 끄고, 안이면 켜서 재배치한 뒤 전투 여부에 맞춰 AI와 무기 히트박스를 토글한다.</summary>
        void ApplySlot(int i)
        {
            var m=opponents[i];
            if(i>=Count) { m.gameObject.SetActive(false);return; }
            m.gameObject.SetActive(true);
            ResetSlot(i);
            m.enabled=Engaged;
            foreach(var w in m.GetComponentsInChildren<EnemyWeaponHitbox>(true))w.enabled=Engaged;
        }
        /// <summary>해당 슬롯의 적을 대기 상태로 돌리고 체력을 회복시켜 초기 위치로 되돌린다.</summary>
        void ResetSlot(int i)
        {
            var m=opponents[i];
            m.ChangeState(m.IdleState);
            var c=m.GetComponent<EnemyCombat>();

            m.GetComponent<EnemyDamageReaction>().RestoreHealth();
            c.ClearParryGauge();
            m.transform.SetPositionAndRotation(spawns[i],rotations[i]);
            defeatedAt[i]=-1;
        }
        /// <summary>+ 버튼: 활성 적 수를 하나 늘린다.</summary>
        public void AddEnemy() { SetCount(Count+1); }
        /// <summary>- 버튼: 활성 적 수를 하나 줄인다.</summary>
        public void RemoveEnemy() { SetCount(Count-1); }
        /// <summary>활성 적 수를 0~최대(20 또는 배치된 수) 범위로 바꾸고, 변경된 구간의 슬롯만 다시 적용한다.</summary>
        public void SetCount(int value)
        {
            if(!initialized)return;
            int old=Count;
            Count=Mathf.Clamp(value,0,Mathf.Min(20,opponents.Count));
            for(int i=Mathf.Min(old,Count);i<Mathf.Max(old,Count);i++)ApplySlot(i);
            RefreshUI();
        }
        /// <summary>활성화된 모든 적을 초기 위치와 체력으로 되돌린다.</summary>
        public void ResetOpponents()
        {
            for(int i=0;i<Count;i++)ResetSlot(i);
            Physics.SyncTransforms();
        }

        #endregion

        #region 전투 진행

        /// <summary>
        /// 플레이어의 구역 진입/이탈에 맞춰 적 전투와 조작 UI를 켜고 끈다. 전투 중에는 Tab(편집 모드)·F5(재배치) 입력을 처리하고,
        /// 구역을 벗어나거나 추락한 적은 즉시, 쓰러진 적은 2초 뒤 재배치한다.
        /// </summary>
        void Update()
        {
            EnsurePlayerLinked();
            if(!initialized||player==null)return;
            bool inside=Inside(player.position);
            if(inside!=Engaged) {
                if(Editing)SetEditing(false);
                Engaged=inside;
                for(int i=0;i<Count;i++)ApplySlot(i);
                controls.SetActive(inside);
                if(dummyMonitor!=null)dummyMonitor.enabled=!inside;
                RefreshUI();
            }
            if(!Engaged)return;
            var k=Keyboard.current;
            if(k!=null&&k.tabKey.wasPressedThisFrame)SetEditing(!Editing);
            if(k!=null&&k.f5Key.wasPressedThisFrame)ResetOpponents();
            if(Editing)return;
            for(int i=0;i<Count;i++){
                var m=opponents[i];
                if(!Inside(m.transform.position)||m.transform.position.y < -3) {ResetSlot(i);continue;}
                if(m.GetComponent<EnemyDamageReaction>().CurrentHealth<=0) {
                    if(defeatedAt[i]<0)defeatedAt[i]=Time.time;
                    if(Time.time-defeatedAt[i]>=2)ResetSlot(i);
                }
            }
        }

        #endregion

        #region 편집 모드

        /// <summary>
        /// 편집 모드에 들어가면 시간을 멈추고 커서를 풀며, 플레이어와 카메라 입력 컴포넌트를 꺼 둔다.
        /// 나올 때는 이전 시간 배율과 각 컴포넌트의 원래 활성 상태를 복원한다. 구역 밖에서는 편집 모드로 들어갈 수 없다.
        /// </summary>
        public void SetEditing(bool value)
        {
            if(value==Editing || (value&&!Engaged))return;
            Editing=value;
            if(value){
                previousTimeScale=Time.timeScale;
                cursorController=player.GetComponent<CombatSystem.Player.PlayerCursorController>();
                if(cursorController!=null)cursorController.SetMenuOpen(this,true);
                inputStates.Clear();
                var character=player.GetComponent<CombatSystem.Player.PlayerCharacter>();
                if(character!=null)inputStates[character]=character.enabled;
                foreach(var root in gameObject.scene.GetRootGameObjects())
                    foreach(var b in root.GetComponentsInChildren<Behaviour>(true)){
                        string n=b.GetType().Name;
                        if(n=="CameraRig"||n=="CinemachineInputAxisController")inputStates[b]=b.enabled;
                    }
                foreach(var pair in inputStates)pair.Key.enabled=false;
                Time.timeScale=0;
            } else {
                Time.timeScale=previousTimeScale;
                foreach(var pair in inputStates)if(pair.Key!=null)pair.Key.enabled=pair.Value;
                inputStates.Clear();
                if(cursorController!=null)cursorController.SetMenuOpen(this,false);
            }
            RefreshUI();
        }
        /// <summary>적 수 표시와 도움말을 갱신하고, 편집 모드일 때만 +/- 버튼을 누를 수 있게 한다.</summary>
        void RefreshUI()
        {
            if(countLabel!=null)countLabel.text="MONSTERS   "+Count+" / 20";
            if(helpLabel!=null)helpLabel.text=Editing?"PAUSED - click + / -\nTab: resume combat":"Tab: edit monster count\nF5: reset opponents";
            if(addButton!=null)addButton.interactable=Editing&&Count<Mathf.Min(20,opponents.Count);
            if(removeButton!=null)removeButton.interactable=Editing&&Count>0;
        }

        #endregion

        #region 정리

        /// <summary>편집 모드를 해제해 시간과 입력을 복원하고, 조작 UI를 숨기며 허수아비 모니터를 되살린다.</summary>
        void OnDisable()
        {
            if(Editing)SetEditing(false);
            if(controls!=null)controls.SetActive(false);
            if(Engaged&&dummyMonitor!=null)dummyMonitor.enabled=true;
        }
        /// <summary>버튼에 등록한 리스너를 해제한다.</summary>
        void OnDestroy()
        {
            if(addButton!=null)addButton.onClick.RemoveListener(AddEnemy);
            if(removeButton!=null)removeButton.onClick.RemoveListener(RemoveEnemy);
        }

        #endregion
    }
}
