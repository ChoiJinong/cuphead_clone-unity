// Unity Editor 플레이 모드에서 Pipeline script run으로 실행하는 검증입니다.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

public static class PlayerAttackChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static async Task<string[]> Run()
    {
        Check(Application.isPlaying, "Enter Play mode before running checks.");
        foreach (var name in new[]{ "Player_bullet", "Player_bullet_death", "Player_bullet_spark" })
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/@Animations/Player_Bullet/"+name+".anim");
            var frames = AnimationUtility.GetObjectReferenceCurveBindings(clip)
                .SelectMany(binding=>AnimationUtility.GetObjectReferenceCurve(clip,binding)).ToArray();
            Check(frames.Length > 0 && frames.All(frame=>frame.value is Sprite), name+" has missing sprite frames");
        }
        var results = new List<string>();
        var scene = UnitySceneManager.CreateScene("PlayerAttackChecks", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
        var physics = scene.GetPhysicsScene2D();
        var root = new GameObject("CombatChecks");
        UnitySceneManager.MoveGameObjectToScene(root, scene);
        var bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/@Prefabs/Player_Bullet.prefab").GetComponent<PlayerBullet>();
        var sparkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/@Prefabs/Player_Bullet_Spark.prefab").GetComponent<PlayerBulletSpark>();
        var pool = root.AddComponent<BulletPool>();
        var settings = new SerializedObject(pool);
        settings.FindProperty("bulletPrefab").objectReferenceValue = bulletPrefab;
        settings.FindProperty("sparkPrefab").objectReferenceValue = sparkPrefab;
        settings.ApplyModifiedPropertiesWithoutUndo();
        var owner = new GameObject("Owner");
        owner.transform.SetParent(root.transform);
        owner.transform.position = new Vector3(0, 4, 0);
        var ownerCollider = owner.AddComponent<BoxCollider2D>();
        var ownerColliders = new Collider2D[] { ownerCollider };
        var realPlayer = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        var originalAttack = realPlayer.GetComponent<PlayerAttack>();
        bool originalAttackEnabled = originalAttack.enabled;
        originalAttack.enabled = false;
        Keyboard keyboard = null;
        var originalKeyboard = Keyboard.current;
        try
        {
            void Step(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    foreach (var bullet in root.GetComponentsInChildren<PlayerBullet>())
                        bullet.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
                    physics.Simulate(0.02f);
                    foreach (var animator in root.GetComponentsInChildren<Animator>()) animator.Update(0.02f);
                    foreach (var bullet in root.GetComponentsInChildren<PlayerBullet>())
                        bullet.SendMessage("Update", SendMessageOptions.RequireReceiver);
                    foreach (var spark in root.GetComponentsInChildren<PlayerBulletSpark>())
                        spark.SendMessage("Update", SendMessageOptions.RequireReceiver);
                }
            }

            int reusedId = 0;
            foreach (var direction in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down,
                new Vector2(1,1), new Vector2(-1,1), new Vector2(1,-1), new Vector2(-1,-1) })
            {
                var bullet = pool.Fire(owner.transform.position, direction, owner.transform, ownerColliders);
                Check(bullet != null, "Pool did not spawn a bullet");
                if (reusedId != 0) Check(bullet.GetInstanceID() == reusedId, "Bullet was not reused");
                reusedId = bullet.GetInstanceID();
                var body = bullet.GetComponent<Rigidbody2D>();
                Check(Mathf.Abs(body.linearVelocity.magnitude - 22f) < 0.01f, "Diagonal speed differs");
                Check(Vector2.Dot(body.linearVelocity.normalized, direction.normalized) > 0.999f, "Wrong bullet direction");
                var spark = root.GetComponentsInChildren<PlayerBulletSpark>().Last();
                Check(Vector2.Dot((Vector2)spark.transform.right, direction.normalized) > 0.999f, "Wrong spark rotation");
                Step(1);
                Check(!bullet.IsHitting, "Owner collision was not ignored");
                bullet.ReturnToPool(); bullet.ReturnToPool();
                Check(pool.ActiveBulletCount == 0 && !body.simulated && !bullet.gameObject.activeSelf, "Return/reset failed");
            }
            Step(20);
            Check(pool.ActiveSparkCount == 0, "Spark did not return after its animation");
            results.Add("8 directions, normalized speed, Spark rotation, owner immunity, reuse and duplicate return OK");

            var wall = new GameObject("ThinWall");
            wall.transform.SetParent(root.transform);
            wall.transform.position = new Vector3(2, 4, 0);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(0.02f, 3f);
            var shot = pool.Fire(owner.transform.position, Vector2.right, owner.transform, ownerColliders);
            int hitId = shot.GetInstanceID();
            Step(8);
            Check(shot.IsHitting && shot.gameObject.activeSelf, "Thin-wall hit did not play on the same object");
            Check(shot.GetInstanceID() == hitId && shot.GetComponent<Rigidbody2D>().linearVelocity == Vector2.zero,
                "Hit replaced or moved the bullet");
            Check(!shot.GetComponent<Collider2D>().enabled, "Hit collider was not disabled");
            Check(Mathf.Abs(shot.transform.position.x - 1.99f) < 0.003f,
                "Death animation is not centered on the wall surface");
            Check(shot.GetComponent<SpriteRenderer>().sprite != null, "Hit animation has no visible sprite");
            Step(25);
            Check(!shot.gameObject.activeSelf && pool.ActiveBulletCount == 0, "Hit animation did not return the bullet");
            wall.SetActive(false);
            var reused = pool.Fire(owner.transform.position, Vector2.left, owner.transform, ownerColliders);
            Check(reused.GetInstanceID() == hitId && !reused.IsHitting && reused.GetComponent<Collider2D>().enabled,
                "Hit state was not reset on reuse");
            Check(reused.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Player_bullet"), "Flight state not reset");
            reused.ReturnToPool();
            results.Add("Thin-wall collision, same-object Hit animation and clean flight reuse OK");

            var ranged = pool.Fire(owner.transform.position, Vector2.up, owner.transform, ownerColliders);
            var bulletSettings = new SerializedObject(ranged);
            bulletSettings.FindProperty("maxDistance").floatValue = 0.9f;
            bulletSettings.ApplyModifiedPropertiesWithoutUndo();
            Step(5);
            Check(!ranged.gameObject.activeSelf, "Distance expiry failed");
            var timed = pool.Fire(owner.transform.position, Vector2.up, owner.transform, ownerColliders);
            bulletSettings = new SerializedObject(timed);
            bulletSettings.FindProperty("maxDistance").floatValue = 25f;
            bulletSettings.FindProperty("maxLifetime").floatValue = 0.05f;
            bulletSettings.ApplyModifiedPropertiesWithoutUndo();
            await Task.Delay(200);
            Check(!timed.gameObject.activeSelf, "Lifetime expiry failed");
            bulletSettings.FindProperty("maxLifetime").floatValue = 2f;
            bulletSettings.ApplyModifiedPropertiesWithoutUndo();
            results.Add("Distance and lifetime auto-return OK");

            var inactive = new GameObject("InactiveRig");
            inactive.transform.SetParent(root.transform);
            inactive.SetActive(false);
            var rig = UnityEngine.Object.Instantiate(realPlayer.gameObject, inactive.transform);
            rig.transform.position = new Vector3(0, 10, 0);
            var rigAttack = rig.GetComponent<PlayerAttack>();
            var attackSettings = new SerializedObject(rigAttack);
            attackSettings.FindProperty("bulletPool").objectReferenceValue = pool;
            attackSettings.ApplyModifiedPropertiesWithoutUndo();
            rigAttack.enabled = true;
            inactive.SetActive(true);
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift, Key.UpArrow, Key.LeftArrow));
            InputSystem.Update();
            var rigMovement = rig.GetComponent<PlayerMovement>();
            for (int i = 0; i < 4; i++)
            {
                float horizontal = rigMovement.ReadInput(keyboard);
                horizontal = rigAttack.ReadInput(keyboard, horizontal);
                rigMovement.ApplyInput(horizontal);
                rigAttack.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
                await Task.Delay(130);
            }
            int heldCount = pool.ActiveBulletCount;
            Check(heldCount >= 2 && heldCount <= 5, "Held firing cadence failed: " + heldCount);
            var bullets = root.GetComponentsInChildren<PlayerBullet>();
            Check(bullets.All(b=>Vector2.Dot(b.Direction, new Vector2(-1,1).normalized)>0.999f), "Combat aim not connected");
            Check(rig.transform.Find("ShootPos").localPosition.x < 0, "ShootPos was not mirrored for left aim");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            rigAttack.ReadInput(keyboard, 0f);
            rigAttack.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            await Task.Delay(200);
            Check(pool.ActiveBulletCount == heldCount, "Firing continued after key release");
            results.Add("Shift held cadence/release, airborne 8-way aim and ShootPos placement OK (shots="+heldCount+")");
            rigAttack.enabled = false;
            root.SetActive(false);
            Check(pool.ActiveBulletCount == 0 && pool.ActiveSparkCount == 0, "Pool disable leaked active objects");
            results.Add("Pool disable cleanup OK");
            return results.ToArray();
        }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (originalKeyboard != null) originalKeyboard.MakeCurrent();
            originalAttack.enabled = originalAttackEnabled;
            UnityEngine.Object.Destroy(root);
            _ = UnitySceneManager.UnloadSceneAsync(scene);
        }
    }
}
