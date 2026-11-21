using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

public static class SalSpudderChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static async Task<string[]> Run()
    {
        Check(Application.isPlaying, "Enter Play mode first.");
        var sourceBoss = UnityEngine.Object.FindFirstObjectByType<Boss_SalSpudder>();
        var sourcePlayer = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        Check(sourceBoss != null && sourcePlayer != null, "Configured boss/player not found.");
        bool bossWasEnabled = sourceBoss.enabled;
        sourceBoss.enabled = false;
        var scene = UnitySceneManager.CreateScene("SalSpudderChecks", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
        var physics = scene.GetPhysicsScene2D();
        var root = new GameObject("BossChecks");
        UnitySceneManager.MoveGameObjectToScene(root, scene);
        root.SetActive(false);
        var dirtPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/@Prefabs/Sal Spudder_Bullet.prefab").GetComponent<Boss_Bullet>();
        var wormPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/@Prefabs/Sal Spudder_Worm_Temporary.prefab").GetComponent<Boss_Bullet>();
        Boss_Bullet_Pool CreatePool(string name, Boss_Bullet prefab)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform);
            var pool = go.AddComponent<Boss_Bullet_Pool>();
            var settings = new SerializedObject(pool);
            settings.FindProperty("projectilePrefab").objectReferenceValue = prefab;
            settings.FindProperty("initialSize").intValue = 2;
            settings.FindProperty("maxPoolSize").intValue = 8;
            settings.ApplyModifiedPropertiesWithoutUndo();
            return pool;
        }
        var dirtPool = CreatePool("Dirt", dirtPrefab);
        var wormPool = CreatePool("Worm", wormPrefab);
        var boss = UnityEngine.Object.Instantiate(sourceBoss.gameObject, root.transform).GetComponent<Boss_SalSpudder>();
        var bossSettings = new SerializedObject(boss);
        bossSettings.FindProperty("dirtPool").objectReferenceValue = dirtPool;
        bossSettings.FindProperty("wormPool").objectReferenceValue = wormPool;
        bossSettings.ApplyModifiedPropertiesWithoutUndo();
        boss.enabled = true;
        AnimationClip fallbackClip = null;
        AnimatorOverrideController fallbackController = null;
        try
        {
            root.SetActive(true);
            var animator = boss.GetComponent<Animator>();
            var tick = typeof(BossBase).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic);
            var changeState = typeof(BossBase).GetMethod("ChangeState", BindingFlags.Instance | BindingFlags.NonPublic);
            var results = new List<string>();
            var kinds = new List<Boss_SalSpudder.ProjectileKind>();
            var stages = new List<int>();
            var fireTimes = new List<float>();
            float clock = 0f;
            int lastShot = 0;
            void StepProjectiles(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    foreach (var bullet in root.GetComponentsInChildren<Boss_Bullet>()) bullet.SendMessage("FixedUpdate");
                    physics.Simulate(0.02f);
                    foreach (var bullet in root.GetComponentsInChildren<Boss_Bullet>())
                    {
                        bullet.GetComponent<Animator>().Update(0.02f);
                        bullet.SendMessage("Update");
                    }
                }
            }
            void StepBoss()
            {
                clock += 0.02f;
                animator.Update(0.02f);
                float normalized = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                bool wasAttacking = boss.CurrentState == BossBase.BossState.Attack;
                tick.Invoke(boss, new object[] { 0.02f });
                if (wasAttacking && boss.CurrentState == BossBase.BossState.Idle)
                    Check(normalized >= 1f, "Attack animation was interrupted.");
                if (boss.TotalShotsFired != lastShot)
                {
                    Check(boss.TotalShotsFired == lastShot + 1, "Duplicate shot in one update.");
                    lastShot = boss.TotalShotsFired;
                    kinds.Add(boss.LastProjectileKind);
                    stages.Add(boss.CurrentSpeedStage);
                    fireTimes.Add(clock);
                    var muzzle = boss.transform.Find("ShootPos");
                    Check(root.GetComponentsInChildren<Boss_Bullet>().Any(b=>Vector2.Distance(b.transform.position,muzzle.position)<0.001f),
                        "Projectile did not spawn at ShootPos.");
                    int activeBefore = dirtPool.ActiveCount + wormPool.ActiveCount;
                    boss.FireProjectile(); boss.FireProjectile();
                    changeState.Invoke(boss, new object[] { BossBase.BossState.Attack });
                    Check(boss.TotalShotsFired == lastShot && dirtPool.ActiveCount + wormPool.ActiveCount == activeBefore,
                        "Repeated event/state entry duplicated a shot.");
                }
                StepProjectiles(1);
            }

            for (int i = 0; i < 4000 && boss.CompletedCycles < 3; i++) StepBoss();
            Check(boss.CompletedCycles == 3 && boss.CurrentSpeedStage == 1 && lastShot == 12, "Stage cycle did not wrap 1/2/3/1.");
            for (int i = 0; i < 12; i++)
            {
                Check(kinds[i] == (i % 4 == 3 ? Boss_SalSpudder.ProjectileKind.Worm : Boss_SalSpudder.ProjectileKind.Dirt), "Wrong dirt/worm order.");
                Check(stages[i] == i / 4 + 1, "Stage changed during a cycle.");
            }
            var gaps = new[] { fireTimes[1]-fireTimes[0], fireTimes[5]-fireTimes[4], fireTimes[9]-fireTimes[8] };
            Check(gaps[0] > gaps[1] && gaps[1] > gaps[2], "Attack intervals do not get shorter.");
            results.Add("12 shots: Dirt/Dirt/Dirt/Worm x3; stages 1/2/3/1; ShootPos and duplicate guards OK");
            results.Add("Measured stage intervals: " + string.Join(",", gaps.Select(x=>x.ToString("F2"))));

            // Animation Event가 없는 클립으로 바꿔 보완 발사가 실제로 작동하는지 검사합니다.
            boss.enabled = false;
            var originalAttack = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/@Animations/Boss/Boss1_Attack.anim");
            fallbackClip = UnityEngine.Object.Instantiate(originalAttack);
            fallbackClip.events = new AnimationEvent[0];
            fallbackController = new AnimatorOverrideController(animator.runtimeAnimatorController);
            fallbackController[originalAttack] = fallbackClip;
            animator.runtimeAnimatorController = fallbackController;
            bossSettings.Update();
            bossSettings.FindProperty("stageOneInterval").floatValue = 0.1f;
            bossSettings.FindProperty("stageTwoInterval").floatValue = 0.08f;
            bossSettings.FindProperty("stageThreeInterval").floatValue = 0.05f;
            bossSettings.ApplyModifiedPropertiesWithoutUndo();
            boss.enabled = true;
            lastShot = 0;
            for (int i = 0; i < 1000 && boss.CompletedCycles < 1; i++) StepBoss();
            Check(boss.TotalShotsFired == 4 && boss.CompletedCycles == 1, "Missing event fallback skipped a shot.");
            boss.enabled = false;
            results.Add("Missing Animation Event fallback and intervals shorter than animation OK");
            dirtPool.gameObject.SetActive(false); wormPool.gameObject.SetActive(false);
            dirtPool.gameObject.SetActive(true); wormPool.gameObject.SetActive(true);

            var owner = new GameObject("Owner");
            owner.transform.SetParent(root.transform);
            var ownerCollider = owner.AddComponent<BoxCollider2D>();
            var owners = new Collider2D[] { ownerCollider };
            var dirt = dirtPool.Spawn(Vector3.zero, Vector2.left, owner.transform, owners);
            var worm = wormPool.Spawn(Vector3.zero, Vector2.left, owner.transform, owners);
            int dirtId = dirt.GetInstanceID(), wormId = worm.GetInstanceID();
            Check(dirtId != wormId && dirtPool.ActiveCount == 1 && wormPool.ActiveCount == 1, "Pools are not separated.");
            Check(Physics2D.GetIgnoreCollision(dirt.GetComponent<Collider2D>(), ownerCollider), "Owner collision not ignored.");
            Check(dirt.Direction == Vector2.left && dirt.GetComponent<Rigidbody2D>().linearVelocity.x < 0f, "Not moving left.");
            StepProjectiles(2);
            Check(!dirt.IsHitting && !worm.IsHitting, "Owner triggered Hit.");
            dirt.ReturnToPool(); dirt.ReturnToPool(); worm.ReturnToPool();
            Check(dirtPool.ActiveCount == 0 && wormPool.ActiveCount == 0, "Double return/own pool return failed.");
            Check(!dirt.GetComponent<Rigidbody2D>().simulated && !dirt.GetComponent<Collider2D>().enabled, "Returned bullet still simulates.");
            var reused = dirtPool.Spawn(Vector3.zero, Vector2.left, owner.transform, owners);
            Check(reused.GetInstanceID() == dirtId && !reused.IsHitting && reused.GetComponent<Collider2D>().enabled, "Reuse initialization failed.");
            reused.ReturnToPool();
            results.Add("Separate pools, prewarming, leftward motion, owner immunity and reuse/double return OK");

            var target = UnityEngine.Object.Instantiate(sourcePlayer.gameObject, root.transform);
            target.GetComponent<PlayerController>().enabled = false;
            target.GetComponent<PlayerAttack>().enabled = false;
            target.transform.position = new Vector3(-3,0,0);
            foreach (var collider in target.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
            target.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            target.AddComponent<BoxCollider2D>().size = new Vector2(0.02f,2f);
            var hit = dirtPool.Spawn(Vector3.zero, Vector2.left, owner.transform, owners);
            StepProjectiles(30);
            Check(hit.IsHitting && hit.gameObject.activeSelf, "Thin player collider was missed.");
            Check(hit.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Sal Spudder_Bullet_Death"), "Hit animation missing.");
            Check(hit.GetComponent<SpriteRenderer>().sprite != null && hit.GetComponent<Rigidbody2D>().linearVelocity == Vector2.zero, "Hit sprite/stop failed.");
            StepProjectiles(30);
            Check(!hit.gameObject.activeSelf, "Hit did not return after animation.");
            target.SetActive(false);
            results.Add("Player collision sweep, stopped Hit animation and completion return OK (no health/damage added)");

            var ranged = dirtPool.Spawn(Vector3.zero, Vector2.left, owner.transform, owners);
            var projectileSettings = new SerializedObject(ranged);
            projectileSettings.FindProperty("maxDistance").floatValue = .4f;
            projectileSettings.ApplyModifiedPropertiesWithoutUndo();
            StepProjectiles(5);
            Check(!ranged.gameObject.activeSelf, "Distance expiry failed.");
            var timed = dirtPool.Spawn(Vector3.zero, Vector2.left, owner.transform, owners);
            projectileSettings = new SerializedObject(timed);
            projectileSettings.FindProperty("maxDistance").floatValue = 30f;
            projectileSettings.FindProperty("maxLifetime").floatValue = .1f;
            projectileSettings.ApplyModifiedPropertiesWithoutUndo();
            await Task.Delay(250);
            Check(!timed.gameObject.activeSelf, "Lifetime expiry failed.");
            root.SetActive(false);
            Check(dirtPool.ActiveCount == 0 && wormPool.ActiveCount == 0, "Pool disable cleanup failed.");
            results.Add("Distance/lifetime expiry and pool disable cleanup OK");
            return results.ToArray();
        }
        finally
        {
            root.SetActive(false);
            UnityEngine.Object.Destroy(root);
            if (fallbackClip != null) UnityEngine.Object.Destroy(fallbackClip);
            if (fallbackController != null) UnityEngine.Object.Destroy(fallbackController);
            _ = UnitySceneManager.UnloadSceneAsync(scene);
            sourceBoss.enabled = bossWasEnabled;
        }
    }
}
