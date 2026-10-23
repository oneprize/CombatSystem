using UnityEngine;
using CombatSystem.Combat;
namespace CombatSystem.Training
{
    /// <summary>
    /// 콤보 연습용 허수아비. 공격하지 않고 맞을 때마다 체력을 즉시 회복하며, 적중 수·누적 피해·최근 피해를 기록해 연습 모니터에 제공한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PracticeDummy : MonoBehaviour
    {
        public int TotalHits { get; private set; }
        /// <summary>3초 안에 이어진 연속 적중 수. 3초 동안 맞지 않으면 0으로 초기화된다.</summary>
        public int ComboHits { get; private set; }
        public float TotalDamage { get; private set; }
        public float LastDamage { get; private set; }
        public float LastHitTime { get; private set; } = -100f;
        public Vector3 SpawnPosition { get; private set; }
        private Quaternion spawnRotation;
        private EnemyDamageReaction reaction;
        private EnemyStateMachine machine;
        private IEnemyState idle;
        /// <summary>아무 행동도 하지 않는 허수아비 전용 대기 상태. 처음 접근할 때 생성한다.</summary>
        public IEnemyState Idle => idle ?? (idle = new DummyIdle(GetComponent<EnemyCombat>()));
        /// <summary>초기 위치/방향을 저장하고, 허수아비가 공격하지 않도록 모든 무기 히트박스를 끈다.</summary>
        private void Awake()
        {
            SpawnPosition = transform.position; spawnRotation = transform.rotation;
            reaction = GetComponent<EnemyDamageReaction>();
            machine = GetComponent<EnemyStateMachine>();
            foreach (var weapon in GetComponentsInChildren<EnemyWeaponHitbox>(true)) weapon.enabled = false;
        }
        /// <summary>피격 이벤트를 구독한다.</summary>
        private void OnEnable() { reaction.DamageReceived += OnHit; }
        /// <summary>피격 이벤트 구독을 해제한다.</summary>
        private void OnDisable() { reaction.DamageReceived -= OnHit; }
        /// <summary>적중 통계를 갱신하고, 허수아비가 쓰러지지 않도록 체력을 바로 회복시킨다.</summary>
        private void OnHit(float damage)
        {
            if (Time.time - LastHitTime > 3f) ComboHits = 0;
            ComboHits++; TotalHits++; TotalDamage += damage; LastDamage = damage; LastHitTime = Time.time;
            reaction.RestoreHealth();
        }
        /// <summary>콤보가 끊겼으면 연속 적중 수를 초기화하고, 맵 아래로 떨어지면 허수아비를 원위치시킨다.</summary>
        private void Update()
        {
            if (Time.time - LastHitTime > 3f) ComboHits = 0;
            if (transform.position.y < -5f) ResetDummy();
        }
        /// <summary>허수아비를 대기 상태·초기 위치·최대 체력으로 되돌리고 모든 적중 통계를 지운다.</summary>
        public void ResetDummy()
        {
            machine.ChangeState(Idle);
            GetComponent<EnemyCombat>().ClearParryGauge();
            transform.SetPositionAndRotation(SpawnPosition, spawnRotation);
            reaction.RestoreHealth();
            TotalHits = ComboHits = 0; TotalDamage = LastDamage = 0; LastHitTime = -100f;
            Physics.SyncTransforms();
        }
        /// <summary>대기 애니메이션만 재생하고 아무 판단도 하지 않는 허수아비 상태.</summary>
        private sealed class DummyIdle : IEnemyState
        {
            private readonly EnemyCombat combat;
            /// <summary>대기 애니메이션 재생에 쓸 전투 컴포넌트를 연결한다.</summary>
            public DummyIdle(EnemyCombat combat) { this.combat = combat; }
            /// <summary>대기 애니메이션을 재생한다.</summary>
            public void Enter() { combat.PlayIdleAnim(); }
            /// <summary>허수아비는 스스로 행동하지 않으므로 아무 것도 하지 않는다.</summary>
            public void Tick(float dt) { }
            /// <summary>정리할 것이 없어 아무 것도 하지 않는다.</summary>
            public void Exit() { }
        }
    }
}
