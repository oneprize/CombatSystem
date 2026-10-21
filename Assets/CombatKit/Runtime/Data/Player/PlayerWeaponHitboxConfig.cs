using UnityEngine;

namespace CombatSystem.Data.Player
{
    /// <summary>
    /// 플레이어 공격 종류별 히트 구간(애니메이션 정규화 시간 시작/끝)과 판정 박스 위치/크기 설정.
    /// 히트박스 판정과 베기 궤적/지면 이펙트 연출이 같은 값을 공유해 타이밍을 맞춘다.
    /// 판정 박스의 Center는 플레이어 로컬 좌표 기준이다.
    /// </summary>
    [CreateAssetMenu(menuName = "CombatKit/Player/Player Weapon Hitbox Config", fileName = "PlayerWeaponHitboxConfig")]
    public class PlayerWeaponHitboxConfig : ScriptableObject
    {
        /// <summary>전용 구간이 없는 공격에 쓰는 기본 히트 구간.</summary>
        public Vector2 hitWindow = new Vector2(0.25f, 0.75f);
        /// <summary>현재 런타임 코드에서는 참조하지 않는다(지상 콤보는 groundHitWindows 사용).</summary>
        public Vector2 combo2HitWindow = new Vector2(0.1f, 0.25f);
        /// <summary>현재 런타임 코드에서는 참조하지 않는다(지상 콤보는 groundHitWindows 사용).</summary>
        public Vector2 combo3HitWindow = new Vector2(0.25f, 0.5f);
        /// <summary>Q 스킬(Skill G) 첫 번째 타격의 히트 구간.</summary>
        public Vector2 skillFirstHitWindow = new Vector2(0.10f, 0.18f);
        /// <summary>Q 스킬(Skill G) 두 번째 타격(지면 강타)의 히트 구간. 지면 이펙트 생성 시점도 이 구간 시작을 따른다.</summary>
        public Vector2 skillHitWindow = new Vector2(0.47f, 0.57f);
        public Vector3 skillImpactCenter = new Vector3(0f, -0.1f, 1.2f);
        public Vector3 skillImpactSize = new Vector3(1.8f, 2.2f, 2.4f);
        public Vector2 skillAHitWindow = new Vector2(15f / 45f, 16f / 45f);
        [Header("Upper attack")]
        public Vector2 upperHitWindow = new Vector2(9f / 75f, 16f / 75f);
        public Vector3 upperImpactCenter = new Vector3(0, -.15f, 1.2f);
        public Vector3 upperImpactSize = new Vector3(1.4f, 2.2f, 2f);
        [Header("Air combo (Combo_Attack_Air_06_01-04)")]
        // 단계별 정규화 시간. 각 클립의 오른팔 휘두름을 보고 추정한 값이므로 플레이 모드에서 조정한다.
        public Vector2[] airHitWindows = {
            new Vector2(.12f, .32f), new Vector2(.02f, .30f), new Vector2(.18f, .32f), new Vector2(.02f, .36f) };
        public Vector3 airImpactCenter = new Vector3(0, -.1f, 1.25f);
        public Vector3 airImpactSize = new Vector3(1.8f, 2.5f, 2.5f);
        [Header("Ground combo (Combo_Attack_04_01-04)")]
        // 단계별 정규화 시간. 각 클립의 오른팔 휘두름을 보고 추정한 값이므로 플레이 모드에서 조정한다.
        public Vector2[] groundHitWindows = {
            new Vector2(.05f, .20f), new Vector2(.12f, .25f), new Vector2(.02f, .15f), new Vector2(.18f, .35f) };
        [Header("Ground combo right-foot kick")]
        [Tooltip("Ground combo step (0-based) that hits with the right foot instead of the sword. -1 = none.")]
        public int groundKickComboIndex = -1;
        public Vector2 groundKickWindow = new Vector2(.25f, .46f);
        /// <summary>발차기 판정에 쓰는 캡슐 반지름(오른발이 이동한 경로를 따라 쓸어 판정).</summary>
        [Min(.01f)] public float groundKickRadius = .3f;
        [Header("Skill C chase leap (Attack_Up_Floor_To_Air_02)")]
        // 올려 베기의 정규화 시간. 판정 박스는 공중 콤보용(airImpactCenter/Size)을 함께 쓴다.
        public Vector2 chaseHitWindow = new Vector2(.45f, .80f);
        [Header("Skill C launcher")]
        public Vector2 skillCHitWindow = new Vector2(.30f, .44f);
        public Vector3 skillCImpactCenter = new Vector3(0f, -.1f, 1.1f);
        public Vector3 skillCImpactSize = new Vector3(1.6f, 2.4f, 2.2f);
    }
}
