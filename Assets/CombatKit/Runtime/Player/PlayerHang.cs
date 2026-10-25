using UnityEngine;

namespace CombatSystem.Player
{
    /// <summary>
    /// 철봉(HangRail) 매달리기. G 입력 시 거리와 높이에 맞는 Start 도약을 계획하고 실제 손 접촉 후 매달린다.
    /// <list type="bullet">
    /// <item>아래에서 잡기: interactionDistance 안에서 가까운 제자리 Start 또는 전방 Start로 도약하며, 클립의 접촉 시점에 손을 고정한다.</item>
    /// <item>위에서 잡기: 손이 철봉보다 위에 있고(철봉이 허리~손 높이) 앞쪽 철봉 아래가 비어 있으면
    /// KipUpIdle_To_HangingIdle 클립으로 철봉을 짚고 아래로 돌아 매달린다.</item>
    /// <item>공중: 실제 양팔 도달 범위와 비행 궤적으로 후보를 찾고, 손 접촉 전까지 비행을 유지한 뒤 몸을 내려 매달린다.</item>
    /// </list>
    /// 매달린 뒤의 스윙, 점프 이탈, 낙하·착지는 기존 동작을 유지한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHang : MonoBehaviour
    {
        // IsHanging also owns the approach/exit, so combat and normal locomotion cannot interrupt it.
        public bool IsHanging { get; private set; }
        public bool IsApproaching => IsHanging && !hasGrip && exitPhase == 0;
        public bool HasGrip => IsHanging && hasGrip && exitPhase < 2;

        public enum GrabKind { None, HopUp, KipDown, AirCatch }

        /// <summary>현재 위치에서 G로 잡을 수 있는 대상. UI 안내("G 매달리기")에 사용할 수 있다.</summary>
        public struct GrabOpportunity
        {
            public GrabKind kind;
            public HangRail rail;
            public Vector3 grip;
            public Quaternion facing;
            public Vector3 hangRoot;
            public float contactTime;
            public bool reachAnimation;
        }
        public GrabOpportunity Current { get; private set; }
        /// <summary>마지막으로 잡기를 거절한 이유. 튜닝용.</summary>
        public string LastDecision { get; private set; } = "";

        [Header("아래에서 잡기 / 공중 잡기")]
        [Tooltip("지상 G 입력으로 Start 도약을 시작할 최대 수평 거리(m).")]
        [Min(.1f)] public float interactionDistance = 2f;
        [Tooltip("전방 도약 대신 제자리 잡기 Start를 사용하는 거리(m).")]
        [Min(0f)] public float closeStartDistance = .75f;
        [Min(.1f)] public float maxGroundHangRise = 2f;
        [Min(.1f)] public float maxLaunchSpeed = 9f, maxApproachSpeed = 5f;
        [Range(.1f, 1.5f)] public float forwardStartSpeed = .65f, closeStartSpeed = .8f;
        [Range(.01f, .5f)] public float hangIdleBlend = .24f;
        [Range(.02f, .2f)] public float handContactTolerance = .12f;
        [Tooltip("공중에서 철봉 후보를 검사하는 수평 범위(m).") ]
        [HideInInspector] public float overheadRadius = .4f;
        [Tooltip("G로 수직 점프해 올라갈 수 있는 최대 높이(m). 매달린 자세 위치까지 이보다 높으면 직접 점프해야 한다.")]
        [HideInInspector] public float maxHopRise = .8f;
        [Tooltip("매달린 자세가 지금보다 이만큼은 높아야 한다(m). 더 낮은 철봉은 매달리면 발이 바닥에 닿는다.")]
        [Min(0f)] public float hangFootClearance = .1f;
        [Tooltip("수직 점프 잡기에 걸리는 시간(초). 상승량에 따라 최소~최대 사이에서 정한다.")]
        [HideInInspector] public float minHopTime = .2f, maxHopTime = .38f;
        [Tooltip("공중에서 G를 누른 뒤 이 시간(초) 동안 잡을 위치에 오면 잡는다.")]
        [Min(0f)] public float airGrabBuffer = .55f;
        [Tooltip("공중 잡기: 캡슐 중심과 철봉 사이 수평 거리가 이 이내여야 한다(m).")]
        [HideInInspector] public float airCatchHorizontal = .3f;
        [Tooltip("공중 잡기: 이번 프레임에 지나간 높이 구간이 매달린 자세 높이와 이 오차(m) 안에서 겹치면 손이 닿은 것으로 본다.")]
        [HideInInspector] public float airCatchVertical = .1f;
        [Tooltip("현재 속도와 중력으로 양손이 철봉에 닿을 수 있는지 예측하는 시간(초).")]
        [Range(.1f, .5f)] public float airPredictionTime = .3f;
        [Tooltip("실제 위팔+아래팔 길이에 더하는 손바닥/리타기팅 여유(m).")]
        [Range(0f, .15f)] public float airReachMargin = .08f;
        [Min(.02f)] public float airHandReachTime = .08f;
        [Min(.1f)] public float airSettleSpeed = 4.5f;
        [Tooltip("수직 점프 잡기: 점프 시작 모션 후 몸이 떠오르기 시작하는 시간(초).")]
        [HideInInspector] public float hopTakeoffDelay = .08f;

        [Header("위에서 잡기 (손이 철봉보다 위)")]
        [Tooltip("발 기준 철봉 높이 범위(m). 허리 아래~손 높이 근처.")]
        public float kipMinBarHeight = .55f, kipMaxBarHeight = 1.25f;
        [Tooltip("앞쪽 철봉까지의 수평 거리 범위(m). 캡슐 중심 기준.")]
        public float kipMinDistance = .3f, kipMaxDistance = 1.15f;
        [Tooltip("철봉을 짚는 시작 자세로 정렬하는 시간(초). 정렬 거리에 비례해 늘어난다.")]
        [Min(.05f)] public float kipAlignTime = .22f;
        [Tooltip("철봉 방향(좌우)으로 허용하는 어긋남(m).")]
        [Min(0f)] public float kipLateralTolerance = .3f;
        [Tooltip("위에서 잡기 경로 검사에 쓰는 몸통 캡슐 반지름(m). 캐릭터 캡슐(0.5)보다 실제 몸에 가깝게 잡는다.")]
        [Min(.05f)] public float kipBodyRadius = .2f;

        [Header("손 고정")]
        [Range(0f, .1f)] public float maximumCatchCorrection = .08f;
        [Min(.05f)] public float handLockBlendTime = .12f;
        [Min(.1f)] public float settleDuration = .32f;
        // Root-relative hand position in the HangingIdle pose.
        public Vector3 holdHandLocalOffset = new Vector3(.01f, 1.189f, 0f);

        [Header("Grip and swing")]
        [Range(.15f, .7f)] public float gripSpacing = .42f;
        public float gripHeightOffset = .035f, gripBackOffset = .035f;
        [Range(0, 90)] public float maximumSwingArc = 90f;
        public float swingBuildRate = 16f, swingDecayRate = 12f;
        [Tooltip("지상에서 철봉을 잡았을 때 가까운 거리/최대 거리의 초기 흔들림 각도(한쪽 진폭).")]
        [Range(0f, 45f)] public float nearEntrySwingAngle = 6f, farEntrySwingAngle = 28f;
        [Tooltip("입력 없이 진입 관성으로 흔들릴 때 진폭이 절반으로 줄어드는 시간(초).")]
        [Min(.1f)] public float entrySwingHalfLife = 1.2f;
        [Min(.1f)] public float swingCycleDuration = 1.8f;
        public float SwingAngle => swingAngle;
        public float SwingStrength => Mathf.Clamp01(swingAmplitude / Mathf.Max(.01f, maximumSwingArc * .5f));
        public float releaseJumpUpSpeed = 4f, releaseJumpForwardSpeed = 3f;
        [Tooltip("최대 진폭에서 더해지는 전진 속도(m/s). Space를 누른 순간의 흔들림 크기로 결정한다.")]
        [Min(0f)] public float swingJumpForwardBonus = 2.5f;
        [Tooltip("철봉을 놓은 뒤 WASD로 수평 속도를 조절하는 가속도(m/s²). 입력이 없으면 도약 관성을 유지한다.")]
        [Min(0f)] public float exitAirAcceleration = 12f;

        const string KipState = "BarKipDownToHang";
        const string ForwardStartState = "JumpToHang", CloseStartState = "BarCloseStart";
        // 플레이어 아바타에서 샘플링한 각 Start의 이륙/손 접촉 구간이다.
        const float ForwardTakeoff = .1f, ForwardContact = .75f;
        const float CloseTakeoff = .33f, CloseContact = .55f;
        string entryState;
        float startLength, startSpeed, takeoffProgress, contactProgress, flightElapsed;
        float forwardClipLength = .5f, closeClipLength = 1.3333334f;
        Vector3 launchVelocity, preparationVelocity;
        bool contactPending;
        public string EntryPhase => !IsHanging ? "None" : exitPhase != 0 ? "Exit" : hasGrip ? "Hanging" :
            entry == GrabKind.KipDown ? "KipDown" : entry == GrabKind.AirCatch ? "AirReach" : hopLaunched ? "Flight" : "Start";

        PlayerCharacter player;
        Animator animator;
        CharacterController controller;
        Transform left, right, hips, leftUpperArm, rightUpperArm;
        float leftArmLength, rightArmLength;
        float airReachWeight, airIKAppliedWeight, airApproachDeadline, airSettleDuration;
        HangRail rail;
        Collider railCollider;
        bool ignored, idle, hasGrip;
        Vector3 grip, leftGrip, rightGrip, approachVelocity;
        Vector3 settleStart, settleTarget;
        Quaternion approachRotation;
        float elapsed, cooldown, gripTime;
        float swingPhase, swingAmplitude, swingAngle;
        bool entrySwingCoasting;
        int exitPhase; // 0 approach/attached, 1 jump preparation, 2 airborne, 3 landing
        float exitTime, verticalSpeed;
        Vector3 exitVelocity;
        float releaseSwingStrength, exitMoveSpeed;
        float grabBufferedUntil = float.NegativeInfinity;

        // 진입 단계
        GrabKind entry;
        Vector3 entryStart, entryTarget;
        float entryDuration;
        bool kipEntered, hopLaunched;
        Vector3 entryVelocity;

        // Player_TraversalKipDownToHang(0~1, 20구간)의 루트 기준 손 위치에서 마지막 프레임 값을 뺀 곡선.
        // 마지막 프레임 손 위치는 HangingIdle의 holdHandLocalOffset과 같으므로 곡선 + holdHandLocalOffset이 루트 기준 손 위치다.
        // 런타임에는 애니메이션 전(IK 전) 손 위치를 얻을 수 없어 에디터에서 샘플링해 둔다(AirComboModule.UpperHeights와 같은 방식).
        static readonly Vector3[] KipHandCurve =
        {
            new Vector3(0.007f, 0.307f, -0.088f), new Vector3(0.007f, 0.300f, -0.057f), new Vector3(0.006f, 0.289f, 0.010f),
            new Vector3(0.004f, 0.273f, 0.093f), new Vector3(0.004f, 0.254f, 0.166f), new Vector3(0.002f, 0.239f, 0.214f),
            new Vector3(-0.001f, 0.229f, 0.243f), new Vector3(-0.004f, 0.207f, 0.233f), new Vector3(-0.004f, 0.168f, 0.207f),
            new Vector3(-0.004f, 0.124f, 0.175f), new Vector3(-0.002f, 0.089f, 0.156f), new Vector3(-0.002f, 0.059f, 0.136f),
            new Vector3(-0.002f, 0.039f, 0.124f), new Vector3(-0.003f, 0.028f, 0.101f), new Vector3(-0.006f, 0.025f, 0.046f),
            new Vector3(-0.005f, 0.024f, -0.015f), new Vector3(-0.005f, 0.023f, -0.043f), new Vector3(-0.002f, 0.019f, -0.042f),
            new Vector3(0.001f, 0.011f, -0.031f), new Vector3(0.001f, 0.004f, -0.013f), new Vector3(0.000f, 0.000f, 0.000f)
        };

        // 같은 클립(0~1, 10구간)의 루트 기준 엉덩이 위치. 위에서 잡기 경로의 몸통 충돌 검사에 쓴다.
        static readonly Vector3[] KipHipsCurve =
        {
            new Vector3(0.00f, 1.48f, -0.09f), new Vector3(0.00f, 1.47f, -0.07f), new Vector3(0.00f, 1.42f, -0.05f),
            new Vector3(0.00f, 1.29f, -0.03f), new Vector3(-0.01f, 0.92f, -0.03f), new Vector3(-0.01f, 0.51f, -0.02f),
            new Vector3(-0.01f, 0.25f, 0.00f), new Vector3(-0.01f, 0.21f, 0.02f), new Vector3(-0.01f, 0.20f, 0.01f),
            new Vector3(-0.01f, 0.18f, 0.01f), new Vector3(-0.01f, 0.16f, 0.03f)
        };

        static Vector3 SampleCurve(Vector3[] curve, float normalizedTime)
        {
            float sample = Mathf.Clamp01(normalizedTime) * (curve.Length - 1);
            int index = Mathf.Min(Mathf.FloorToInt(sample), curve.Length - 2);
            return Vector3.Lerp(curve[index], curve[index + 1], sample - index);
        }

        Vector3 KipHandOffset(float normalizedTime, Quaternion rotation)
        {
            return rotation * Vector3.Scale(SampleCurve(KipHandCurve, normalizedTime) + holdHandLocalOffset, transform.lossyScale);
        }

        Vector3 KipRoot(Vector3 point, Quaternion rotation, float normalizedTime)
        {
            return point + GripOffsetFor(rotation) - KipHandOffset(normalizedTime, rotation);
        }

        Vector3 KipHips(Vector3 root, Quaternion rotation, float normalizedTime)
        {
            return root + rotation * Vector3.Scale(SampleCurve(KipHipsCurve, normalizedTime), transform.lossyScale);
        }

        /// <summary>
        /// 엉덩이 기준 무릎~머리 높이의 얇은 몸통 캡슐이 자기 자신과 철봉 외의 물체와 겹치지 않는지.
        /// 아래 끝은 서 있을 때 발밑 바닥에 닿지 않도록 무릎 높이(엉덩이 아래 0.55m)까지만 잡는다.
        /// </summary>
        bool BodyClear(Vector3 hips, Collider allowed)
        {
            float s = transform.lossyScale.y;
            foreach (var c in Physics.OverlapCapsule(hips - Vector3.up * .55f * s, hips + Vector3.up * .5f * s, kipBodyRadius, ~0, QueryTriggerInteraction.Ignore))
                if (c != allowed && !c.transform.IsChildOf(transform)) return false;
            return true;
        }

        /// <summary>
        /// 위에서 잡기 전체 경로(서 있는 자세 → 철봉을 짚은 시작 자세 → 아래로 돌아 매달림)에서 몸통이 막히지 않는지 미리 확인한다.
        /// 이 전환은 캐릭터 캡슐 대신 실제 몸에 가까운 몸통 캡슐로 충돌을 판단한다.
        /// </summary>
        bool KipPathClear(Vector3 point, Quaternion rotation, Collider allowed)
        {
            Vector3 standHips = hips != null ? hips.position : transform.position;
            Vector3 startHips = KipHips(KipRoot(point, rotation, 0f), rotation, 0f);
            for (int i = 0; i <= 4; i++)
                if (!BodyClear(Vector3.Lerp(standHips, startHips, i / 4f), allowed)) return false;
            for (int i = 1; i <= 10; i++)
            {
                float n = i / 10f;
                if (!BodyClear(KipHips(KipRoot(point, rotation, n), rotation, n), allowed)) return false;
            }
            return true;
        }

        bool GrabPressed => UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.gKey.wasPressedThisFrame;
        float Gravity => Mathf.Min(-.1f, player.Profile.locomotion.gravity);
        // 철봉에서 놓은 뒤에는 일반 이동 모듈 대신 이 모듈이 비행 속도를 소유한다.
        Vector3 GrabVelocity => IsHanging && exitPhase == 2
            ? exitVelocity + Vector3.up * verticalSpeed : player.Locomotion.TraversalVelocity;
        Vector3 GripOffset => GripOffsetFor(approachRotation);
        Vector3 GripOffsetFor(Quaternion rotation) => Vector3.up * gripHeightOffset - rotation * Vector3.forward * gripBackOffset;

        void Awake()
        {
            player = GetComponent<PlayerCharacter>();
            animator = GetComponent<Animator>();
            controller = GetComponent<CharacterController>();
            if (animator.isHuman)
            {
                left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                right = animator.GetBoneTransform(HumanBodyBones.RightHand);
                hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                var leftElbow = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                var rightElbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                if (leftUpperArm != null && leftElbow != null && left != null)
                    leftArmLength = Vector3.Distance(leftUpperArm.position, leftElbow.position) + Vector3.Distance(leftElbow.position, left.position);
                if (rightUpperArm != null && rightElbow != null && right != null)
                    rightArmLength = Vector3.Distance(rightUpperArm.position, rightElbow.position) + Vector3.Distance(rightElbow.position, right.position);
            }
            if (animator.runtimeAnimatorController != null)
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                {
                    if (clip.name == "Player_TraversalJumpToHang") forwardClipLength = clip.length;
                    if (clip.name == "Player_BarCloseStart") closeClipLength = clip.length;
                }
        }

        #region 갱신

        public bool Tick(float dt)
        {
            if (!isActiveAndEnabled || !controller.enabled) return false;
            cooldown = Mathf.Max(0, cooldown - dt);
            if (IsHanging)
            {
                if (player.IsDead || player.IsHitReacting) { Release(false); return false; }
                if (exitPhase != 0) return TickExit(dt);
                if (rail == null || !rail.isActiveAndEnabled) { MissGrab(entryVelocity); return true; }
                elapsed += dt;
                if (!hasGrip)
                {
                    Vector3 before = transform.position;
                    bool owned = entry == GrabKind.KipDown ? TickKip(dt) : entry == GrabKind.AirCatch ? TickAirReach(dt) : TickHop(dt);
                    if (entry == GrabKind.KipDown && dt > 0f && IsHanging && exitPhase == 0) entryVelocity = (transform.position - before) / dt;
                    return owned;
                }
                gripTime += dt;
                if (entry == GrabKind.AirCatch && !TickAirSettle(dt)) return true;
                // The capsule settles under the fixed grip; it is never chased toward animated hands.
                float settle = Mathf.SmoothStep(0, 1, Mathf.Clamp01(gripTime / settleDuration));
                Vector3 target = Vector3.Lerp(settleStart, settleTarget, settle);
                if (entry == GrabKind.KipDown) controller.Move(Vector3.ClampMagnitude(target - transform.position, 4f * dt));
                if (idle && player.Controls.JumpPressed) { BeginExit(swingAmplitude > 2f); return true; }
                if (idle && (entry == GrabKind.HopUp || gripTime >= (entry == GrabKind.AirCatch ? airSettleDuration : hangIdleBlend))) TickSwing(dt);
                return true;
            }

            bool grounded = controller.isGrounded;
            if (GrabPressed) grabBufferedUntil = Time.time + (grounded ? 0f : airGrabBuffer);
            bool blocked = cooldown > 0 || player.IsDead || player.IsAttacking || player.IsDodging ||
                player.IsHitReacting || player.IsLinkJumping || player.IsAirComboActive ||
                (player.WallRun != null && player.WallRun.IsWallRunning) ||
                (player.WeaponStance != null && (player.WeaponStance.IsDrawn || player.WeaponStance.IsTransitioning)) ||
                left == null || right == null;
            Current = blocked ? default(GrabOpportunity) : Evaluate(grounded);
            // 같은 프레임 또는 공중 입력 유예 시간 안에만 잡는다. 그 전까지는 일반 이동(점프 궤적)이 그대로 유지된다.
            if (blocked || Time.time > grabBufferedUntil) return false;
            if (Current.kind == GrabKind.None) return false;
            grabBufferedUntil = float.NegativeInfinity;
            Begin(Current);
            return true;
        }

        #endregion

        #region 판정

        GrabOpportunity Evaluate(bool grounded, HangRail excludedRail = null)
        {
            var best = default(GrabOpportunity);
            float bestScore = float.PositiveInfinity;
            string reason = "철봉 없음";
            float reasonDistance = float.PositiveInfinity;
            float railDistance = 0f;
            // 거절 이유는 가장 가까운 철봉 것만 남긴다(멀리 있는 철봉의 이유가 덮어쓰지 않게).
            void Note(string r) { if (railDistance <= reasonDistance) { reasonDistance = railDistance; reason = r; } }
            float feet = controller.bounds.min.y;
            foreach (var candidate in HangRail.Active)
            {
                if (candidate == null || candidate == excludedRail) continue;
                Vector3 point;
                if (!candidate.TwoHandGrip(transform.position, gripSpacing + .08f, out point)) { railDistance = Vector3.Distance(candidate.ClosestPoint(transform.position), transform.position); Note("철봉이 양손보다 짧음"); continue; }
                Vector3 flat = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
                railDistance = flat.magnitude;
                Vector3 across = Vector3.Cross(candidate.Axis, Vector3.up).normalized;
                Collider col = candidate.GetComponent<Collider>();
                if (!grounded)
                {
                    GrabOpportunity air; string airReason;
                    if (TryPlanAirCatch(candidate, col, out air, out airReason))
                    {
                        float score = Vector3.Distance((left.position + right.position) * .5f, air.grip) + air.contactTime;
                        if (score < bestScore) { bestScore = score; best = air; }
                    }
                    else Note(airReason);
                    continue;
                }

                // 지상은 Start가 도달할 수 있는 거리, 공중은 실제 접촉 근처를 따로 검사한다.
                float range = interactionDistance;
                Vector3 face = flat.sqrMagnitude > .04f ? (Vector3.Dot(across, flat) >= 0 ? across : -across) :
                    (Vector3.Dot(across, transform.forward) >= 0 ? across : -across);
                var rotation = Quaternion.LookRotation(face);
                Vector3 hangRoot = HangRoot(point, rotation);
                float rise = hangRoot.y - transform.position.y;
                bool overhead = rise >= hangFootClearance;
                if (overhead)
                {
                    if (flat.magnitude > range) { Note("잡기 거리 밖"); continue; }
                    GrabKind kind = GrabKind.None;
                    if (grounded)
                    {
                        Vector3 velocity; float duration;
                        if (rise > maxGroundHangRise) Note("철봉이 너무 높음");
                        else if (!PlanJump(point, rotation, transform.position, col, out velocity, out duration)) Note("Start 도약 경로 또는 속도 제한");
                        else kind = GrabKind.HopUp;
                    }
                    if (kind != GrabKind.None)
                    {
                        if (PathBlocked(controller.bounds.center + Vector3.up * (controller.bounds.extents.y - .1f), point, col)) { Note("손과 철봉 사이가 막힘"); continue; }
                        if (!SpaceClear(hangRoot, col)) { Note("매달릴 공간이 막힘"); continue; }
                        if (flat.magnitude < bestScore) { bestScore = flat.magnitude; best = new GrabOpportunity { kind = kind, rail = candidate, grip = point, facing = rotation, hangRoot = hangRoot }; }
                    }
                    continue;
                }

                // 위에서 잡기: 손이 철봉보다 위에 있고 철봉이 앞쪽에 있어야 한다.
                if (!grounded) continue;
                float h = point.y - feet;
                if (h < kipMinBarHeight || h > kipMaxBarHeight) { Note("위에서 잡을 높이가 아님"); continue; }
                Vector3 toward = Vector3.Dot(across, flat) >= 0 ? across : -across;
                float distance = Vector3.Dot(flat, toward);
                // 철봉 방향(좌우)으로 벗어나 있으면 잡을 위치가 아니다. 끝점으로 당긴 잡기 지점이 옆에 있는 경우를 거른다.
                if ((flat - toward * distance).magnitude > kipLateralTolerance) { Note("철봉 옆으로 벗어남"); continue; }
                if (distance < kipMinDistance || distance > kipMaxDistance) { Note("철봉까지 거리가 맞지 않음"); continue; }
                var kipRotation = Quaternion.LookRotation(toward);
                Vector3 kipHang = HangRoot(point, kipRotation);
                if (PathBlocked(controller.bounds.center, point, col)) { Note("철봉 앞이 막힘"); continue; }
                // 아래로 돌아 내려가는 경로와 최종 매달림 위치가 비어 있어야 한다(평지의 허리 높이 철봉은 여기서 거절).
                if (!SpaceClear(kipHang, col) || !SpaceClear(kipHang + Vector3.up * .5f, col)) { Note("철봉 아래 공간이 막힘"); continue; }
                // 서 있는 자세 → 철봉을 짚은 자세 → 매달림까지 몸통이 지나갈 경로 전체를 확인한다.
                if (!KipPathClear(point, kipRotation, col)) { Note("내려가는 경로가 막힘"); continue; }
                float kipScore = .5f + distance;
                if (kipScore < bestScore) { bestScore = kipScore; best = new GrabOpportunity { kind = GrabKind.KipDown, rail = candidate, grip = point, facing = kipRotation, hangRoot = kipHang }; }
            }
            LastDecision = best.kind == GrabKind.None ? reason : best.kind.ToString();
            return best;
        }

        const float AirTurnSpeed = 1440f;

        void AirGripPoints(HangRail target, Vector3 point, Quaternion rotation, out Vector3 l, out Vector3 r)
        {
            Vector3 axis = target.Axis;
            if (Vector3.Dot(axis, rotation * Vector3.right) < 0f) axis = -axis;
            l = target.ClosestPoint(point - axis * gripSpacing * .5f) + GripOffsetFor(rotation);
            r = target.ClosestPoint(point + axis * gripSpacing * .5f) + GripOffsetFor(rotation);
        }

        bool ArmsCanReach(Vector3 l, Vector3 r, Vector3 displacement, Collider allowed, float predictionTime = 0f, Quaternion rotation = default, bool reachPose = true)
        {
            if (leftUpperArm == null || rightUpperArm == null || leftArmLength <= 0f || rightArmLength <= 0f) return false;
            Vector3 ls = leftUpperArm.position + displacement, rs = rightUpperArm.position + displacement;
            if (predictionTime > 0f)
            {
                // 현재 점프의 웅크린 어깨뿐 아니라, 실제 JumpToHang 도달 포즈의 어깨 위치도 예측한다.
                // 현재 아바타에서 복사 클립의 0.25 진행도를 샘플링한 루트 기준 위치다.
                Vector3 root = transform.position + displacement;
                float blend = reachPose ? Mathf.Clamp01(predictionTime / .1f) : 1f;
                Vector3 localLeft = reachPose ? new Vector3(-.22f, .61f, .12f) : transform.InverseTransformPoint(leftUpperArm.position);
                Vector3 localRight = reachPose ? new Vector3(.14f, .68f, .10f) : transform.InverseTransformPoint(rightUpperArm.position);
                ls = Vector3.Lerp(ls, root + rotation * Vector3.Scale(localLeft, transform.lossyScale), blend);
                rs = Vector3.Lerp(rs, root + rotation * Vector3.Scale(localRight, transform.lossyScale), blend);
            }
            return Vector3.Distance(ls, l) <= leftArmLength + airReachMargin &&
                Vector3.Distance(rs, r) <= rightArmLength + airReachMargin &&
                !PathBlocked(ls, l, allowed) && !PathBlocked(rs, r, allowed) &&
                !PathBlocked(left.position + displacement, l, allowed) && !PathBlocked(right.position + displacement, r, allowed);
        }

        bool TryPlanAirCatch(HangRail candidate, Collider allowed, out GrabOpportunity opportunity, out string reason)
        {
            opportunity = default;
            reason = "공중: 양손이 닿을 비행 궤적이 없음";
            if (leftUpperArm == null || rightUpperArm == null) return false;
            Vector3 velocity = GrabVelocity;
            float horizon = Mathf.Max(airPredictionTime, airHandReachTime);
            int steps = Mathf.Max(4, Mathf.CeilToInt(horizon / .01f));
            Vector3 previous = transform.position;
            for (int i = 0; i <= steps; i++)
            {
                float t = horizon * i / steps;
                Vector3 delta = velocity * t + Vector3.up * (.5f * Gravity * t * t);
                Vector3 root = transform.position + delta;
                if (i > 0 && (!SpaceClear(root, allowed) || !CapsulePathClear(previous, root, allowed)))
                { reason = "공중: 비행 경로가 막힘"; return false; }
                previous = root;
                Vector3 point;
                if (!candidate.TwoHandGrip((leftUpperArm.position + rightUpperArm.position) * .5f + delta, gripSpacing + .08f, out point)) continue;
                Vector3 across = Vector3.Cross(candidate.Axis, Vector3.up).normalized;
                Vector3 flat = Vector3.ProjectOnPlane(point - transform.position - delta, Vector3.up);
                Vector3 facing = flat.sqrMagnitude > .04f ? (Vector3.Dot(across, flat) >= 0f ? across : -across) :
                    (Vector3.Dot(across, transform.forward) >= 0f ? across : -across);
                Quaternion rotation = Quaternion.LookRotation(facing);
                Vector3 l, r; AirGripPoints(candidate, point, rotation, out l, out r);
                // 팔을 뻗고 돌아설 시간을 포함한 실제 비행 위치에서 도달할 수 있어야 한다.
                float ready = Mathf.Max(airHandReachTime, Quaternion.Angle(transform.rotation, rotation) / AirTurnSpeed);
                if (t + .001f < ready) continue;
                bool reachAnimation = !ArmsCanReach(l, r, delta, allowed, t, rotation, false);
                if (reachAnimation && !ArmsCanReach(l, r, delta, allowed, t, rotation)) continue;
                Vector3 hanging = HangRoot(point, rotation);
                if (!SpaceClear(hanging, allowed) || !CapsulePathClear(root, hanging, allowed))
                { reason = "공중: 비행 또는 매달림 공간이 막힘"; continue; }
                opportunity = new GrabOpportunity { kind = GrabKind.AirCatch, rail = candidate, grip = point,
                    facing = rotation, hangRoot = hanging, contactTime = t, reachAnimation = reachAnimation };
                return true;
            }
            return false;
        }

        bool TickAirReach(float dt)
        {
            Vector3 l = leftGrip + GripOffset, r = rightGrip + GripOffset;
            // 이전 Animator 평가에서 IK까지 적용된 실제 손이 닿았을 때만 매달림을 확정한다.
            if (airIKAppliedWeight >= .5f && Quaternion.Angle(transform.rotation, approachRotation) < 8f &&
                Vector3.Distance(left.position, l) <= handContactTolerance && Vector3.Distance(right.position, r) <= handContactTolerance &&
                !PathBlocked(left.position, l, railCollider) && !PathBlocked(right.position, r, railCollider))
            {
                if (!SpaceClear(HangRoot(grip, approachRotation), railCollider) ||
                    !CapsulePathClear(transform.position, HangRoot(grip, approachRotation), railCollider))
                { LastDecision = "공중: 접촉 후 매달릴 경로가 막힘"; MissGrab(entryVelocity); return true; }
                Attach(); return true;
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, approachRotation, AirTurnSpeed * dt);
            Vector3 before = transform.position;
            Vector3 movement = entryVelocity * dt + Vector3.up * (.5f * Gravity * dt * dt);
            entryVelocity += Vector3.up * Gravity * dt;
            if (!SpaceClear(before + movement, railCollider) || !CapsulePathClear(before, before + movement, railCollider))
            { LastDecision = "공중: 손을 뻗는 중 경로가 막힘"; MissGrab(entryVelocity); return true; }
            var flags = controller.Move(movement);
            if ((flags & (CollisionFlags.Above | CollisionFlags.Sides | CollisionFlags.Below)) != 0)
            {
                if ((flags & CollisionFlags.Above) != 0) entryVelocity.y = Mathf.Min(0f, entryVelocity.y);
                if ((flags & CollisionFlags.Sides) != 0) entryVelocity = Vector3.up * entryVelocity.y;
                LastDecision = "공중: 손 접촉 전 충돌"; MissGrab(entryVelocity); return true;
            }
            bool reachable = ArmsCanReach(l, r, Vector3.zero, railCollider);
            airReachWeight = Mathf.MoveTowards(airReachWeight, reachable ? 1f : 0f, dt / Mathf.Max(.02f, airHandReachTime));
            if (elapsed > airApproachDeadline)
            { LastDecision = "공중: 손 접촉 실패"; MissGrab(entryVelocity); }
            return true;
        }

        void ApplyAirReachIK(Vector3 l, Vector3 r)
        {
            float alignment = Mathf.InverseLerp(90f, 0f, Quaternion.Angle(transform.rotation, approachRotation));
            airIKAppliedWeight = rail != null && ArmsCanReach(l, r, Vector3.zero, railCollider) ? airReachWeight * alignment : 0f;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, airIKAppliedWeight);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, airIKAppliedWeight);
            if (airIKAppliedWeight <= 0f) return;
            animator.SetIKPosition(AvatarIKGoal.LeftHand, l + animator.GetIKPosition(AvatarIKGoal.LeftHand) - left.position);
            animator.SetIKPosition(AvatarIKGoal.RightHand, r + animator.GetIKPosition(AvatarIKGoal.RightHand) - right.position);
        }

        bool TickAirSettle(float dt)
        {
            float progress = Mathf.SmoothStep(0, 1, Mathf.Clamp01(gripTime / airSettleDuration));
            Vector3 target = Vector3.Lerp(settleStart, settleTarget, progress);
            Vector3 movement = Vector3.ClampMagnitude(target - transform.position, airSettleSpeed * dt);
            Vector3 before = transform.position;
            if (!SpaceClear(before + movement, railCollider) || !CapsulePathClear(before, before + movement, railCollider))
            { LastDecision = "공중: 손 고정 후 내려갈 공간이 막힘"; MissGrab(entryVelocity); return false; }
            var flags = controller.Move(movement);
            if (dt > 0f) entryVelocity = (transform.position - before) / dt;
            if ((flags & (CollisionFlags.Above | CollisionFlags.Sides)) != 0 || Vector3.Distance(transform.position, before + movement) > maximumCatchCorrection)
            { LastDecision = "공중: 매달림 자세 전환 중 충돌"; MissGrab(entryVelocity); return false; }
            return true;
        }

        bool UseCloseStart(Vector3 point, Vector3 root) => Vector3.ProjectOnPlane(point - root, Vector3.up).magnitude <= closeStartDistance;

        Vector3 ContactOffset(bool close, Quaternion rotation)
        {
            Vector3 local = close ? new Vector3(-.003f, 1.636f, .122f) : new Vector3(.030f, .589f, .670f);
            return rotation * Vector3.Scale(local, transform.lossyScale);
        }

        /// <summary>Start의 이륙과 손 접촉 사이 시간에 도착하는 속도만 허용한다. 목표는 이륙 때 확정한다.</summary>
        bool PlanJump(Vector3 point, Quaternion rotation, Vector3 from, Collider allowed, out Vector3 velocity, out float duration)
        {
            bool close = UseCloseStart(point, entry == GrabKind.HopUp && IsApproaching ? entryStart : from);
            float speed = Mathf.Max(.1f, close ? closeStartSpeed : forwardStartSpeed);
            duration = (close ? CloseContact - CloseTakeoff : ForwardContact - ForwardTakeoff) *
                (close ? closeClipLength : forwardClipLength) / speed;
            Vector3 target = point + GripOffsetFor(rotation) - ContactOffset(close, rotation);
            velocity = (target - from) / duration - Vector3.up * (.5f * Gravity * duration);
            if (!animator.HasState(0, Animator.StringToHash("Base Layer." + (close ? CloseStartState : ForwardStartState))) ||
                velocity.y <= 0f || velocity.y > maxLaunchSpeed ||
                Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude > maxApproachSpeed) return false;
            Vector3 previous = from;
            int steps = Mathf.Max(8, Mathf.CeilToInt(duration / .025f));
            for (int i = 1; i <= steps; i++)
            {
                float t = duration * i / steps;
                Vector3 next = from + velocity * t + Vector3.up * (.5f * Gravity * t * t);
                if (!SpaceClear(next, allowed) || !CapsulePathClear(previous, next, allowed)) return false;
                previous = next;
            }
            return true;
        }

        bool CapsulePathClear(Vector3 from, Vector3 to, Collider allowed)
        {
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < .000001f) return true;
            Vector3 scale = transform.lossyScale;
            float radius = Mathf.Max(.05f, controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) - controller.skinWidth * .5f);
            float half = Mathf.Max(0f, controller.height * Mathf.Abs(scale.y) * .5f - radius);
            Vector3 center = from + transform.TransformVector(controller.center);
            foreach (var hit in Physics.CapsuleCastAll(center + Vector3.up * half, center - Vector3.up * half, radius,
                delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider != allowed && !hit.transform.IsChildOf(transform)) return false;
            return true;
        }

        /// <summary>grip을 잡고 HangingIdle 자세로 매달렸을 때의 캡슐(Transform) 위치.</summary>
        Vector3 HangRoot(Vector3 point, Quaternion rotation)
        {
            Vector3 offset = Vector3.up * gripHeightOffset - rotation * Vector3.forward * gripBackOffset;
            return point + offset - rotation * Vector3.Scale(holdHandLocalOffset, transform.lossyScale);
        }

        /// <summary>root 위치에 캡슐을 두었을 때 자기 자신과 철봉 외의 물체와 겹치지 않는지.</summary>
        bool SpaceClear(Vector3 root, Collider allowed)
        {
            float radius = controller.radius * .95f;
            Vector3 center = root + transform.rotation * controller.center;
            float half = Mathf.Max(0f, controller.height * .5f - radius);
            foreach (var c in Physics.OverlapCapsule(center + Vector3.up * half, center - Vector3.up * half, radius, ~0, QueryTriggerInteraction.Ignore))
                if (c != allowed && !c.transform.IsChildOf(transform)) return false;
            return true;
        }

        bool PathBlocked(Vector3 from, Vector3 to, Collider allowed)
        {
            foreach (var hit in Physics.RaycastAll(from, to - from, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.collider != allowed) return true;
            return false;
        }

        #endregion

        #region 진입

        void Begin(GrabOpportunity op)
        {
            Vector3 incomingVelocity = GrabVelocity;
            // 다음 철봉으로 넘어갈 때 이전 철봉의 충돌 무시를 먼저 되돌린다.
            if (ignored && railCollider != null) Physics.IgnoreCollision(controller, railCollider, false);
            ignored = false;
            rail = op.rail; grip = op.grip; approachRotation = op.facing;
            railCollider = rail.GetComponent<Collider>();
            Vector3 axis = rail.Axis;
            if (Vector3.Dot(axis, approachRotation * Vector3.right) < 0) axis = -axis;
            leftGrip = rail.ClosestPoint(grip - axis * gripSpacing * .5f);
            rightGrip = rail.ClosestPoint(grip + axis * gripSpacing * .5f);
            // 준비 구간에서는 기존 수평 속도를 짧게 감속하고, 이륙 시 실제 위치에서 다시 도약을 계산한다.
            approachVelocity = incomingVelocity;
            player.Combat.CancelForDamage(); player.AirCombo.OnDisable();
            IsHanging = true; hasGrip = false; idle = false;
            elapsed = gripTime = 0; exitPhase = 0; contactPending = false;
            swingPhase = swingAmplitude = swingAngle = 0; entrySwingCoasting = false;
            entry = op.kind; entryStart = transform.position; entryTarget = op.hangRoot;
            IgnoreRailCollision();
            if (entry == GrabKind.KipDown)
            {
                kipEntered = false;
                float alignDistance = Vector3.Distance(Flat3(entryStart), Flat3(grip));
                entryDuration = Mathf.Min(kipAlignTime + alignDistance * .12f, kipAlignTime * 2f);
                animator.CrossFadeInFixedTime("Base Layer." + KipState, entryDuration, 0, 0f);
                return;
            }
            if (entry == GrabKind.AirCatch)
            {
                // 추가 도약이나 목표점 추적 없이 진입 속도와 중력을 이어가며 팔만 먼저 뻗는다.
                entryVelocity = approachVelocity;
                airReachWeight = airIKAppliedWeight = 0f;
                airApproachDeadline = Mathf.Max(.3f, op.contactTime + airHandReachTime + .2f);
                animator.SetFloat("HangEntrySpeed", 1f);
                if (op.reachAnimation)
                    animator.CrossFadeInFixedTime("Base Layer." + ForwardStartState, .1f, 0, forwardClipLength * .2f);
                return;
            }
            bool close = UseCloseStart(grip, entryStart);
            entryState = close ? CloseStartState : ForwardStartState;
            startLength = close ? closeClipLength : forwardClipLength;
            startSpeed = Mathf.Max(.1f, close ? closeStartSpeed : forwardStartSpeed);
            takeoffProgress = close ? CloseTakeoff : ForwardTakeoff;
            contactProgress = close ? CloseContact : ForwardContact;
            hopLaunched = false; flightElapsed = 0f; entryVelocity = Vector3.zero;
            // 준비 중 철봉을 지나쳤다가 반대로 뛰지 않도록, 남은 도약 위치까지의 거리 안에서 감속한다.
            Vector3 forward = approachRotation * Vector3.forward;
            Vector3 contactRoot = grip + GripOffset - ContactOffset(close, approachRotation);
            float preparationTime = takeoffProgress * startLength / startSpeed;
            float stoppingRoom = Mathf.Max(0f, Vector3.Dot(contactRoot - entryStart, forward));
            float preparationSpeed = Mathf.Min(3f, Mathf.Max(0f, Vector3.Dot(approachVelocity, forward)),
                stoppingRoom * 2f / Mathf.Max(.01f, preparationTime));
            preparationVelocity = forward * preparationSpeed;
            animator.SetFloat("HangEntrySpeed", startSpeed);
            animator.CrossFadeInFixedTime("Base Layer." + entryState, .06f, 0, 0f);
        }

        static Vector3 Flat3(Vector3 v) { v.y = 0; return v; }

        AnimatorStateInfo EntryStateInfo()
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(entryState))
                state = animator.GetNextAnimatorStateInfo(0);
            return state;
        }

        /// <summary>애니메이터 진행도에 맞춰 도약한다. 손 접촉은 IK 평가 시 실제 양손 위치로 확정한다.</summary>
        bool TickHop(float dt)
        {
            var state = EntryStateInfo();
            if (!state.IsName(entryState))
            {
                if (elapsed > .3f) MissGrab(entryVelocity);
                return true;
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, approachRotation, 720f * dt);
            // 다음 애니메이터 평가에서 보일 자세와 같은 시각으로 이동을 진행한다.
            float n = state.normalizedTime + dt * startSpeed * animator.speed / startLength;
            if (n < takeoffProgress)
            {
                float remaining = 1f - Mathf.Clamp01(n / takeoffProgress);
                controller.Move((preparationVelocity * remaining - Vector3.up * 2f) * dt);
                return true;
            }
            if (!hopLaunched)
            {
                if (Vector3.ProjectOnPlane(grip - transform.position, Vector3.up).magnitude > interactionDistance + .05f ||
                    !PlanJump(grip, approachRotation, transform.position, railCollider, out launchVelocity, out entryDuration))
                {
                    LastDecision = "이륙 위치에서 도약 불가";
                    if (controller.isGrounded) { Release(false); animator.CrossFadeInFixedTime("Base Layer.Idle", .1f, 0, 0f); }
                    else MissGrab(approachVelocity);
                    return true;
                }
                hopLaunched = true; entryTarget = transform.position;
            }
            flightElapsed = Mathf.Max(0f, (n - takeoffProgress) * startLength / startSpeed);
            float time = Mathf.Min(flightElapsed, entryDuration + .12f);
            Vector3 target = entryTarget + launchVelocity * time + Vector3.up * (.5f * Gravity * time * time);
            Vector3 before = transform.position;
            var flags = controller.Move(target - before);
            entryVelocity = launchVelocity + Vector3.up * Gravity * time;
            if ((flags & (CollisionFlags.Above | CollisionFlags.Sides)) != 0 ||
                Vector3.Distance(transform.position, target) > .08f)
            {
                if ((flags & CollisionFlags.Above) != 0) entryVelocity.y = Mathf.Min(0f, entryVelocity.y);
                if ((flags & CollisionFlags.Sides) != 0) entryVelocity = Vector3.up * entryVelocity.y;
                LastDecision = "도약 중 경로가 막힘"; MissGrab(entryVelocity); return true;
            }
            contactPending = n >= contactProgress - .04f;
            if (n > contactProgress + .14f) { LastDecision = "손 접촉 실패"; MissGrab(entryVelocity); }
            return true;
        }

        /// <summary>
        /// 위에서 잡기: 철봉을 짚는 시작 자세로 정렬한 뒤, 손을 철봉에 고정한 채 캡슐이 손 기준으로 따라가며
        /// 몸이 철봉 아래로 돌아 내려간다. 클립의 이동은 포즈에 들어 있어 루트 모션을 쓰지 않는다.
        /// </summary>
        bool TickKip(float dt)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, approachRotation, 540f * dt);
            var state = KipStateInfo();
            if (state.IsName(KipState)) kipEntered = true;
            // 손이 철봉에 있도록 클립 진행도에 맞는 루트 위치를 정한다. 정렬 구간에서는 시작 위치에서 서서히 옮긴다.
            float n = state.IsName(KipState) ? state.normalizedTime : 0f;
            Vector3 target = KipRoot(grip, approachRotation, n);
            float a = Mathf.Clamp01(elapsed / entryDuration);
            // 수평으로 먼저 철봉 쪽으로 옮기고 높이는 뒤따르게 해 난간 모서리에 걸리지 않게 한다.
            float horizontal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(a * 1.6f));
            float vertical = a * a;
            Vector3 desired = new Vector3(Mathf.Lerp(entryStart.x, target.x, horizontal), Mathf.Lerp(entryStart.y, target.y, vertical), Mathf.Lerp(entryStart.z, target.z, horizontal));
            Vector3 next = Vector3.MoveTowards(transform.position, desired, 8f * dt);
            // 캐릭터 캡슐(반지름 0.5)은 실제 몸보다 두꺼워 난간 모서리에 걸린다. 그래서 이 전환에서는 캡슐 대신
            // 몸통 캡슐로 충돌을 본다. 경로 전체는 진입 전에 확인했고, 진입 중 새로 막히면(움직이는 물체 등) 중단한다.
            if (!BodyClear(KipHips(next, approachRotation, n), railCollider)) { LastDecision = "내려가는 중 막힘"; MissGrab(entryVelocity); return true; }
            transform.position = next;
            Physics.SyncTransforms();
            if (kipEntered && !animator.IsInTransition(0) && (!state.IsName(KipState) || state.normalizedTime >= .98f)) { Attach(); return true; }
            if (elapsed > entryDuration + 2f) MissGrab(entryVelocity);
            return true;
        }

        AnimatorStateInfo KipStateInfo()
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(KipState))
                state = animator.GetNextAnimatorStateInfo(0);
            return state;
        }

        /// <summary>양손을 철봉에 고정하고 매달림 상태로 들어간다. 진입 운동량으로 스윙을 시작한다.</summary>
        void Attach()
        {
            hasGrip = true; gripTime = 0; idle = true;
            settleStart = transform.position;
            settleTarget = HangRoot(grip, approachRotation);
            airSettleDuration = Mathf.Max(settleDuration, Vector3.Distance(settleStart, settleTarget) * 1.5f / Mathf.Max(.1f, airSettleSpeed));
            Vector3 incomingVelocity = entry == GrabKind.KipDown ? approachVelocity : entryVelocity;
            float incoming = Vector3.Dot(incomingVelocity, approachRotation * Vector3.forward);
            float limit = Mathf.Clamp(maximumSwingArc, 0, 90) * .5f;
            entrySwingCoasting = entry == GrabKind.HopUp;
            if (entrySwingCoasting)
            {
                // 이륙 직전 감속량이나 Start 클립 분기와 관계없이 G를 누른 진입 거리를 사용한다.
                float distance = Vector3.ProjectOnPlane(grip - entryStart, Vector3.up).magnitude;
                float amount = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance / Mathf.Max(.1f, interactionDistance)));
                swingAmplitude = Mathf.Clamp(Mathf.Lerp(nearEntrySwingAngle,
                    Mathf.Max(nearEntrySwingAngle, farEntrySwingAngle), amount), 0f, limit);
                // 손 아래 몸을 진입 속도 방향으로 먼저 보내며, 접촉 순간 각도는 0에서 시작한다.
                swingPhase = incoming >= 0 ? .5f : 0f;
            }
            else
            {
                swingAmplitude = Mathf.Min(Mathf.Abs(incoming) * 3f, 8f, limit);
                swingPhase = incoming >= 0 ? 0f : .5f;
            }
            if (entry == GrabKind.AirCatch)
            {
                swingAmplitude = Mathf.Min(Mathf.Max(3f, Mathf.Abs(incoming) * 5f), farEntrySwingAngle, limit);
                entrySwingCoasting = true;
                swingPhase = incoming >= 0 ? .5f : 0f;
            }
            swingAngle = 0f;
            approachVelocity = Vector3.zero;
            animator.CrossFadeInFixedTime("Base Layer.HangingIdle", entry == GrabKind.AirCatch ? airSettleDuration : hangIdleBlend, 0, 0);
            contactPending = false;
        }

        /// <summary>잡기에 실패하면 직전 운동량(velocity)을 그대로 이어 받아 떨어진다. 공중에서 갑자기 멈추지 않는다.</summary>
        void MissGrab(Vector3 velocity)
        {
            hasGrip = false; exitPhase = 2; exitTime = 0;
            verticalSpeed = velocity.y;
            exitVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
            exitMoveSpeed = Mathf.Max(player.Profile.locomotion.walkSpeed, exitVelocity.magnitude);
            animator.CrossFadeInFixedTime("Base Layer.HangFall", .1f, 0, 0);
        }

        #endregion

        #region 매달림

        void TickSwing(float dt)
        {
            float input = player.Controls.Move.y;
            float limit = Mathf.Clamp(maximumSwingArc, 0, 90) * .5f;
            if (entrySwingCoasting && Mathf.Abs(input) <= .1f)
            {
                // 프레임 수와 무관한 감쇠로 앞뒤 왕복을 남긴다. W/S 조작 후에는 기존 증폭/제동을 사용한다.
                swingAmplitude *= Mathf.Pow(.5f, dt / Mathf.Max(.1f, entrySwingHalfLife));
                if (swingAmplitude < .1f) { swingAmplitude = 0f; entrySwingCoasting = false; }
            }
            else
            {
                entrySwingCoasting = false;
                swingAmplitude = Mathf.MoveTowards(swingAmplitude, input > .1f ? limit : 0,
                    (input > .1f ? swingBuildRate : input < -.1f ? swingDecayRate * 2 : swingDecayRate) * dt);
            }
            // Preserve the established W pump / S brake cadence.
            swingPhase = Mathf.Repeat(swingPhase + dt / 1.8f, 1f);
            swingAngle = Mathf.Clamp(Mathf.Sin(swingPhase * Mathf.PI * 2) * swingAmplitude, -limit, limit);
        }

        public void ApplyHandIK(int layerIndex)
        {
            if (layerIndex != 0 || !animator.isHuman) return;
            Vector3 l = leftGrip + GripOffset, r = rightGrip + GripOffset;
            if (IsApproaching && entry == GrabKind.AirCatch)
            {
                ApplyAirReachIK(l, r);
                return;
            }
            if (IsApproaching && entry == GrabKind.KipDown && rail != null && controller.enabled)
            {
                // 위에서 잡기: 캡슐은 KipHandCurve로 손이 철봉에 오도록 움직이고,
                // IK는 정렬이 진행될수록 남은 오차만큼 손을 철봉에 고정한다(몸 위치는 건드리지 않음).
                float lockWeight = kipEntered ? Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / entryDuration)) : 0f;
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, lockWeight);
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, lockWeight);
                if (lockWeight <= 0f) return;
                animator.SetIKPosition(AvatarIKGoal.LeftHand, l);
                animator.SetIKPosition(AvatarIKGoal.RightHand, r);
                return;
            }
            if (IsApproaching && entry == GrabKind.HopUp && hopLaunched && contactPending && rail != null)
            {
                var state = EntryStateInfo();
                float error = Mathf.Max(Vector3.Distance(left.position, l), Vector3.Distance(right.position, r));
                if (state.IsName(entryState) && state.normalizedTime >= contactProgress - .04f &&
                    error <= handContactTolerance && Quaternion.Angle(transform.rotation, approachRotation) < 10f &&
                    !PathBlocked(left.position, l, railCollider) && !PathBlocked(right.position, r, railCollider)) Attach();
            }
            bool attached = HasGrip && rail != null && left != null && right != null && controller.enabled;
            float weight = attached ? Mathf.SmoothStep(0, 1, Mathf.Clamp01(gripTime / handLockBlendTime)) : 0f;
            if (attached && entry == GrabKind.AirCatch) weight = 1f;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
            if (!attached) return;
            // After contact, pin the pose around the fixed hands while the capsule settles underneath.
            // 큰 거리를 IK로 숨기지 않도록 몸 위치 보정량을 제한한다.
            // 몸 위치를 조정하기 전에 손바닥-손목 오프셋을 읽어 보정량이 IK 목표에 중복되지 않게 한다.
            Vector3 airLeftOffset = entry == GrabKind.AirCatch ? animator.GetIKPosition(AvatarIKGoal.LeftHand) - left.position : Vector3.zero;
            Vector3 airRightOffset = entry == GrabKind.AirCatch ? animator.GetIKPosition(AvatarIKGoal.RightHand) - right.position : Vector3.zero;
            Vector3 poseCorrection = ((l + r) - (left.position + right.position)) * .5f;
            if (entry == GrabKind.HopUp)
            {
                // Start→Idle의 손 자세 변화만큼 캡슐을 옮겨 고정된 손 아래로 몸을 잇는다.
                // 목표로 끌어오는 진입 이동이 아니라, 이미 접촉한 손을 기준으로 한 자세 전환이다.
                Vector3 before = transform.position;
                // 첫 접촉 오차는 손 IK와 같은 시간 동안 흡수해 고프레임에서도 한 프레임 스냅이 생기지 않게 한다.
                Vector3 correction = poseCorrection * weight;
                if (poseCorrection.magnitude > .35f || !CapsulePathClear(before, before + correction, railCollider))
                { LastDecision = "손 고정 전환 경로가 막힘"; MissGrab(Vector3.zero); return; }
                controller.Move(correction);
                Vector3 applied = transform.position - before;
                animator.bodyPosition += applied;
                if ((applied - correction).magnitude > maximumCatchCorrection)
                { LastDecision = "손 고정 공간 부족"; MissGrab(Vector3.zero); return; }
            }
            else
            {
                float bodyWeight = entry == GrabKind.AirCatch ? Mathf.SmoothStep(0, 1, Mathf.Clamp01(gripTime / airSettleDuration)) : weight;
                animator.bodyPosition += Vector3.ClampMagnitude(poseCorrection, .25f) * bodyWeight;
            }
            if (idle && exitPhase == 0 && (entry == GrabKind.HopUp || gripTime >= (entry == GrabKind.AirCatch ? airSettleDuration : hangIdleBlend)))
            {
                Vector3 pivot = (l + r) * .5f;
                Quaternion rotation = Quaternion.AngleAxis(swingAngle, transform.right);
                animator.bodyPosition = pivot + rotation * (animator.bodyPosition - pivot);
                animator.bodyRotation = rotation * animator.bodyRotation;
            }
            Vector3 leftGoalOffset = entry == GrabKind.AirCatch ? airLeftOffset : animator.GetIKPosition(AvatarIKGoal.LeftHand) - left.position;
            Vector3 rightGoalOffset = entry == GrabKind.AirCatch ? airRightOffset : animator.GetIKPosition(AvatarIKGoal.RightHand) - right.position;
            animator.SetIKPosition(AvatarIKGoal.LeftHand, l + leftGoalOffset);
            animator.SetIKPosition(AvatarIKGoal.RightHand, r + rightGoalOffset);
        }

        void IgnoreRailCollision()
        {
            if (railCollider == null) return;
            ignored = !Physics.GetIgnoreCollision(controller, railCollider);
            if (ignored) Physics.IgnoreCollision(controller, railCollider, true);
        }

        #endregion

        #region 이탈

        void BeginExit(bool jump)
        {
            exitTime = 0;
            releaseSwingStrength = jump ? SwingStrength : 0f;
            if (jump) { exitPhase = 1; animator.CrossFadeInFixedTime("Base Layer.HangJumpStart", .12f, 0, 0); }
            else Detach(false);
        }

        void Detach(bool jump)
        {
            exitPhase = 2; exitTime = 0; verticalSpeed = jump ? releaseJumpUpSpeed : 0;
            float launchSpeed = Mathf.Max(0f, releaseJumpForwardSpeed + swingJumpForwardBonus * releaseSwingStrength);
            exitVelocity = jump ? transform.forward * launchSpeed : Vector3.zero;
            exitMoveSpeed = Mathf.Max(player.Profile.locomotion.walkSpeed, jump ? launchSpeed : 0f);
            if (!jump) animator.CrossFadeInFixedTime("Base Layer.HangFall", .1f, 0, 0);
        }

        void SteerExit(float dt)
        {
            Vector2 input = Vector2.ClampMagnitude(player.Controls.Move, 1f);
            if (input.sqrMagnitude <= .01f) return;
            Transform basis = player.CameraTransform;
            Vector3 forward = Vector3.ProjectOnPlane(basis != null ? basis.forward : transform.forward, Vector3.up).normalized;
            Vector3 desired = forward * input.y + Vector3.Cross(Vector3.up, forward) * input.x;
            exitVelocity = Vector3.MoveTowards(exitVelocity, desired * exitMoveSpeed, exitAirAcceleration * dt);
            if (exitVelocity.sqrMagnitude > .01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(exitVelocity),
                    player.Profile.locomotion.rotationSpeed * dt);
        }

        bool WantsExitMovement => player.Controls.Move.sqrMagnitude > .01f || player.Controls.JumpPressed;

        bool TickExit(float dt)
        {
            exitTime += dt;
            if (exitPhase <= 2 && GrabPressed) grabBufferedUntil = Time.time + airGrabBuffer;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (exitPhase == 1)
            {
                if ((state.IsName("HangJumpStart") && state.normalizedTime >= .45f) || exitTime > .8f) Detach(true);
                return true;
            }
            if (exitPhase == 2)
            {
                SteerExit(dt);
                // G를 누른 비행 중에만 기존 양팔 도달/궤적 검사를 재사용한다. 방금 놓은 철봉은 제외한다.
                if (!controller.isGrounded && Time.time <= grabBufferedUntil)
                {
                    Current = Evaluate(false, rail);
                    if (Current.kind == GrabKind.AirCatch)
                    {
                        grabBufferedUntil = float.NegativeInfinity;
                        Begin(Current);
                        return true;
                    }
                }
                Vector3 movement = (exitVelocity + Vector3.up * verticalSpeed) * dt + Vector3.up * (.5f * Gravity * dt * dt);
                verticalSpeed += Gravity * dt;
                var flags = controller.Move(movement);
                if ((flags & CollisionFlags.Above) != 0) verticalSpeed = Mathf.Min(0, verticalSpeed);
                if ((flags & CollisionFlags.Sides) != 0) exitVelocity = Vector3.zero;
                if (verticalSpeed <= 0 && controller.isGrounded)
                {
                    if (WantsExitMovement) { Release(false); return false; }
                    exitPhase = 3; exitTime = 0;
                    animator.CrossFadeInFixedTime("Base Layer.HangLand", .08f, 0, 0);
                }
                else if (!animator.IsInTransition(0) &&
                    (state.IsName("HangJumpStart") || state.IsName("HangJumpAir") || state.IsName("JumpToHang")) && state.normalizedTime >= .9f)
                    animator.CrossFadeInFixedTime("Base Layer.HangFall", .1f, 0, 0);
                return true;
            }
            // 착지 모션 도중에도 입력이 생기면 같은 프레임에 일반 이동으로 넘긴다.
            if (WantsExitMovement) { Release(false); return false; }
            controller.Move(Vector3.down * 2f * dt);
            if ((state.IsName("HangLand") && state.normalizedTime >= .9f) || exitTime > 1.8f)
            {
                Release(false);
                animator.CrossFadeInFixedTime("Base Layer.Idle", .15f, 0, 0);
            }
            return true;
        }

        public void Release(bool animate)
        {
            if (!IsHanging) return;
            IsHanging = false; hasGrip = false; cooldown = .6f; exitPhase = 0; contactPending = false; entrySwingCoasting = false;
            if (ignored && railCollider != null) Physics.IgnoreCollision(controller, railCollider, false);
            ignored = false; rail = null;
            if (player != null && player.Locomotion != null) player.Locomotion.ResumeAirDodge();
            if (animate) animator.CrossFadeInFixedTime("Base Layer.LinkJumpLoop", .1f, 0, 0);
        }

        void OnDisable() { Release(false); }

        #endregion
    }
}
