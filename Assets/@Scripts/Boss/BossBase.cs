using UnityEngine;

[RequireComponent(typeof(Animator))]
public abstract class BossBase : MonoBehaviour
{
    public enum BossState { Idle, Attack, Dead }

    [Header("공통 보스 체력 · 피격")]
    [SerializeField, Min(1)] private int maxHealth = 360;
    [SerializeField] private int currentHealth;
    [SerializeField, Min(0f)] private float hitFlashDuration = 0.12f;
    [SerializeField, Range(0f, 1f), Tooltip("흰색 효과의 불투명도입니다. 0은 원래 색, 1은 완전한 흰색입니다.")]
    private float hitFlashOpacity = 1f;
    private SpriteRenderer spriteRenderer;
    private static readonly int FlashAmount = Shader.PropertyToID("_FlashAmount");
    private MaterialPropertyBlock flashProperties;
    private Collider2D[] hitColliders;
    private float flashRemaining;
    private bool damageEnabled = true;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsDead { get; private set; }
    public event System.Action Died;

    public void SetDamageEnabled(bool value) => damageEnabled = value;

    private bool hasState;
    public BossState CurrentState { get; private set; }
    protected float StateElapsed { get; private set; }
    protected Animator BossAnimator { get; private set; }

    protected virtual void Awake()
    {
        BossAnimator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        flashProperties = new MaterialPropertyBlock();
        hitColliders = GetComponentsInChildren<Collider2D>(true);
        currentHealth = Mathf.Max(1, maxHealth);
    }

    protected virtual void OnEnable()
    {
        if (!IsDead) ChangeState(BossState.Idle);
    }
    private void Update() => Tick(Time.deltaTime);

    private void Tick(float deltaTime)
    {
        if (IsDead) return;
        UpdateHitFlash(deltaTime);
        if (!hasState) return;
        StateElapsed += Mathf.Max(0f, deltaTime);
        UpdateState();
    }

    protected void ChangeState(BossState nextState)
    {
        if (IsDead && nextState != BossState.Dead) return;
        if (hasState && CurrentState == nextState) return;
        if (hasState) ExitState(CurrentState);
        CurrentState = nextState;
        StateElapsed = 0f;
        hasState = true;
        EnterState(nextState);
    }

    protected void PlayAnimation(int stateHash)
    {
        BossAnimator.Play(stateHash, 0, 0f);
        BossAnimator.Update(0f);
    }

    protected abstract void EnterState(BossState state);
    protected abstract void UpdateState();
    protected abstract void ExitState(BossState state);

    public bool TakeDamage(int amount)
    {
        if (!isActiveAndEnabled || !damageEnabled || IsDead || amount <= 0) return false;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        if (currentHealth == 0)
        {
            Die();
            return true;
        }
        flashRemaining = hitFlashDuration;
        SetFlash(flashRemaining > 0f);
        return true;
    }

    private void UpdateHitFlash(float deltaTime)
    {
        if (flashRemaining <= 0f) return;
        flashRemaining = Mathf.Max(0f, flashRemaining - deltaTime);
        if (flashRemaining <= 0f) SetFlash(false);
    }

    private void SetFlash(bool flashing)
    {
        if (spriteRenderer == null) return;
        flashProperties ??= new MaterialPropertyBlock();
        spriteRenderer.GetPropertyBlock(flashProperties);
        flashProperties.SetFloat(FlashAmount, flashing ? hitFlashOpacity : 0f);
        spriteRenderer.SetPropertyBlock(flashProperties);
    }

    private void Die()
    {
        IsDead = true;
        StopAllCoroutines();
        ChangeState(BossState.Dead);
        flashRemaining = 0f;
        SetFlash(false);
        foreach (var collider in hitColliders)
            if (collider != null) collider.enabled = false;
        Died?.Invoke();
        OnDeath();
    }

    // 향후 자식 보스는 여기에서 사망 연출 후 비활성화할 수 있습니다.
    protected virtual void OnDeath() => gameObject.SetActive(false);

    protected virtual void OnDisable()
    {
        // 비활성화를 공격 완료로 처리해서 사이클이 진행되지 않게 합니다.
        hasState = false;
        StateElapsed = 0f;
        flashRemaining = 0f;
        SetFlash(false);
    }
}
