using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(Animator))]
public class PlayerBullet : MonoBehaviour
{
    [SerializeField, Min(0f)] private float speed = 22f;
    [SerializeField, Min(0.05f)] private float maxLifetime = 2f;
    [SerializeField, Min(0.1f)] private float maxDistance = 25f;
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField] private AnimationClip hitAnimation;
    [SerializeField, Min(0)] private int damage = 4;

    private static readonly int FlyState = Animator.StringToHash("Base Layer.Player_bullet");
    private static readonly int HitState = Animator.StringToHash("Base Layer.Player_bullet_death");
    private readonly RaycastHit2D[] sweepHits = new RaycastHit2D[32];
    private Rigidbody2D body;
    private Collider2D bulletCollider;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private BulletPool pool;
    private Transform owner;
    private PlayerCardGauge ownerCards;
    private Collider2D[] ownerColliders;
    private Vector2 direction;
    private Vector2 spawnPosition;
    private Vector3 initialScale;
    private RigidbodyInterpolation2D flightInterpolation;
    private float lifetimeElapsed;
    private float hitElapsed;
    private bool isSpawned;
    private bool isHitting;

    public bool IsHitting => isHitting;
    public Vector2 Direction => direction;

    private void Awake() => CacheComponents();

    private void CacheComponents()
    {
        if (body != null) return;
        body = GetComponent<Rigidbody2D>();
        bulletCollider = GetComponent<Collider2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        initialScale = transform.localScale;
        flightInterpolation = body.interpolation;
    }

    public void Launch(BulletPool source, Vector3 position, Vector2 aim, Transform shooter, Collider2D[] shooterColliders)
    {
        CacheComponents();
        pool = source;
        owner = shooter;
        ownerCards = shooter != null ? shooter.GetComponentInParent<PlayerCardGauge>() : null;
        ownerColliders = shooterColliders;
        direction = aim.sqrMagnitude > 0f ? aim.normalized : Vector2.right;
        spawnPosition = position;
        lifetimeElapsed = hitElapsed = 0f;
        isHitting = false;
        isSpawned = true;
        transform.localScale = initialScale;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
        body.position = position;
        body.rotation = transform.eulerAngles.z;
        body.gravityScale = 0f;
        body.angularVelocity = 0f;
        body.simulated = true;
        body.interpolation = flightInterpolation;
        bulletCollider.enabled = true;
        gameObject.SetActive(true);
        IgnoreOwner(true);
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            spriteRenderer.flipX = spriteRenderer.flipY = false;
            spriteRenderer.color = Color.white;
        }
        animator.enabled = true;
        animator.speed = 1f;
        animator.Rebind();
        animator.Play(FlyState, 0, 0f);
        animator.Update(0f);
        body.linearVelocity = direction * speed;
        body.WakeUp();
    }

    private void FixedUpdate()
    {
        if (!isSpawned || isHitting) return;
        float remaining = maxDistance - Vector2.Distance(spawnPosition, body.position);
        if (remaining <= 0f)
        {
            ReturnToPool();
            return;
        }

        // 빠른 총알이 얇은 벽을 통과하지 않도록 다음 물리 이동 구간을 검사합니다.
        float step = Mathf.Min(speed * Time.fixedDeltaTime, remaining);
        if (TryGetHitPoint(step, out Vector2 hitPoint, out Collider2D hitCollider))
        {
            PlayHitAnimation(hitPoint, hitCollider);
            return;
        }
        body.linearVelocity = direction * (step / Time.fixedDeltaTime);
    }

    private bool TryGetHitPoint(float distance, out Vector2 hitPoint, out Collider2D hitCollider)
    {
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = hitLayers, useTriggers = true };
        int count = body.Cast(direction, filter, sweepHits, distance);
        float nearest = float.PositiveInfinity;
        hitPoint = body.position;
        hitCollider = null;
        for (int i = 0; i < count; i++)
        {
            if (CanHit(sweepHits[i].collider) && sweepHits[i].distance < nearest)
            {
                nearest = sweepHits[i].distance;
                hitCollider = sweepHits[i].collider;
                hitPoint = sweepHits[i].collider.ClosestPoint(
                    sweepHits[i].distance > 0f ? sweepHits[i].point : body.position);
            }
        }
        return !float.IsPositiveInfinity(nearest);
    }

    private void Update()
    {
        if (!isSpawned) return;
        if (isHitting)
        {
            UpdateHitAnimation();
            return;
        }
        lifetimeElapsed += Time.deltaTime;
        if (lifetimeElapsed >= maxLifetime || Vector2.Distance(spawnPosition, body.position) >= maxDistance)
            ReturnToPool();
    }

    private void UpdateHitAnimation()
    {
        hitElapsed += Time.deltaTime;
        var state = animator.GetCurrentAnimatorStateInfo(0);
        bool animationFinished = state.fullPathHash == HitState && state.normalizedTime >= 1f;
        // Animator 연결이 잘못되어도 총알이 풀 밖에 영구히 남지 않도록 합니다.
        float timeout = Mathf.Max(1f, hitAnimation != null ? hitAnimation.length + 0.25f : 1f);
        if (animationFinished || hitElapsed >= timeout)
            ReturnToPool();
    }

    private bool CanHit(Collider2D other)
    {
        return other != null && other != bulletCollider
            && (owner == null || !other.transform.IsChildOf(owner))
            && other.GetComponentInParent<PlayerBullet>() == null
            && (hitLayers.value & (1 << other.gameObject.layer)) != 0;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (CanHit(other)) PlayHitAnimation(other.ClosestPoint(body.position), other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (CanHit(collision.collider))
            PlayHitAnimation(collision.contactCount > 0 ? collision.GetContact(0).point
                : collision.collider.ClosestPoint(body.position), collision.collider);
    }

    private void PlayHitAnimation(Vector2 point, Collider2D target)
    {
        if (!isSpawned || isHitting) return;
        isHitting = true;
        hitElapsed = 0f;
        StopPhysics();
        // Death 스프라이트의 중앙을 총알 원점 대신 실제 접촉 지점에 맞춥니다.
        body.interpolation = RigidbodyInterpolation2D.None;
        body.position = point;
        transform.position = new Vector3(point.x, point.y, transform.position.z);
        animator.Play(HitState, 0, 0f);
        animator.Update(0f);
        // isHitting을 먼저 설정해 여러 Collider/콜백에서도 한 발당 한 번만 적용합니다.
        if (target != null)
        {
            var boss = target.GetComponentInParent<BossBase>();
            // 보스 사망 이벤트에서 풀이 회수되어도 마지막 명중의 충전 대상을 보존합니다.
            var cards = ownerCards;
            if (boss != null && boss.TakeDamage(damage) && cards != null)
                cards.ChargeFromNormalAttack();
        }
    }

    public void ReturnToPool()
    {
        if (!isSpawned) return;
        isSpawned = false; // 여러 충돌 콜백이 와도 한 번만 반환합니다.
        if (pool != null) pool.Return(this);
        else gameObject.SetActive(false);
    }

    private void IgnoreOwner(bool ignore)
    {
        if (ownerColliders == null) return;
        foreach (var collider in ownerColliders)
            if (collider != null) Physics2D.IgnoreCollision(bulletCollider, collider, ignore);
    }

    private void StopPhysics()
    {
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        bulletCollider.enabled = false;
        body.simulated = false;
    }

    public void ResetForPool()
    {
        CacheComponents();
        IgnoreOwner(false);
        StopPhysics();
        isSpawned = isHitting = false;
        lifetimeElapsed = hitElapsed = 0f;
        direction = Vector2.zero;
        owner = null;
        ownerCards = null;
        ownerColliders = null;
        gameObject.SetActive(false);
    }
}
