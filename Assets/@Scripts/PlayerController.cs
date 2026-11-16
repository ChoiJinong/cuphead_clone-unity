using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 5f;

    private static readonly int IsRunning = Animator.StringToHash("IsRunning");
    private Rigidbody2D body;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private float horizontalInput;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        horizontalInput = 0f;
        Keyboard keyboard = Keyboard.current;
        if (Application.isFocused && keyboard != null)
        {
            bool left = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
            bool right = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
            horizontalInput = (right ? 1f : 0f) - (left ? 1f : 0f);
        }

        if (horizontalInput != 0f)
            spriteRenderer.flipX = horizontalInput < 0f;

        animator.SetBool(IsRunning, horizontalInput != 0f && moveSpeed > 0f);
    }

    private void FixedUpdate()
    {
        body.linearVelocity = new Vector2(horizontalInput * moveSpeed, body.linearVelocity.y);
    }

    private void OnDisable()
    {
        horizontalInput = 0f;
        if (body != null)
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetBool(IsRunning, false);
    }
}
