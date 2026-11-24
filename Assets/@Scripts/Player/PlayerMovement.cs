using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public class PlayerMovement : MonoBehaviour
{
    [Header("이동 · 점프")]
    [SerializeField, Min(0f)] private float moveSpeed = 5f; // 좌우 이동 속도
    [SerializeField, Min(0f)] private float jumpSpeed = 20f; // 점프 시작 시 위로 올라가는 속도
    [SerializeField, Range(0f, 1f)] private float jumpCutMultiplier = 0.7f; // 키를 놓을 때 남길 상승 속도 비율
    [Header("대시")]
    [SerializeField, Min(0f)] private float dashSpeed = 12f; // 대시 이동 속도
    [SerializeField, Min(0.02f)] private float dashDuration = 0.25f; // 대시 지속 시간
    [SerializeField, Min(0f)] private float dashCooldown = 0.35f; // 종료 후 다시 대시하기까지의 시간
    [SerializeField, Range(0.1f, 0.5f)] private float dashSlowdownRatio = 0.25f; // 대시 끝에서 부드럽게 감속할 구간 비율
    [SerializeField, Range(0.1f, 0.5f)] private float dashAccelerationRatio = 0.25f; // 대시 시작에서 서서히 가속할 구간 비율
    private float dashStartSpeed; // 대시 직전의 진행 방향 속도
    private bool dashRequested; // X를 새로 눌렀을 때의 대시 요청
    private bool isDashing;
    private bool airDashUsed; // 착지 전 공중 대시 사용 여부
    private float dashTimeLeft;
    private float dashCooldownLeft;
    private float dashDirection;
    private int attackLayer;
    private float gravityBeforeDash; // 대시가 끝나면 복원할 기존 중력
    private static readonly int DirectionalAttackState = Animator.StringToHash("Attack.DirectionalAttack");
    private static readonly int DashState = Animator.StringToHash("Attack.Player_dash");
    private static readonly int AirDashState = Animator.StringToHash("Attack.Player_dash_air");
    private static readonly int DashPlaybackRate = Animator.StringToHash("DashPlaybackRate");

    // Animator 변수 이름을 빠르게 찾을 수 있는 숫자로 저장합니다.
    private static readonly int IsRunningParameter = Animator.StringToHash("IsRunning"); // 달리는 중인지 전달할 변수
    private static readonly int GroundedParameter = Animator.StringToHash("isGrounded"); // 바닥에 있는지 전달할 변수
    private static readonly int SpeedParameter = Animator.StringToHash("speed"); // 이동 속도를 전달할 변수
    private readonly ContactPoint2D[] groundContacts = new ContactPoint2D[16]; // 바닥 확인에 사용할 충돌 접촉점 목록
    private Rigidbody2D body; // 이동과 중력을 처리하는 물리 컴포넌트
    private Animator animator; // 애니메이션을 전환하는 컴포넌트
    private SpriteRenderer spriteRenderer; // 캐릭터 그림과 좌우 반전을 담당하는 컴포넌트
    private float horizontalInput; // 이동 방향: 왼쪽 -1, 정지 0, 오른쪽 1
    private bool jumpRequested; // 다음 물리 계산에서 처리할 점프 요청
    private bool jumpHeld; // 점프키(C)를 계속 누르고 있는지
    private bool canCutJump; // 이번 점프의 상승 속도를 아직 줄일 수 있는지
    private bool isGrounded; // 플레이어가 바닥에 닿아 있는지
    public bool IsGrounded => isGrounded;
    public bool IsDashing => isDashing;
    public float HorizontalInput => horizontalInput;
    public float MoveSpeed => moveSpeed;

    public void Initialize()
    {
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        attackLayer = animator.GetLayerIndex("Attack");
    }

    public float ReadInput(Keyboard keyboard)
    {
        horizontalInput = 0f;
        jumpHeld = false;
        if (keyboard != null)
        {
            horizontalInput = (keyboard.rightArrowKey.isPressed ? 1f : 0f)
                - (keyboard.leftArrowKey.isPressed ? 1f : 0f);
            jumpRequested |= keyboard.cKey.wasPressedThisFrame;
            jumpHeld = keyboard.cKey.isPressed;
            dashRequested |= keyboard.xKey.wasPressedThisFrame && !isDashing;
        }
        else
        {
            jumpRequested = false;
            dashRequested = false;
        }
        return horizontalInput;
    }

    public void ApplyInput(float input)
    {
        horizontalInput = input;
        if (horizontalInput != 0f)
            spriteRenderer.flipX = horizontalInput < 0f;
        if (isDashing)
            spriteRenderer.flipX = dashDirection < 0f;
        animator.SetBool(IsRunningParameter, isGrounded && horizontalInput != 0f && moveSpeed > 0f);
        animator.SetFloat(SpeedParameter, Mathf.Abs(horizontalInput * moveSpeed));
        animator.SetBool(GroundedParameter, isGrounded);
    }

    public void TickPhysics(bool isAimLocked, bool isDuckHeld)
    {
        // 일정한 물리 계산 간격마다 바닥 여부와 이동 속도를 갱신합니다.
        isGrounded = CheckGrounded();
        dashCooldownLeft = Mathf.Max(0f, dashCooldownLeft - Time.fixedDeltaTime);
        if (isGrounded && !isDashing)
            airDashUsed = false;

        if (dashRequested && !isDashing && dashCooldownLeft <= 0f && (isGrounded || !airDashUsed))
            BeginDash();
        dashRequested = false;

        if (UpdateDash(isAimLocked, isDuckHeld))
            return;

        UpdateMovementAndJump();
    }

    private void UpdateMovementAndJump()
    {
        Vector2 velocity = body.linearVelocity; // 현재 좌우(x), 위아래(y) 속도
        velocity.x = horizontalInput * moveSpeed;

        // 바닥에서 새로 점프를 요청했을 때만 위로 출발합니다.
        if (jumpRequested && isGrounded)
        {
            velocity.y = jumpSpeed;
            isGrounded = false;
            canCutJump = true;
        }
        jumpRequested = false; // 한 번 처리한 요청은 지워 자동 연속 점프를 막습니다.

        // 상승 중 키를 놓으면 속도를 한 번 줄여 낮게 점프합니다.
        if (canCutJump && !jumpHeld && velocity.y > 0f)
        {
            velocity.y *= jumpCutMultiplier;
            canCutJump = false;
        }
        // 내려가기 시작하면 점프 높이를 줄이는 처리를 끝냅니다.
        if (velocity.y <= 0f)
            canCutJump = false;

        body.linearVelocity = velocity; // 계산한 속도를 실제 물리 이동에 적용합니다.
    }

    private bool UpdateDash(bool isAimLocked, bool isDuckHeld)
    {
        if (!isDashing)
            return false;

        jumpRequested = false;
        if (dashTimeLeft <= 0.0001f)
        {
            EndDash();
            return false;
        }

        body.linearVelocity = new Vector2(dashDirection * CalculateDashSpeed(isAimLocked, isDuckHeld), 0f);
        dashTimeLeft -= Time.fixedDeltaTime;
        return true;
    }

    private float CalculateDashSpeed(bool isAimLocked, bool isDuckHeld)
    {
        float endSpeed = horizontalInput * dashDirection > 0f && !isAimLocked && !isDuckHeld
            ? Mathf.Min(moveSpeed, dashSpeed) : 0f;
        float slowdown = Mathf.Clamp(dashSlowdownRatio, 0.1f, 0.5f);
        float acceleration = Mathf.Clamp(dashAccelerationRatio, 0.1f, 0.5f);
        // 가속·감속으로 줄어드는 거리를 중간 구간의 속도로 보정합니다.
        float peakSpeed = (dashSpeed - dashStartSpeed * acceleration * 0.5f - endSpeed * slowdown * 0.5f)
            / (1f - (acceleration + slowdown) * 0.5f);
        float duration = Mathf.Max(dashDuration, 0.02f);
        float progress = Mathf.Clamp01((duration - dashTimeLeft) / duration);
        float nextProgress = Mathf.Clamp01(progress + Time.fixedDeltaTime / duration);
        float distance = DashDistanceAt(nextProgress, acceleration, slowdown, peakSpeed, endSpeed)
            - DashDistanceAt(progress, acceleration, slowdown, peakSpeed, endSpeed);
        return distance * duration / Time.fixedDeltaTime;
    }

    private bool CheckGrounded()
    {
        // 상승 중에는 직전의 바닥 접촉이 남아 있어도 공중으로 판단합니다.
        if (body.linearVelocity.y > 0.1f)
            return false;

        int count = body.GetContacts(groundContacts); // 현재 닿아 있는 접촉점 개수
        for (int i = 0; i < count; i++) // 접촉점을 하나씩 확인할 순서
        {
            // 접촉면이 위쪽을 향해야 바닥입니다. 벽과 천장은 제외합니다.
            if (groundContacts[i].normal.y >= 0.65f)
                return true;
        }
        return false;
    }

    // 부드러운 속도 곡선을 적분해 물리 계산 간격에 따른 거리 오차를 막습니다.
    private float DashDistanceAt(float progress, float acceleration, float slowdown, float peakSpeed, float endSpeed)
    {
        float up = Mathf.Clamp01(progress / acceleration);
        float down = Mathf.Clamp01((progress - (1f - slowdown)) / slowdown);
        float upArea = up * up * up - 0.5f * up * up * up * up;
        float downArea = down * down * down - 0.5f * down * down * down * down;
        return peakSpeed * progress
            - (peakSpeed - dashStartSpeed) * acceleration * (up - upArea)
            + (endSpeed - peakSpeed) * slowdown * downArea;
    }

    private void BeginDash()
    {
        isDashing = true;
        airDashUsed |= !isGrounded;
        dashDirection = spriteRenderer.flipX ? -1f : 1f;
        dashStartSpeed = Mathf.Clamp(body.linearVelocity.x * dashDirection, 0f, dashSpeed);
        dashTimeLeft = dashDuration;
        gravityBeforeDash = body.gravityScale;
        body.gravityScale = 0f; // 공중에서도 대시 중에는 수평으로 이동합니다.
        canCutJump = false;
        if (attackLayer >= 0)
        {
            // 각 상태의 클립 길이와 곱해져 대시 시간 동안 한 번 재생됩니다.
            animator.SetFloat(DashPlaybackRate, 1f / Mathf.Max(dashDuration, 0.02f));
            animator.Play(isGrounded ? DashState : AirDashState, attackLayer, 0f);
            animator.SetLayerWeight(attackLayer, 1f);
        }
    }

    private void EndDash()
    {
        if (!isDashing)
            return;
        isDashing = false;
        body.gravityScale = gravityBeforeDash;
        dashCooldownLeft = dashCooldown;
        if (attackLayer >= 0)
            animator.Play(DirectionalAttackState, attackLayer, 0f);
    }

    public void ApplyParryBounce(float upwardSpeed)
    {
        EndDash();
        jumpRequested = false;
        dashRequested = false;
        canCutJump = false; // 점프 키를 놓아도 패링 반동은 줄이지 않습니다.
        isGrounded = false;
        body.linearVelocity = new Vector2(body.linearVelocity.x, upwardSpeed);
    }

    public void ResetState()
    {
        EndDash(); // 비활성화되더라도 중력이 0인 채로 남지 않게 합니다.
        dashRequested = false;
        airDashUsed = false;
        dashCooldownLeft = 0f;
        // 비활성화될 때 입력과 점프 상태를 지웁니다.
        horizontalInput = 0f;
        jumpRequested = false;
        jumpHeld = false;
        canCutJump = false;
        isGrounded = false;
        if (animator != null && attackLayer >= 0)
            animator.SetLayerWeight(attackLayer, 0f);
        // 수직 속도는 유지하고 좌우 이동과 달리기 표시를 멈춥니다.
        if (body != null)
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetBool(IsRunningParameter, false);
    }
}
