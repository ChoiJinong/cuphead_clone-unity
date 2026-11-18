using UnityEngine;
using UnityEngine.InputSystem;

// 입력·이동·공격의 실행 순서만 관리합니다.
[RequireComponent(typeof(PlayerMovement), typeof(PlayerAttack))]
public class PlayerController : MonoBehaviour
{
    private PlayerMovement movement;
    private PlayerAttack attack;

    public Vector2 AimDirection => attack.AimDirection;
    public bool IsAttacking => attack.IsAttacking;
    public bool IsAimLocked => attack.IsAimLocked;
    public bool IsDucking => attack.IsDucking;
    public bool IsDashing => movement.IsDashing;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        attack = GetComponent<PlayerAttack>();
        movement.Initialize(attack);
        attack.Initialize(movement);
    }

    private void Update()
    {
        Keyboard keyboard = Application.isFocused ? Keyboard.current : null;
        float horizontal = movement.ReadInput(keyboard);
        horizontal = attack.ReadInput(keyboard, horizontal);
        movement.ApplyInput(horizontal);
        attack.UpdateAnimation();
    }

    private void FixedUpdate()
    {
        movement.TickPhysics();
        attack.UpdateAnimation();
    }

    private void OnDisable()
    {
        if (movement != null) movement.ResetState();
        if (attack != null) attack.ResetState();
    }
}
