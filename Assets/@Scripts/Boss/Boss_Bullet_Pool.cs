using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class Boss_Bullet_Pool : MonoBehaviour
{
    [SerializeField] private Boss_Bullet projectilePrefab;
    [SerializeField, Min(0)] private int initialSize = 8;
    [SerializeField, Min(1)] private int maxPoolSize = 64;
    private ObjectPool<Boss_Bullet> pool;
    private Transform inactiveRoot;
    private readonly List<Boss_Bullet> activeProjectiles = new List<Boss_Bullet>();
    public int ActiveCount => activeProjectiles.Count;
    public int TotalCount => pool != null ? pool.CountAll : 0;

    private void Start()
    {
        if (projectilePrefab != null) Initialize();
    }

    private void Initialize()
    {
        if (pool != null) return;
        var root = new GameObject("Inactive");
        root.transform.SetParent(transform, false);
        root.SetActive(false);
        inactiveRoot = root.transform;
        int size = Mathf.Clamp(initialSize, 0, Mathf.Max(1, maxPoolSize));
        pool = new ObjectPool<Boss_Bullet>(
            createFunc: CreateProjectile, actionOnGet: null,
            actionOnRelease: projectile => projectile.ResetForPool(),
            actionOnDestroy: projectile => Destroy(projectile.gameObject),
            collectionCheck: true, defaultCapacity: Mathf.Max(1, size), maxSize: Mathf.Max(1, maxPoolSize));
        // 모두 대여한 뒤 반환해야 initialSize만큼 실제로 생성됩니다.
        var prewarmed = new Boss_Bullet[size];
        for (int i = 0; i < size; i++) prewarmed[i] = pool.Get();
        foreach (var projectile in prewarmed) pool.Release(projectile);
    }

    private Boss_Bullet CreateProjectile()
    {
        var projectile = Instantiate(projectilePrefab, inactiveRoot);
        projectile.gameObject.SetActive(false);
        projectile.transform.SetParent(transform, true);
        return projectile;
    }

    public Boss_Bullet Spawn(Vector3 position, Vector2 direction, Transform owner, Collider2D[] ownerColliders)
    {
        if (!isActiveAndEnabled || projectilePrefab == null) return null;
        Initialize();
        var projectile = pool.Get();
        activeProjectiles.Add(projectile);
        projectile.Launch(this, position, direction, owner, ownerColliders);
        return projectile;
    }

    public void Return(Boss_Bullet projectile)
    {
        if (activeProjectiles.Remove(projectile)) pool.Release(projectile);
    }

    private void OnDisable()
    {
        while (activeProjectiles.Count > 0) Return(activeProjectiles[activeProjectiles.Count - 1]);
    }

    private void OnDestroy() => pool?.Clear();
}
