using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerMovement), typeof(PlayerAnimationFeedback), typeof(PlayerCardGauge))]
public class PlayerParry : MonoBehaviour
{
    [SerializeField, Min(.01f)] private float parryWindow = .25f;
    [SerializeField, Min(0f)] private float bounceSpeed = 25f;
    private PlayerMovement movement;
    private PlayerAnimationFeedback feedback;
    private PlayerCardGauge cards;
    private float remaining;

    public bool IsAttempting => isActiveAndEnabled && remaining > 0f
        && !movement.IsGrounded && !feedback.IsDead && !feedback.IsHitReacting;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        feedback = GetComponent<PlayerAnimationFeedback>();
        cards = GetComponent<PlayerCardGauge>();
    }

    private void Update()
    {
        Tick(Time.deltaTime);
        ReadInput(Application.isFocused ? Keyboard.current : null);
    }

    public void ReadInput(Keyboard keyboard)
    {
        if (keyboard != null && keyboard.cKey.wasPressedThisFrame) TryBeginParry();
    }

    public bool TryBeginParry()
    {
        if (!isActiveAndEnabled || remaining > 0f || movement.IsGrounded
            || feedback.IsDead || feedback.IsHitReacting) return false;
        remaining = parryWindow;
        feedback.PlayParryAttempt();
        return true;
    }

    private void Tick(float deltaTime)
    {
        if (movement.IsGrounded || feedback.IsDead || feedback.IsHitReacting) remaining = 0f;
        else remaining = Mathf.Max(0f, remaining - deltaTime);
    }

    // 투사체가 패링 가능 여부를 확인한 뒤 데미지 처리보다 먼저 호출합니다.
    public bool TryParry()
    {
        if (!IsAttempting) return false;
        remaining = 0f; // 같은 입력으로 두 투사체를 연속 처리하지 않습니다.
        movement.ApplyParryBounce(bounceSpeed);
        cards.AddCards(1f);
        feedback.PlayParrySuccess();
        return true;
    }

    private void OnDisable() => remaining = 0f;
}
