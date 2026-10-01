using UnityEngine;
using ParrySystem.Combat;
namespace ParrySystem.Training
{
    [DisallowMultipleComponent]
    public sealed class PracticeDummy : MonoBehaviour
    {
        public int TotalHits { get; private set; }
        public int ComboHits { get; private set; }
        public float TotalDamage { get; private set; }
        public float LastDamage { get; private set; }
        public float LastHitTime { get; private set; } = -100f;
        public Vector3 SpawnPosition { get; private set; }
        private Quaternion spawnRotation;
        private EnemyDamageReaction reaction;
        private EnemyStateMachine machine;
        private IEnemyState idle;
        public IEnemyState Idle => idle ?? (idle = new DummyIdle(GetComponent<EnemyCombat>()));
        private void Awake()
        {
            SpawnPosition = transform.position; spawnRotation = transform.rotation;
            reaction = GetComponent<EnemyDamageReaction>();
            machine = GetComponent<EnemyStateMachine>();
            foreach (var weapon in GetComponentsInChildren<EnemyWeaponHitbox>(true)) weapon.enabled = false;
        }
        private void OnEnable() { reaction.DamageReceived += OnHit; }
        private void OnDisable() { reaction.DamageReceived -= OnHit; }
        private void OnHit(float damage)
        {
            if (Time.time - LastHitTime > 3f) ComboHits = 0;
            ComboHits++; TotalHits++; TotalDamage += damage; LastDamage = damage; LastHitTime = Time.time;
            reaction.RestoreHealth();
        }
        private void Update()
        {
            if (Time.time - LastHitTime > 3f) ComboHits = 0;
            if (transform.position.y < -5f) ResetDummy();
        }
        public void ResetDummy()
        {
            machine.ChangeState(Idle);
            transform.SetPositionAndRotation(SpawnPosition, spawnRotation);
            reaction.RestoreHealth();
            TotalHits = ComboHits = 0; TotalDamage = LastDamage = 0; LastHitTime = -100f;
            Physics.SyncTransforms();
        }
        private sealed class DummyIdle : IEnemyState
        {
            private readonly EnemyCombat combat;
            public DummyIdle(EnemyCombat combat) { this.combat = combat; }
            public void Enter() { combat.SetParryWindowOpen(false); combat.PlayIdleAnim(); }
            public void Tick(float dt) { }
            public void Exit() { }
        }
    }
}
