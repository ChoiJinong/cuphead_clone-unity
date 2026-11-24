using UnityEngine;

public class Boss_SalSpudder : BossBase
{
    public enum ProjectileKind { Dirt, Worm }
    private const int DirtShotsPerCycle = 3;
    private const int ShotsPerCycle = 4;
    private const int SpeedStageCount = 3;

    [Header("발사 위치와 종류별 풀")]
    [SerializeField] private Transform shootPos;
    [SerializeField] private Boss_Bullet_Pool dirtPool;
    [SerializeField] private Boss_Bullet_Pool wormPool;
    [Header("공격 시작 간격 (애니메이션보다 짧으면 종료까지 대기)")]
    [SerializeField, Min(0f)] private float stageOneInterval = 2.6f;
    [SerializeField, Min(0f)] private float stageTwoInterval = 2.1f;
    [SerializeField, Min(0f)] private float stageThreeInterval = 1.85f;
    [SerializeField, Min(0f)] private float cyclePause = 1.5f;
    [Header("현재 Animator의 전체 상태 경로")]
    [SerializeField] private string idleState = "Base Layer.Boss1_Idle";
    [SerializeField] private string attackState = "Base Layer.Boss1_Attack";
    [SerializeField, Range(0f, 1f)] private float releaseNormalizedTime = 0.4090909f;

    private Collider2D[] ownerColliders;
    private int idleStateHash;
    private int attackStateHash;
    private int speedStage;
    private int shotsInCycle;
    private bool shotFired;
    private float idleDelay;
    private float intervalForAttack;

    public int CurrentSpeedStage => speedStage + 1;
    public int ShotsInCycle => shotsInCycle;
    public int TotalShotsFired { get; private set; }
    public int CompletedCycles { get; private set; }
    public ProjectileKind LastProjectileKind { get; private set; }
    private float CurrentInterval => speedStage == 0 ? stageOneInterval
        : speedStage == 1 ? stageTwoInterval : stageThreeInterval;

    protected override void Awake()
    {
        base.Awake();
        idleStateHash = Animator.StringToHash(idleState);
        attackStateHash = Animator.StringToHash(attackState);
        ownerColliders = GetComponentsInChildren<Collider2D>();
        if (shootPos == null) shootPos = transform.Find("ShootPos");
        if (shootPos == null || dirtPool == null || wormPool == null
            || !BossAnimator.HasState(0, idleStateHash) || !BossAnimator.HasState(0, attackStateHash))
        {
            Debug.LogError("Boss_SalSpudder: ShootPos, 두 투사체 풀과 Animator 상태를 확인하세요.", this);
            enabled = false;
        }
    }

    protected override void OnEnable()
    {
        if (IsDead) return;
        speedStage = shotsInCycle = 0;
        TotalShotsFired = CompletedCycles = 0;
        shotFired = false;
        idleDelay = stageOneInterval;
        base.OnEnable();
    }

    protected override void EnterState(BossState state)
    {
        if (state == BossState.Dead) return;
        if (state == BossState.Idle)
        {
            PlayAnimation(idleStateHash);
            return;
        }
        shotFired = false;
        intervalForAttack = CurrentInterval;
        PlayAnimation(attackStateHash);
    }

    protected override void UpdateState()
    {
        if (IsDead) return;
        if (CurrentState == BossState.Idle)
        {
            if (StateElapsed >= idleDelay) ChangeState(BossState.Attack);
            return;
        }

        var animation = BossAnimator.GetCurrentAnimatorStateInfo(0);
        if (animation.fullPathHash != attackStateHash) return;
        // 이벤트가 누락되어도 같은 시점에 보완하고, 한 번 발사 검사로 중복을 막습니다.
        if (!shotFired && animation.normalizedTime >= releaseNormalizedTime) FireProjectile();
        if (animation.normalizedTime >= 1f && shotFired) ChangeState(BossState.Idle);
    }

    protected override void ExitState(BossState state)
    {
        if (state != BossState.Attack || IsDead) return;
        idleDelay = Mathf.Max(0f, intervalForAttack - StateElapsed);
        if (shotsInCycle < ShotsPerCycle) return;
        shotsInCycle = 0;
        CompletedCycles++;
        speedStage = (speedStage + 1) % SpeedStageCount;
        idleDelay = cyclePause;
    }

    // Boss1_Attack의 발사 Animation Event에서 호출합니다.
    public void FireProjectile()
    {
        if (!isActiveAndEnabled || IsDead || CurrentState != BossState.Attack || shotFired) return;
        ProjectileKind kind = shotsInCycle < DirtShotsPerCycle ? ProjectileKind.Dirt : ProjectileKind.Worm;
        Boss_Bullet_Pool pool = kind == ProjectileKind.Dirt ? dirtPool : wormPool;
        if (pool.Spawn(shootPos.position, Vector2.left, transform, ownerColliders) == null)
        {
            Debug.LogError("Boss_SalSpudder: 투사체 풀에서 발사하지 못했습니다.", this);
            enabled = false;
            return;
        }
        shotFired = true;
        shotsInCycle++;
        TotalShotsFired++;
        LastProjectileKind = kind;
    }
}
