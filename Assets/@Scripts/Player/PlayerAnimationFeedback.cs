using UnityEngine;

// 일반 이동·공격보다 우선하는 일회성 피격·패링 애니메이션을 관리합니다.
[RequireComponent(typeof(Animator), typeof(PlayerHealth), typeof(PlayerMovement))]
public class PlayerAnimationFeedback : MonoBehaviour
{
    private enum Feedback { None, ParryAttempt, ParrySuccess, Hit }

    [SerializeField] private AnimationClip hitAnimation;
    [SerializeField] private AnimationClip parryAttemptAnimation;
    [SerializeField] private AnimationClip parrySuccessAnimation;

    private static readonly int HitState = Animator.StringToHash("Base Layer.Player_hit");
    private static readonly int ParryState = Animator.StringToHash("Base Layer.Player_Parry");
    private static readonly int ParrySuccessState = Animator.StringToHash("Base Layer.Player_Parrying");
    private static readonly int IdleState = Animator.StringToHash("Base Layer.Player_Idle");
    private static readonly int RunState = Animator.StringToHash("Base Layer.Player_Run");
    private static readonly int JumpState = Animator.StringToHash("Base Layer.Player_Jump");
    private Animator animator;
    private PlayerHealth health;
    private PlayerMovement movement;
    private Feedback current;
    private int attackLayer;
    private float remaining;

    public bool IsHitReacting => current == Feedback.Hit && !IsDead;
    public bool IsDead => health.IsDead;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        health = GetComponent<PlayerHealth>();
        movement = GetComponent<PlayerMovement>();
        attackLayer = animator.GetLayerIndex("Attack");
    }

    private void OnEnable() => health.Damaged += PlayHit;
    private void OnDisable()
    {
        health.Damaged -= PlayHit;
        current = Feedback.None;
        remaining = 0f;
    }

    private void PlayHit() => Play(Feedback.Hit, HitState, hitAnimation);

    public void PlayParryAttempt()
    {
        if (!IsHitReacting) Play(Feedback.ParryAttempt, ParryState, parryAttemptAnimation);
    }

    public void PlayParrySuccess()
    {
        if (!IsHitReacting) Play(Feedback.ParrySuccess, ParrySuccessState, parrySuccessAnimation);
    }

    private void Play(Feedback feedback, int state, AnimationClip clip)
    {
        if (!isActiveAndEnabled || IsDead) return;
        current = feedback;
        remaining = clip != null ? Mathf.Max(.01f, clip.length) : .4f;
        HideAttackLayer();
        animator.Play(state, 0, 0f);
        animator.Update(0f);
    }

    private void Update() => Tick(Time.deltaTime);

    private void Tick(float deltaTime)
    {
        if (IsDead)
        {
            current = Feedback.None;
            remaining = 0f;
            return;
        }
        if (current == Feedback.None) return;
        remaining -= deltaTime;
        if (remaining > 0f) return;
        current = Feedback.None;
        int state = !movement.IsGrounded ? JumpState
            : movement.HorizontalInput != 0f ? RunState : IdleState;
        animator.Play(state, 0, 0f);
    }

    private void LateUpdate()
    {
        // 공격·대시 레이어가 피격 또는 패링 그림을 덮지 않도록 합니다.
        if (current != Feedback.None || IsDead) HideAttackLayer();
    }

    private void HideAttackLayer()
    {
        if (attackLayer >= 0) animator.SetLayerWeight(attackLayer, 0f);
    }
}
