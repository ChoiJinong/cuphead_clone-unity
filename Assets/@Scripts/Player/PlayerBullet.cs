using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(Animator))]
public class PlayerBullet : MonoBehaviour
{
    [SerializeField, Min(0f)] private float speed = 22f;
    [SerializeField, Min(0.05f)] private float maxLifetime = 2f;
    [SerializeField, Min(0.1f)] private float maxDistance = 25f;
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField] private AnimationClip hitAnimation;

    private static readonly int FlyState = Animator.StringToHash("Base Layer.Player_bullet");
    private static readonly int HitState = Animator.StringToHash("Base Layer.Player_bullet_death");
    private readonly RaycastHit2D[] sweepHits = new RaycastHit2D[32];
    private Rigidbody2D body;
    private Collider2D bulletCollider;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private BulletPool pool;
    private Transform owner;
    private Collider2D[] ownerColliders;
    private Vector2 direction;
    private Vector2 spawnPosition;
    private Vector3 initialScale;
    private RigidbodyInterpolation2D flightInterpolation;
    private float age;
    private float hitAge;
    private bool leased;
    private bool hitting;

    public bool IsHitting => hitting;
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
        ownerColliders = shooterColliders;
        direction = aim.sqrMagnitude > 0f ? aim.normalized : Vector2.right;
        spawnPosition = position;
        age = hitAge = 0f;
        hitting = false;
        leased = true;
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
        if (!leased || hitting) return;
        float remaining = maxDistance - Vector2.Distance(spawnPosition, body.position);
        if (remaining <= 0f)
        {
            ReturnToPool();
            return;
        }

        // 빠른 총알이 얇은 벽을 통과하지 않도록 다음 물리 이동 구간을 검사합니다.
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = hitLayers, useTriggers = true };
        float step = Mathf.Min(speed * Time.fixedDeltaTime, remaining);
        int count = body.Cast(direction, filter, sweepHits, step);
        float nearest = float.PositiveInfinity;
        Vector2 hitPoint = body.position;
        for (int i = 0; i < count; i++)
        {
            if (IsTarget(sweepHits[i].collider) && sweepHits[i].distance < nearest)
            {
                nearest = sweepHits[i].distance;
                hitPoint = sweepHits[i].collider.ClosestPoint(
                    sweepHits[i].distance > 0f ? sweepHits[i].point : body.position);
            }
        }
        if (!float.IsPositiveInfinity(nearest))
        {
            Hit(hitPoint);
            return;
        }
        body.linearVelocity = direction * (step / Time.fixedDeltaTime);
    }

    private void Update()
    {
        if (!leased) return;
        if (hitting)
        {
            hitAge += Time.deltaTime;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if ((state.fullPathHash == HitState && state.normalizedTime >= 1f)
                || hitAge >= Mathf.Max(1f, hitAnimation != null ? hitAnimation.length + 0.25f : 1f))
                ReturnToPool();
            return;
        }
        age += Time.deltaTime;
        if (age >= maxLifetime || Vector2.Distance(spawnPosition, body.position) >= maxDistance)
            ReturnToPool();
    }

    private bool IsTarget(Collider2D other)
    {
        return other != null && other != bulletCollider
            && (owner == null || !other.transform.IsChildOf(owner))
            && other.GetComponentInParent<PlayerBullet>() == null
            && (hitLayers.value & (1 << other.gameObject.layer)) != 0;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsTarget(other)) Hit(other.ClosestPoint(body.position));
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsTarget(collision.collider))
            Hit(collision.contactCount > 0 ? collision.GetContact(0).point
                : collision.collider.ClosestPoint(body.position));
    }

    private void Hit(Vector2 point)
    {
        if (!leased || hitting) return;
        hitting = true;
        hitAge = 0f;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        bulletCollider.enabled = false;
        body.simulated = false;
        // Death 스프라이트의 중앙을 총알 원점 대신 실제 접촉 지점에 맞춥니다.
        body.interpolation = RigidbodyInterpolation2D.None;
        body.position = point;
        transform.position = new Vector3(point.x, point.y, transform.position.z);
        animator.Play(HitState, 0, 0f);
        animator.Update(0f);
    }

    public void ReturnToPool()
    {
        if (!leased) return;
        leased = false; // 여러 충돌 콜백이 와도 한 번만 반환합니다.
        if (pool != null) pool.Return(this);
        else gameObject.SetActive(false);
    }

    private void IgnoreOwner(bool ignore)
    {
        if (ownerColliders == null) return;
        foreach (var collider in ownerColliders)
            if (collider != null) Physics2D.IgnoreCollision(bulletCollider, collider, ignore);
    }

    public void ResetForPool()
    {
        CacheComponents();
        IgnoreOwner(false);
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        bulletCollider.enabled = false;
        body.simulated = false;
        leased = hitting = false;
        age = hitAge = 0f;
        direction = Vector2.zero;
        owner = null;
        ownerColliders = null;
        gameObject.SetActive(false);
    }
}
