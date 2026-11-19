using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class BulletPool : MonoBehaviour
{
    [SerializeField] private PlayerBullet bulletPrefab;
    [SerializeField] private PlayerBulletSpark sparkPrefab;
    [SerializeField, Min(1)] private int defaultCapacity = 24;
    [SerializeField, Min(1)] private int maxPoolSize = 128;
    private ObjectPool<PlayerBullet> bullets;
    private ObjectPool<PlayerBulletSpark> sparks;
    private readonly List<PlayerBullet> activeBullets = new List<PlayerBullet>();
    private readonly List<PlayerBulletSpark> activeSparks = new List<PlayerBulletSpark>();
    private Transform inactiveRoot;

    public int ActiveBulletCount => activeBullets.Count;
    public int ActiveSparkCount => activeSparks.Count;

    private void Initialize()
    {
        if (bullets != null) return;
        var root = new GameObject("Inactive");
        root.transform.SetParent(transform, false);
        root.SetActive(false);
        inactiveRoot = root.transform;
        int capacity = Mathf.Clamp(defaultCapacity, 1, Mathf.Max(1, maxPoolSize));
        bullets = new ObjectPool<PlayerBullet>(
            () => Create(bulletPrefab), null, b => b.ResetForPool(), b => Destroy(b.gameObject),
            true, capacity, Mathf.Max(1, maxPoolSize));
        sparks = new ObjectPool<PlayerBulletSpark>(
            () => Create(sparkPrefab), null, s => s.ResetForPool(), s => Destroy(s.gameObject),
            true, capacity, Mathf.Max(1, maxPoolSize));
    }

    private T Create<T>(T prefab) where T : Component
    {
        // 비활성 부모 아래에서 생성해 발사 설정 전에 물리·애니메이션이 실행되지 않게 합니다.
        var instance = Instantiate(prefab, inactiveRoot);
        instance.gameObject.SetActive(false);
        instance.transform.SetParent(transform, true);
        return instance;
    }

    public PlayerBullet Fire(Vector3 position, Vector2 direction, Transform owner, Collider2D[] ownerColliders)
    {
        if (!isActiveAndEnabled || bulletPrefab == null || sparkPrefab == null)
        {
            Debug.LogError("BulletPool: 활성 상태와 Bullet/Spark 프리팹 연결을 확인해 주세요.", this);
            return null;
        }
        Initialize();
        var bullet = bullets.Get();
        activeBullets.Add(bullet);
        bullet.Launch(this, position, direction, owner, ownerColliders);
        var spark = sparks.Get();
        activeSparks.Add(spark);
        spark.Play(this, position, direction);
        return bullet;
    }

    public void Return(PlayerBullet bullet)
    {
        if (activeBullets.Remove(bullet)) bullets.Release(bullet);
    }

    public void Return(PlayerBulletSpark spark)
    {
        if (activeSparks.Remove(spark)) sparks.Release(spark);
    }

    private void OnDisable()
    {
        // 풀 오브젝트 비활성화 후에도 대여 목록에 총알이 남지 않게 합니다.
        while (activeBullets.Count > 0) Return(activeBullets[activeBullets.Count - 1]);
        while (activeSparks.Count > 0) Return(activeSparks[activeSparks.Count - 1]);
    }

    private void OnDestroy()
    {
        bullets?.Clear();
        sparks?.Clear();
    }
}
