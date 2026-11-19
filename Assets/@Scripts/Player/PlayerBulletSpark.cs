using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerBulletSpark : MonoBehaviour
{
    [SerializeField] private AnimationClip sparkAnimation;
    private static readonly int SparkState = Animator.StringToHash("Base Layer.Player_bullet_spark");
    private Animator animator;
    private BulletPool pool;
    private float age;
    private bool leased;

    public void Play(BulletPool source, Vector3 position, Vector2 direction)
    {
        if (animator == null) animator = GetComponent<Animator>();
        pool = source;
        age = 0f;
        leased = true;
        transform.SetPositionAndRotation(position,
            Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
        gameObject.SetActive(true);
        animator.enabled = true;
        animator.speed = 1f;
        animator.Rebind();
        animator.Play(SparkState, 0, 0f);
        animator.Update(0f);
    }

    private void Update()
    {
        if (!leased) return;
        age += Time.deltaTime;
        var state = animator.GetCurrentAnimatorStateInfo(0);
        if ((state.fullPathHash == SparkState && state.normalizedTime >= 1f)
            || age >= Mathf.Max(1f, sparkAnimation != null ? sparkAnimation.length + 0.25f : 1f))
        {
            leased = false;
            if (pool != null) pool.Return(this);
            else gameObject.SetActive(false);
        }
    }

    public void ResetForPool()
    {
        leased = false;
        age = 0f;
        gameObject.SetActive(false);
    }
}
