using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
public class PlayerAttack : MonoBehaviour
{
    [SerializeField] private Transform shootPos;
    [SerializeField] private BulletPool bulletPool;
    [SerializeField, Min(0.02f)] private float fireInterval = 0.12f;
    [SerializeField] private float duckMuzzleOffset = -0.35f; // 앉을 때 발사 위치를 내릴 높이
    private PlayerController controller;
    private Collider2D[] ownerColliders;
    private Vector3 muzzleRestPosition;
    private Quaternion muzzleRestRotation;
    private float nextFireTime;
    private bool attackHeld; // 왼쪽 Shift를 누르고 있는지
    private bool aimLocked; // Z로 제자리 조준 중인지
    private bool duckHeld; // Z 조준이 아닐 때 아래 방향키를 누르고 있는지
    private bool wasDucking; // 앉기 시작 순간을 구분하는 이전 상태
    private bool wasDuckAttacking; // 앉은 공격을 끝내고 대기로 돌아갈 때 사용
    private static readonly int DuckState = Animator.StringToHash("Attack.Player_duck");
    private static readonly int DuckIdleState = Animator.StringToHash("Attack.Player_duck_idle");
    private static readonly int DuckShootState = Animator.StringToHash("Attack.Player_duck_shoot");
    private static readonly int DirectionalAttackState = Animator.StringToHash("Attack.DirectionalAttack");
    private Vector2 aimDirection = Vector2.right; // 마지막 조준 방향
    private int attackLayer; // 기존 이동 애니메이션 위에 표시할 공격 레이어
    private static readonly int AimPose = Animator.StringToHash("AimPose");
    private static readonly int AttackPlaybackSpeed = Animator.StringToHash("AttackPlaybackSpeed");

    private PlayerMovement movement;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    
    private bool isGrounded => movement.IsGrounded;
    private bool isDashing => movement.IsDashing;
    private float horizontalInput => movement.HorizontalInput;
    private float moveSpeed => movement.MoveSpeed;
    public bool AimLockedInput => aimLocked;
    public bool DuckHeldInput => duckHeld;
    public bool AttackHeldInput => attackHeld; // 점프 애니메이션과 별개로 실제 발사 입력을 전달합니다.

    // 나중에 발사 코드에서 입력 상태와 실제 조준 방향을 사용할 수 있습니다.
    public Vector2 AimDirection => aimDirection.normalized;
    public bool IsAttacking => attackHeld && isGrounded && !isDashing;
    public bool IsAimLocked => aimLocked && isGrounded && !isDashing;
    public bool IsDucking => duckHeld && isGrounded && !isDashing;

    public void Initialize(PlayerMovement playerMovement)
    {
        movement = playerMovement;
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        attackLayer = animator.GetLayerIndex("Attack");
        aimDirection = spriteRenderer.flipX ? Vector2.left : Vector2.right;
        controller = GetComponent<PlayerController>();
        ownerColliders = GetComponentsInChildren<Collider2D>();
        if (shootPos == null) shootPos = transform.Find("ShootPos");
        if (shootPos != null)
        {
            muzzleRestPosition = shootPos.localPosition;
            muzzleRestRotation = shootPos.localRotation;
        }
    }

    private void LateUpdate()
    {
        // 이번 프레임의 조준·이동 입력 처리가 끝난 뒤 발사합니다.
        if (controller == null || !controller.isActiveAndEnabled || !movement.isActiveAndEnabled
            || shootPos == null || bulletPool == null)
            return;
        Vector2 direction = AimDirection;
        UpdateMuzzle(direction);
        if (!attackHeld || isDashing || Time.time < nextFireTime)
            return;
        bulletPool.Fire(shootPos.position, direction, transform, ownerColliders);
        nextFireTime = Time.time + Mathf.Max(0.02f, fireInterval);
    }

