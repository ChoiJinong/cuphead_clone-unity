using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
public class PlayerAttack : MonoBehaviour
{
    private enum AimAnimation
    {
        Forward = 0,
        DiagonalUp = 1,
        Up = 2,
        DiagonalDown = 3,
        Down = 4,
        RunForward = 5,
        RunDiagonalUp = 6
    }

    private static readonly int DuckState = Animator.StringToHash("Attack.Player_duck");
    private static readonly int DuckIdleState = Animator.StringToHash("Attack.Player_duck_idle");
    private static readonly int DuckShootState = Animator.StringToHash("Attack.Player_duck_shoot");
    private static readonly int DirectionalAttackState = Animator.StringToHash("Attack.DirectionalAttack");
    private static readonly int AimPoseParameter = Animator.StringToHash("AimPose");
    private static readonly int AttackPlaybackSpeedParameter = Animator.StringToHash("AttackPlaybackSpeed");

    [Header("발사")]
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
    private Vector2 aimDirection = Vector2.right; // 마지막 조준 방향
    private int attackLayer; // 기존 이동 애니메이션 위에 표시할 공격 레이어

    private PlayerMovement movement;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    public bool AimLockedInput => aimLocked;
    public bool DuckHeldInput => duckHeld;
    public bool AttackHeldInput => attackHeld; // 점프 애니메이션과 별개로 실제 발사 입력을 전달합니다.

    public Vector2 AimDirection => aimDirection.normalized;
    public bool IsAttacking => attackHeld && movement.IsGrounded && !movement.IsDashing;
    public bool IsAimLocked => aimLocked && movement.IsGrounded && !movement.IsDashing;
    public bool IsDucking => duckHeld && movement.IsGrounded && !movement.IsDashing;

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
        if (!attackHeld || movement.IsDashing || Time.time < nextFireTime)
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
        if (keyboard == null)
            return horizontalInput;

        attackHeld = keyboard.leftShiftKey.isPressed;
        aimLocked = keyboard.zKey.isPressed;
        duckHeld = keyboard.downArrowKey.isPressed && !aimLocked;
        UpdateAimDirection(keyboard, horizontalInput, wasAimLocked);

        if (aimLocked && movement.IsGrounded)
            horizontalInput = 0f;
        if (IsDucking)
        {
            horizontalInput = 0f;
            aimDirection = spriteRenderer.flipX ? Vector2.left : Vector2.right;
        }
        return horizontalInput;
    }

    private void UpdateAimDirection(Keyboard keyboard, float horizontalInput, bool wasAimLocked)
    {
        float verticalInput = (keyboard.upArrowKey.isPressed ? 1f : 0f)
            - (keyboard.downArrowKey.isPressed ? 1f : 0f);
        if (!aimLocked || !wasAimLocked)
            aimDirection = spriteRenderer.flipX ? Vector2.left : Vector2.right;
        if (horizontalInput == 0f && verticalInput == 0f)
            return;

        aimDirection = new Vector2(horizontalInput, verticalInput);
        if (horizontalInput != 0f)
            spriteRenderer.flipX = horizontalInput < 0f;
    }

    public void UpdateAnimation()
    {
        if (attackLayer < 0)
            return;
        if (movement.IsDashing)
        {
            ResetDuckState();
            animator.SetLayerWeight(attackLayer, 1f);
            return;
        }

        UpdateDuckAnimation();
        animator.SetFloat(AimPoseParameter, (int)GetAimAnimation());
        animator.SetFloat(AttackPlaybackSpeedParameter, attackHeld || aimLocked || IsDucking ? 1f : 0f);
        animator.SetLayerWeight(attackLayer,
            movement.IsGrounded && (attackHeld || aimLocked || IsDucking) ? 1f : 0f);
    }

    private void UpdateDuckAnimation()
    {
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
    }

    private AimAnimation GetAimAnimation()
    {
        bool movingAttack = attackHeld && !aimLocked && movement.HorizontalInput != 0f && movement.MoveSpeed > 0f;
        if (movingAttack && aimDirection.y >= 0f)
            return aimDirection.y > 0f ? AimAnimation.RunDiagonalUp : AimAnimation.RunForward;
        if (aimDirection.y > 0f)
            return aimDirection.x == 0f ? AimAnimation.Up : AimAnimation.DiagonalUp;
        if (aimDirection.y < 0f)
            return aimDirection.x == 0f ? AimAnimation.Down : AimAnimation.DiagonalDown;
        return AimAnimation.Forward;
    }

    private void ResetDuckState()
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
