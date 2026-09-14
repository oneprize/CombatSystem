using UnityEngine;
using ParrySystem.Player;

public class PlayerController : MonoBehaviour
{
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float rotationSpeed = 720f;
    public float jumpForce = 5f;
    public float gravity = -9.81f;
    public float groundCheckDistance = 0.2f; // 바닥 체크용 거리
    private float turningTimer = 0f;
    private float turningHoldTime = 0.2f;

    int jumpCount = 0; // 플레이어가 점프한 횟수
    const int maxJumps = 2; // 최대 연속 점프 횟수

    public Animator animator;
    public Transform cam;
    public PlayerCombat playerCombat;

    private PlayerDodge dodge;
    private PlayerLinkJump linkJump;
    private CharacterController controller;
    private Vector3 velocity;
    private bool isJumping = false;
    private bool isGrounded = false;
    private bool isTurning = false;
    private bool isMoving = false;
    private Vector3 lastMoveDirection; // 이전 이동 방향을 저장할 변수

    void Awake()
    {
        dodge = GetComponent<PlayerDodge>();
        controller = GetComponent<CharacterController>();
        linkJump = GetComponent<PlayerLinkJump>();
        if (playerCombat == null)
        {
            playerCombat = GetComponent<PlayerCombat>();
        }
    }

    void Update()
    {
        if (dodge != null)
        {
            if (dodge.IsDodging)
            {
                dodge.Tick(Time.deltaTime);
                velocity = Vector3.zero;
                return;
            }
            if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl))
            {
                Transform basis = cam != null ? cam : transform;
                Vector3 forward = basis.forward; forward.y = 0f; forward.Normalize();
                Vector3 right = basis.right; right.y = 0f; right.Normalize();
                Vector3 input = forward * Input.GetAxisRaw("Vertical") + right * Input.GetAxisRaw("Horizontal");
                if (dodge.TryBegin(input)) { velocity = Vector3.zero; return; }
            }
        }
        if (linkJump != null)
        {
            if (linkJump.IsTraversing)
            {
                linkJump.Tick(Time.deltaTime);
                velocity = Vector3.zero;
                jumpCount = 0;
                return;
            }
            if (Input.GetKeyDown(KeyCode.Space) && linkJump.TryBegin())
            {
                velocity = Vector3.zero;
                jumpCount = 0;
                return;
            }
        }
        HandleMovement();
        HandleJump();
    }

    void HandleMovement()
    {
        // 공격 또는 방어 중일 때는 이동을 막는다
        bool isCombatLocked = playerCombat != null && (playerCombat.isAttacking || playerCombat.IsGuarding || playerCombat.IsHitReacting);
        if (isCombatLocked)
        {
            animator.SetBool("isMoving", false);
            animator.SetBool("isRunning", false);
            animator.SetBool("isTurning", false);
            animator.SetFloat("moveX", 0f);
            animator.SetFloat("moveY", 0f);
            this.isMoving = false;
            return;
        }

        Vector3 inputDir = GetInputDirection();
        bool isMoving = inputDir.magnitude > 0.1f;
        bool isRunning = isMoving && Input.GetKey(KeyCode.LeftShift);

        // 현재 이동 방향을 계산합니다.
        Vector3 currentMoveDirection = cam.forward * Input.GetAxisRaw("Vertical") + cam.right * Input.GetAxisRaw("Horizontal");
        currentMoveDirection.y = 0f;
        currentMoveDirection.Normalize();

        if (isMoving && lastMoveDirection.magnitude > 0.1f)
        {
            float dot = Vector3.Dot(lastMoveDirection.normalized,
                                    currentMoveDirection.normalized);
            if (dot < -0.8f)
                turningTimer = turningHoldTime; // 반전 시 타이머 시작
        }

        // 타이머가 남아있으면 isTurning 유지
        if (turningTimer > 0f)
        {
            turningTimer -= Time.deltaTime;
            isTurning = true;
        }
        else
        {
            isTurning = false;
        }


        if (isMoving)
        {
            Quaternion targetRot = Quaternion.LookRotation(currentMoveDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);

            float speed = isRunning ? runSpeed : walkSpeed;
            if (isTurning) speed *= 0.3f;
            controller.Move(currentMoveDirection * speed * Time.deltaTime);

            lastMoveDirection = currentMoveDirection;
        }

        // 애니메이터 파라미터 업데이트
        animator.SetBool("isMoving", isMoving);
        animator.SetBool("isRunning", isRunning);
        animator.SetBool("isTurning", isTurning);

        Vector2 animInput = TransformToLocalInput(inputDir);
        animator.SetFloat("moveX", animInput.x);
        animator.SetFloat("moveY", animInput.y);


        this.isMoving = isMoving;
    }

    void HandleJump()
    {
        // Raycast를 사용하여 바닥에 닿았는지 확인
        isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, groundCheckDistance + 0.1f);

        if (isGrounded)
        {
            // 착지 시점에 점프 횟수를 초기화
            jumpCount = 0;
            isJumping = false;
            animator.SetBool("isJumping", false);

            animator.SetInteger("jumpCount", jumpCount);
            // CharacterController가 바닥에 잘 붙도록 y축 속도 보정
            if (velocity.y < 0)
            {
                velocity.y = -2f;
            }
        }

        animator.SetBool("isGrounded", isGrounded);

        // TODO: 공격/방어/다운 상태가 이 스크립트에 추가되면 조건에 다시 포함시킨다.
        if ((playerCombat == null || !playerCombat.IsHitReacting) && Input.GetKeyDown(KeyCode.Space) && jumpCount < maxJumps)
        {
            velocity.y = jumpForce;
            isJumping = true;
            animator.SetBool("isJumping", true);
            jumpCount++; // 점프 횟수 1 증가
            animator.SetInteger("jumpCount", jumpCount);
        }

        // 중력 적용
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    Vector3 GetInputDirection()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        return new Vector3(h, 0, v).normalized;
    }

    Vector2 TransformToLocalInput(Vector3 input)
    {
        Vector3 camForward = cam.forward;
        camForward.y = 0;
        camForward.Normalize();

        Vector3 camRight = cam.right;
        camRight.y = 0;
        camRight.Normalize();

        float localX = Vector3.Dot(input, camRight);
        float localY = Vector3.Dot(input, camForward);

        return new Vector2(localX, localY);
    }

}
