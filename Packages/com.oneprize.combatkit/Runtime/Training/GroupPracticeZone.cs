using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using CombatSystem.Combat;
using CombatSystem.Core;
using CombatSystem.Data.Training;
namespace CombatSystem.Training
{
    [DefaultExecutionOrder(-100)]
    public sealed class GroupPracticeZone : MonoBehaviour
    {
        public Transform player;
        public List<EnemyStateMachine> opponents = new List<EnemyStateMachine>();
        public PracticeMonitor dummyMonitor;
        public GameObject controls;
        public Button addButton, removeButton;
        public Text countLabel, helpLabel;
        [SerializeField] private GroupPracticeZoneConfig config;
        public int Count { get; private set; }
        public bool Engaged { get; private set; }
        public bool Editing { get; private set; }
        private Vector3[] spawns;
        private Quaternion[] rotations;
        private float[] defeatedAt;
        private float previousTimeScale;
        private CursorLockMode previousLock;
        private bool previousVisible;
        private readonly Dictionary<Behaviour,bool> inputStates=new Dictionary<Behaviour,bool>();
        private bool initialized;
        void Awake()
        {
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GroupPracticeZoneConfig>();
                Debug.LogWarning("GroupPracticeZone: no GroupPracticeZoneConfig assigned, using built-in defaults.", this);
            }
        }
        void EnsurePlayerLinked()
        {
            if (player != null) return;
            var nearest = CombatRegistry.FindNearest(CombatTeam.Player, transform.position);
            if (nearest != null) player = nearest.Transform;
        }
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
        bool Inside(Vector3 point)
        {
            var p=point-transform.position;
            return Mathf.Abs(p.x)<config.halfSize.x && Mathf.Abs(p.z)<config.halfSize.y;
        }
        void ApplySlot(int i)
        {
            var m=opponents[i];
            if(i>=Count) { m.gameObject.SetActive(false);return; }
            m.gameObject.SetActive(true);
            ResetSlot(i);
            m.enabled=Engaged;
            foreach(var w in m.GetComponentsInChildren<EnemyWeaponHitbox>(true))w.enabled=Engaged;
        }
        void ResetSlot(int i)
        {
            var m=opponents[i];
            m.ChangeState(m.IdleState);
            var c=m.GetComponent<EnemyCombat>();

            m.GetComponent<EnemyDamageReaction>().RestoreHealth();
            m.transform.SetPositionAndRotation(spawns[i],rotations[i]);
            defeatedAt[i]=-1;
        }
        public void AddEnemy() { SetCount(Count+1); }
        public void RemoveEnemy() { SetCount(Count-1); }
        public void SetCount(int value)
        {
            if(!initialized)return;
            int old=Count;
            Count=Mathf.Clamp(value,0,Mathf.Min(20,opponents.Count));
            for(int i=Mathf.Min(old,Count);i<Mathf.Max(old,Count);i++)ApplySlot(i);
            RefreshUI();
        }
        public void ResetOpponents()
        {
            for(int i=0;i<Count;i++)ResetSlot(i);
            Physics.SyncTransforms();
        }
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
        public void SetEditing(bool value)
        {
            if(value==Editing || (value&&!Engaged))return;
            Editing=value;
            if(value){
                previousTimeScale=Time.timeScale;previousLock=Cursor.lockState;previousVisible=Cursor.visible;
                inputStates.Clear();
                var character=player.GetComponent<CombatSystem.Player.PlayerCharacter>();
                if(character!=null)inputStates[character]=character.enabled;
                foreach(var root in gameObject.scene.GetRootGameObjects())
                    foreach(var b in root.GetComponentsInChildren<Behaviour>(true)){
                        string n=b.GetType().Name;
                        if(n=="CameraRig"||n=="CinemachineInputAxisController")inputStates[b]=b.enabled;
                    }
                foreach(var pair in inputStates)pair.Key.enabled=false;
                Time.timeScale=0;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            } else {
                Time.timeScale=previousTimeScale;
                foreach(var pair in inputStates)if(pair.Key!=null)pair.Key.enabled=pair.Value;
                inputStates.Clear();
                Cursor.lockState=previousLock;Cursor.visible=previousVisible;
            }
            RefreshUI();
        }
        void RefreshUI()
        {
            if(countLabel!=null)countLabel.text="MONSTERS   "+Count+" / 20";
            if(helpLabel!=null)helpLabel.text=Editing?"PAUSED - click + / -\nTab: resume combat":"Tab: edit monster count\nF5: reset opponents";
            if(addButton!=null)addButton.interactable=Editing&&Count<Mathf.Min(20,opponents.Count);
            if(removeButton!=null)removeButton.interactable=Editing&&Count>0;
        }
        void OnDisable()
        {
            if(Editing)SetEditing(false);
            if(controls!=null)controls.SetActive(false);
            if(Engaged&&dummyMonitor!=null)dummyMonitor.enabled=true;
        }
        void OnDestroy()
        {
            if(addButton!=null)addButton.onClick.RemoveListener(AddEnemy);
            if(removeButton!=null)removeButton.onClick.RemoveListener(RemoveEnemy);
        }
    }
}
