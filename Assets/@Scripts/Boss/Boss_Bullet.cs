using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(Animator))]
public class Boss_Bullet : MonoBehaviour
{
    [SerializeField, Min(0f)] private float speed = 7f;
    [SerializeField, Min(0.1f)] private float maxLifetime = 6f;
    [SerializeField, Min(0.1f)] private float maxDistance = 30f;
    [SerializeField] private LayerMask playerLayers = ~0;
    [SerializeField] private string flyState = "Base Layer.Sal Spudder_Bullet_Shoot";
    [SerializeField] private string hitState = "Base Layer.Sal Spudder_Bullet_Death";
    [SerializeField] private AnimationClip hitAnimation;
    [SerializeField, Min(0)] private int damage = 1;
    [SerializeField] private bool isParryable;

    public bool IsParryable => isParryable;

    private readonly RaycastHit2D[] castHits = new RaycastHit2D[32];
    private Rigidbody2D body;
    private Collider2D projectileCollider;
    private Animator animator;
    private Boss_Bullet_Pool sourcePool;
    private Transform owner;
    private Collider2D[] ownerColliders;
    private Vector2 direction;
    private Vector2 startPosition;
    private Vector3 originalScale;
    private int flyStateHash;
    private int hitStateHash;
    private float lifetimeElapsed;
    private float hitElapsed;
    private bool isSpawned;
    public bool IsHitting { get; private set; }
    public Vector2 Direction => direction;

    private void Awake() => CacheComponents();

    private void CacheComponents()
    {
        if (body != null) return;
        body = GetComponent<Rigidbody2D>();
        projectileCollider = GetComponent<Collider2D>();
        animator = GetComponent<Animator>();
        originalScale = transform.localScale;
        flyStateHash = Animator.StringToHash(flyState);
        hitStateHash = Animator.StringToHash(hitState);
    }

    public void Launch(Boss_Bullet_Pool pool, Vector3 position, Vector2 aim, Transform shooter, Collider2D[] shooterColliders)
    {
        CacheComponents();
        sourcePool = pool;
        owner = shooter;
        ownerColliders = shooterColliders;
        direction = aim.sqrMagnitude > 0f ? aim.normalized : Vector2.left;
        startPosition = position;
        lifetimeElapsed = hitElapsed = 0f;
        isSpawned = true;
        IsHitting = false;
        transform.localScale = originalScale;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
        body.position = position;
        body.rotation = transform.eulerAngles.z;
        body.gravityScale = 0f;
        body.angularVelocity = 0f;
        body.simulated = true;
        projectileCollider.enabled = true;
        gameObject.SetActive(true);
        IgnoreOwner(true);
        animator.enabled = true;
        animator.speed = 1f;
        animator.Rebind();
        animator.Play(flyStateHash, 0, 0f);
        animator.Update(0f);
        body.linearVelocity = direction * speed;
        body.WakeUp();
    }

    private void FixedUpdate()
    {
        if (!isSpawned || IsHitting) return;
        float remaining = maxDistance - Vector2.Distance(startPosition, body.position);
        if (remaining <= 0f) { ReturnToPool(); return; }
        float step = Mathf.Min(speed * Time.fixedDeltaTime, remaining);
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = playerLayers, useTriggers = true };
        int count = body.Cast(direction, filter, castHits, step);
        float nearest = float.PositiveInfinity;
        Vector2 point = body.position;
        Collider2D target = null;
        for (int i = 0; i < count; i++)
        {
            if (!CanHit(castHits[i].collider) || castHits[i].distance >= nearest) continue;
            nearest = castHits[i].distance;
            target = castHits[i].collider;
            point = castHits[i].collider.ClosestPoint(castHits[i].distance > 0f ? castHits[i].point : body.position);
        }
        if (!float.IsPositiveInfinity(nearest)) { PlayHit(point, target); return; }
        body.linearVelocity = direction * (step / Time.fixedDeltaTime);
    }

    private bool CanHit(Collider2D other)
    {
        return other != null && (owner == null || !other.transform.IsChildOf(owner))
            && (playerLayers.value & (1 << other.gameObject.layer)) != 0
            && other.GetComponentInParent<PlayerHealth>() != null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (CanHit(other)) PlayHit(other.ClosestPoint(body.position), other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (CanHit(collision.collider))
            PlayHit(collision.contactCount > 0 ? collision.GetContact(0).point
                : collision.collider.ClosestPoint(body.position), collision.collider);
    }

    private void PlayHit(Vector2 position, Collider2D target)
    {
        if (!isSpawned || IsHitting) return;
        IsHitting = true;
        if (isParryable && target != null)
        {
            var parry = target.GetComponentInParent<PlayerParry>();
            if (parry != null && parry.TryParry())
            {
                ReturnToPool();
                return;
            }
        }
        hitElapsed = 0f;
        StopPhysics();
        body.position = position;
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        animator.Play(hitStateHash, 0, 0f);
        animator.Update(0f);
        if (target != null)
        {
            var health = target.GetComponentInParent<PlayerHealth>();
            // 무적으로 데미지를 받지 않아도 Hit 재생과 풀 반환은 그대로 진행합니다.
            if (health != null) health.TakeDamage(damage);
        }
    }

    private void Update()
    {
        if (!isSpawned) return;
        if (IsHitting)
        {
            hitElapsed += Time.deltaTime;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            float timeout = Mathf.Max(1f, hitAnimation != null ? hitAnimation.length + 0.25f : 1f);
            if ((state.fullPathHash == hitStateHash && state.normalizedTime >= 1f) || hitElapsed >= timeout)
                ReturnToPool();
            return;
        }
        lifetimeElapsed += Time.deltaTime;
        if (lifetimeElapsed >= maxLifetime || Vector2.Distance(startPosition, body.position) >= maxDistance)
            ReturnToPool();
    }

    private void StopPhysics()
    {
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.simulated = false;
        projectileCollider.enabled = false;
    }

    private void IgnoreOwner(bool ignore)
    {
        if (ownerColliders == null) return;
        foreach (var collider in ownerColliders)
            if (collider != null) Physics2D.IgnoreCollision(projectileCollider, collider, ignore);
    }

    public void ReturnToPool()
    {
        if (!isSpawned) return;
        isSpawned = false;
        if (sourcePool != null) sourcePool.Return(this);
        else ResetForPool();
    }

    public void ResetForPool()
    {
        CacheComponents();
        IgnoreOwner(false);
        StopPhysics();
        isSpawned = false;
        IsHitting = false;
        lifetimeElapsed = hitElapsed = 0f;
        direction = Vector2.zero;
        owner = null;
        ownerColliders = null;
        gameObject.SetActive(false);
    }
}
