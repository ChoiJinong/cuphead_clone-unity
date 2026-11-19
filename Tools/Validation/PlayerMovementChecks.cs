// Play 모드에서 script run --entry PlayerMovementChecks.Run으로 실행합니다.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

public static class PlayerMovementChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static string[] Run()
    {
        Check(Application.isPlaying, "Enter Play mode.");
        var realPlayer = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        var scene = UnitySceneManager.CreateScene("PlayerMovementChecks", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
        var physics = scene.GetPhysicsScene2D();
        var root = new GameObject("MovementChecks");
        UnitySceneManager.MoveGameObjectToScene(root, scene);
        root.SetActive(false);
        var rig = UnityEngine.Object.Instantiate(realPlayer.gameObject, root.transform);
        var floor = new GameObject("Floor");
        floor.transform.SetParent(root.transform);
        floor.transform.position = new Vector3(0, -0.5f, 0);
        floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
        var controller = rig.GetComponent<PlayerController>();
        var movement = rig.GetComponent<PlayerMovement>();
        var attack = rig.GetComponent<PlayerAttack>();
        var body = rig.GetComponent<Rigidbody2D>();
        var animator = rig.GetComponent<Animator>();
        var settings = new SerializedObject(movement);
        float jumpSpeed = settings.FindProperty("jumpSpeed").floatValue;
        float jumpCut = settings.FindProperty("jumpCutMultiplier").floatValue;
        float expectedDashDistance = settings.FindProperty("dashSpeed").floatValue * settings.FindProperty("dashDuration").floatValue;
        var originalKeyboard = Keyboard.current;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            rig.transform.position = new Vector3(0, 3, 0);
            root.SetActive(true);
            controller.enabled = false;
            attack.enabled = false;
            void Input(params Key[] keys)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
                InputSystem.Update();
                float horizontal = movement.ReadInput(keyboard);
                horizontal = attack.ReadInput(keyboard, horizontal);
                movement.ApplyInput(horizontal);
                attack.UpdateAnimation();
            }
            void Tick()
            {
                controller.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
                physics.Simulate(Time.fixedDeltaTime);
                animator.Update(Time.fixedDeltaTime);
            }
            for (int i = 0; i < 100; i++) physics.Simulate(Time.fixedDeltaTime);
            Input(); Tick();
            Check(movement.IsGrounded, "Ground check failed");
            Vector2 groundPosition = body.position;
            void ResetToGround()
            {
                movement.ResetState(); attack.ResetState();
                body.position = groundPosition;
                body.linearVelocity = Vector2.zero;
                for (int i = 0; i < 5; i++) physics.Simulate(Time.fixedDeltaTime);
                Input(); Tick();
            }
            var results = new List<string>();
            Input(Key.RightArrow);
            controller.SendMessage("FixedUpdate");
            Check(Mathf.Abs(body.linearVelocity.x - movement.MoveSpeed) < 0.001f, "Move velocity changed");
            for (int i = 0; i < 5; i++) Tick();
            float moveDistance = body.position.x - groundPosition.x;
            Check(moveDistance > 0.3f && moveDistance < 0.6f, "Movement did not advance");
            Input(Key.LeftArrow); Tick();
            Check(rig.GetComponent<SpriteRenderer>().flipX, "Facing left failed");
            results.Add("Arrow movement speed=5, test floor distance=" + moveDistance + ", facing preserved");

            ResetToGround(); Input(Key.DownArrow); Tick();
            Check(attack.IsDucking && movement.HorizontalInput == 0, "Duck entry failed");
            Check(animator.GetCurrentAnimatorStateInfo(1).IsName("Player_duck"), "Duck animation failed");
            animator.Update(0.7f); animator.Update(0.02f);
            Check(animator.GetCurrentAnimatorStateInfo(1).IsName("Player_duck_idle"), "Duck idle failed");
            Input(Key.DownArrow, Key.LeftShift); animator.Update(0.02f);
            Check(animator.GetCurrentAnimatorStateInfo(1).IsName("Player_duck_shoot"), "Duck shooting failed");
            Input(Key.Z, Key.DownArrow); animator.Update(0.02f);
            Check(!attack.IsDucking && attack.IsAimLocked && attack.AimDirection == Vector2.down,
                "Z/down aim conflicts with duck");
            results.Add("Duck -> idle -> shoot and Z/down aim preserved");

            ResetToGround();
            var aimInputs = new[]
            {
                new[] { Key.RightArrow }, new[] { Key.LeftArrow }, new[] { Key.UpArrow }, new[] { Key.DownArrow },
                new[] { Key.RightArrow, Key.UpArrow }, new[] { Key.LeftArrow, Key.UpArrow },
                new[] { Key.RightArrow, Key.DownArrow }, new[] { Key.LeftArrow, Key.DownArrow }
            };
            var expectedDirections = new[]
            {
                Vector2.right, Vector2.left, Vector2.up, Vector2.down,
                new Vector2(1,1).normalized, new Vector2(-1,1).normalized,
                new Vector2(1,-1).normalized, new Vector2(-1,-1).normalized
            };
            for (int i = 0; i < aimInputs.Length; i++)
            {
                var keys = new List<Key>(aimInputs[i]) { Key.Z };
                Input(keys.ToArray());
                Check(attack.IsAimLocked && movement.HorizontalInput == 0
                    && Vector2.Distance(attack.AimDirection, expectedDirections[i]) < 0.001f,
                    "Locked aim direction changed: " + i);
            }
            Input(Key.Z);
            Check(Vector2.Distance(attack.AimDirection, expectedDirections[7]) < 0.001f,
                "Locked aim did not retain its direction");
            results.Add("All 8 locked aim directions and last direction retention preserved");

            ResetToGround(); Input(Key.C);
            controller.SendMessage("FixedUpdate");
            Check(Mathf.Abs(body.linearVelocity.y - jumpSpeed) < 0.001f, "Jump speed changed");
            physics.Simulate(Time.fixedDeltaTime);
            Input(); float beforeCut = body.linearVelocity.y;
            controller.SendMessage("FixedUpdate");
            Check(Mathf.Abs(body.linearVelocity.y - beforeCut * jumpCut) < 0.001f, "Short jump changed");
            results.Add("C jump speed=" + jumpSpeed + ", release multiplier=" + jumpCut);

            foreach (bool airborne in new[] { false, true })
            {
                ResetToGround();
                if (airborne)
                {
                    body.position += Vector2.up * 3;
                    body.linearVelocity = Vector2.zero;
                    physics.Simulate(Time.fixedDeltaTime);
                    Input(); Tick();
                }
                float gravity = body.gravityScale;
                Input(Key.RightArrow, Key.X);
                float start = body.position.x;
                int steps = 0;
                Tick();
                Check(movement.IsDashing && body.gravityScale == 0, "Dash start failed");
                Check(animator.GetCurrentAnimatorStateInfo(1).IsName(airborne ? "Player_dash_air" : "Player_dash"),
                    "Dash animation failed");
                // 다음 물리 프레임부터 실제 입력 해제 상태를 반영합니다.
                Input();
                while (movement.IsDashing && steps++ < 100) Tick();
                float distance = body.position.x - start;
                Check(!movement.IsDashing && Mathf.Abs(distance - expectedDashDistance) < 0.02f, "Dash distance changed: " + distance);
                Check(body.gravityScale == gravity, "Dash gravity not restored");
                results.Add((airborne ? "Air" : "Ground") + " dash distance=" + distance + ", gravity restored");
                if (airborne)
                {
                    for (int i = 0; i < 25; i++) controller.SendMessage("FixedUpdate");
                    Input(Key.X); controller.SendMessage("FixedUpdate");
                    Check(!movement.IsDashing, "Repeated air dash allowed");
                    results.Add("Repeated air dash blocked");
                }
            }
            return results.ToArray();
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            if (originalKeyboard != null) originalKeyboard.MakeCurrent();
            UnityEngine.Object.Destroy(root);
            _ = UnitySceneManager.UnloadSceneAsync(scene);
        }
    }
}
