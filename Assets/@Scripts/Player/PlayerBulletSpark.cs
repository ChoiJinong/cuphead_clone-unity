using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerBulletSpark : MonoBehaviour
{
    [SerializeField] private AnimationClip sparkAnimation;
    private static readonly int SparkState = Animator.StringToHash("Base Layer.Player_bullet_spark");
    private Animator animator;
    private BulletPool pool;
    private float elapsedTime;
    private bool isSpawned;

    public void Play(BulletPool source, Vector3 position, Vector2 direction)
    {
        if (animator == null) animator = GetComponent<Animator>();
        pool = source;
        elapsedTime = 0f;
        isSpawned = true;
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
        if (!isSpawned) return;
        elapsedTime += Time.deltaTime;
        var state = animator.GetCurrentAnimatorStateInfo(0);
        bool animationFinished = state.fullPathHash == SparkState && state.normalizedTime >= 1f;
        float timeout = Mathf.Max(1f, sparkAnimation != null ? sparkAnimation.length + 0.25f : 1f);
        if (animationFinished || elapsedTime >= timeout)
        {
            isSpawned = false;
            if (pool != null) pool.Return(this);
            else gameObject.SetActive(false);
        }
    }

    public void ResetForPool()
    {
        isSpawned = false;
        elapsedTime = 0f;
        gameObject.SetActive(false);
    }
}