    private void UpdateMuzzle(Vector2 direction)
    {
        Vector3 pivot = new Vector3(0f, muzzleRestPosition.y, muzzleRestPosition.z);
        if (IsDucking) pivot.y += duckMuzzleOffset;
        shootPos.localPosition = pivot
            + new Vector3(direction.x, direction.y, 0f) * Mathf.Abs(muzzleRestPosition.x);
        shootPos.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    private void OnDisable() => ResetState();

    public float ReadInput(Keyboard keyboard, float horizontalInput)
    {
        attackHeld = false;
        duckHeld = false;
        bool wasAimLocked = aimLocked;
        aimLocked = false;
        if (keyboard != null)
        {
            attackHeld = keyboard.leftShiftKey.isPressed;
            aimLocked = keyboard.zKey.isPressed;
            duckHeld = keyboard.downArrowKey.isPressed && !aimLocked;
            float verticalInput = (keyboard.upArrowKey.isPressed ? 1f : 0f)
                - (keyboard.downArrowKey.isPressed ? 1f : 0f);
            if (!aimLocked || !wasAimLocked)
                aimDirection = spriteRenderer.flipX ? Vector2.left : Vector2.right;
            if (horizontalInput != 0f || verticalInput != 0f)
            {
                aimDirection = new Vector2(horizontalInput, verticalInput);
                if (horizontalInput != 0f)
                    spriteRenderer.flipX = horizontalInput < 0f;
            }
            // 지상에서는 Z를 누른 동안 방향키를 이동 대신 조준에 사용합니다.
            if (aimLocked && isGrounded)
                horizontalInput = 0f;
            // 앉아 있는 동안 이동을 멈추고 바라보는 쪽으로 공격합니다.
            if (IsDucking)
            {
                horizontalInput = 0f;
                aimDirection = spriteRenderer.flipX ? Vector2.left : Vector2.right;
            }
        }
        return horizontalInput;
    }

    public void UpdateAnimation()
    {
        if (attackLayer < 0)
            return;
        if (isDashing)
        {
            animator.SetLayerWeight(attackLayer, 1f);
            return;
        }

        bool ducking = IsDucking;
        if (ducking)
        {
            if (attackHeld && (!wasDucking || !wasDuckAttacking))
                animator.Play(DuckShootState, attackLayer, 0f);
            else if (!attackHeld && !wasDucking)
                animator.Play(DuckState, attackLayer, 0f);
            else if (!attackHeld && wasDuckAttacking)
                animator.Play(DuckIdleState, attackLayer, 0f);
        }
        else if (wasDucking)
        {
            animator.Play(DirectionalAttackState, attackLayer, 0f);
        }
        wasDucking = ducking;
        wasDuckAttacking = ducking && attackHeld;

        // 다섯 방향 클립과 좌우 반전으로 여덟 방향을 표현합니다.
        int pose = aimDirection.y > 0f ? (aimDirection.x == 0f ? 2 : 1)
            : aimDirection.y < 0f ? (aimDirection.x == 0f ? 4 : 3) : 0;
        // 이동 공격은 전용 달리기 클립을 사용합니다. 아래 방향은 기존 클립을 유지합니다.
        if (attackHeld && !aimLocked && horizontalInput != 0f && moveSpeed > 0f && aimDirection.y >= 0f)
            pose = aimDirection.y > 0f ? 6 : 5;
        animator.SetFloat(AimPose, pose);
        // 고정 조준 중에도 선택한 방향의 애니메이션을 반복 재생합니다.
        animator.SetFloat(AttackPlaybackSpeed, attackHeld || aimLocked || ducking ? 1f : 0f);
        animator.SetLayerWeight(attackLayer, isGrounded && (attackHeld || aimLocked || ducking) ? 1f : 0f);
    }

    public void ResetDuckState()
    {
        wasDucking = false;
        wasDuckAttacking = false;
    }

    public void ResetState()
    {
        nextFireTime = 0f;
        if (shootPos != null)
        {
            shootPos.localPosition = muzzleRestPosition;
            shootPos.localRotation = muzzleRestRotation;
        }
        attackHeld = false;
        aimLocked = false;
        duckHeld = false;
        ResetDuckState();
        if (animator != null && attackLayer >= 0)
            animator.SetLayerWeight(attackLayer, 0f);
    }
}
