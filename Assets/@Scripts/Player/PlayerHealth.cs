using UnityEngine;

[RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(Animator))]
public class PlayerHealth : MonoBehaviour
{
    [Header("체력 · 피격")]
    [SerializeField, Min(1)] private int maxHealth = 3;
    [SerializeField] private int currentHealth;
    [SerializeField, Min(0f)] private float invincibilityDuration = 1.5f;
    [UnityEngine.Serialization.FormerlySerializedAs("blinkInterval")]
    [SerializeField, Min(0f), Tooltip("피격 시 한 번 흰색으로 표시되는 시간입니다. 무적 시간과는 별개입니다.")]
    private float hitFlashDuration = 0.1f;
    [SerializeField, Range(0f, 1f), Tooltip("흰색 효과의 불투명도입니다. 0은 원래 색, 1은 완전한 흰색입니다.")]
    private float hitFlashOpacity = 1f;
    [Header("사망 · 유령")]
    [SerializeField, Min(0f)] private float ghostRiseSpeed = 1.5f;
    [SerializeField, Min(0f)] private float deactivateDelay = 3f;

    private static readonly int IsDeadParameter = Animator.StringToHash("IsDead");
    private static readonly int DeathPhaseParameter = Animator.StringToHash("DeathPhase");
    private static readonly int DeathState = Animator.StringToHash("Base Layer.Death");
    private SpriteRenderer spriteRenderer;
    private static readonly int FlashAmount = Shader.PropertyToID("_FlashAmount");
    private MaterialPropertyBlock flashProperties;
    private Rigidbody2D body;
    private Animator animator;
    private PlayerController controller;
    private PlayerMovement movement;
    private PlayerAttack attack;
    private Collider2D[] colliders;
    private float invincibilityRemaining;
    private float flashRemaining;
    private float deathElapsed;
    private bool damageEnabled = true;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsDead { get; private set; }
    public bool IsInvincible => !IsDead && invincibilityRemaining > 0f;
    public event System.Action Damaged;
    public event System.Action<int> HealthChanged;
    public event System.Action Died;

    public void SetDamageEnabled(bool value) => damageEnabled = value;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        flashProperties = new MaterialPropertyBlock();
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        controller = GetComponent<PlayerController>();
        movement = GetComponent<PlayerMovement>();
        attack = GetComponent<PlayerAttack>();
        colliders = GetComponentsInChildren<Collider2D>(true);
        currentHealth = Mathf.Max(1, maxHealth);
        HealthChanged?.Invoke(currentHealth);
    }

    public bool TakeDamage(int amount)
    {
        if (!isActiveAndEnabled || !damageEnabled || IsDead || IsInvincible || amount <= 0) return false;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        HealthChanged?.Invoke(currentHealth);
        if (currentHealth == 0)
        {
            Die();
            return true;
        }
        invincibilityRemaining = invincibilityDuration;
        flashRemaining = hitFlashDuration;
        SetFlash(flashRemaining > 0f);
        Damaged?.Invoke();
        return true;
    }

    private void Update() => Tick(Time.deltaTime);

    private void Tick(float deltaTime)
    {
        if (IsDead)
        {
            UpdateDeath(deltaTime);
            return;
        }
        invincibilityRemaining = Mathf.Max(0f, invincibilityRemaining - deltaTime);
        if (flashRemaining <= 0f) return;
        flashRemaining = Mathf.Max(0f, flashRemaining - deltaTime);
        if (flashRemaining <= 0f) SetFlash(false);
    }

    private void SetFlash(bool flashing)
    {
        if (spriteRenderer == null) return;
        flashProperties ??= new MaterialPropertyBlock();
        // 공유 Material을 바꾸지 않아 이 캐릭터에만 피격 효과를 적용합니다.
        spriteRenderer.GetPropertyBlock(flashProperties);
        flashProperties.SetFloat(FlashAmount, flashing ? hitFlashOpacity : 0f);
        spriteRenderer.SetPropertyBlock(flashProperties);
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;
        invincibilityRemaining = 0f;
        flashRemaining = 0f;
        SetFlash(false);

        // 먼저 대시를 정리해 중력이 복원된 뒤 사망용 물리 상태로 바꿉니다.
        if (controller != null) controller.enabled = false;
        if (movement != null) { movement.ResetState(); movement.enabled = false; }
        if (attack != null) { attack.ResetState(); attack.enabled = false; }
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.gravityScale = 0f;
        body.simulated = false;
        foreach (var collider in colliders)
            if (collider != null) collider.enabled = false;

        for (int layer = 1; layer < animator.layerCount; layer++) animator.SetLayerWeight(layer, 0f);
        animator.SetFloat(DeathPhaseParameter, 1f);
        animator.SetBool(IsDeadParameter, true);
        animator.Play(DeathState, 0, 0f);
        animator.Update(0f);
        Died?.Invoke();
    }

    private void UpdateDeath(float deltaTime)
    {
        deathElapsed += deltaTime;
        transform.position += Vector3.up * (ghostRiseSpeed * deltaTime);
        if (deathElapsed >= deactivateDelay) gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        invincibilityRemaining = 0f;
        flashRemaining = 0f;
        SetFlash(false);
    }
}
