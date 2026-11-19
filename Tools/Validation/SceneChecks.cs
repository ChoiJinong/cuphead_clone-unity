// Play 모드에서 script run --entry SceneChecks.Run으로 실행합니다.
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

public static class SceneChecks
{
    public static async Task<string[]> Run()
    {
        if (!Application.isPlaying)
            throw new Exception("Enter Play mode.");

        var sceneObject = new GameObject("SceneTypeCheck");
        var managerObject = new GameObject("SceneManagerCheck");
        try
        {
            var scene = sceneObject.AddComponent<BaseScene>();
            typeof(BaseScene).GetProperty(nameof(BaseScene.SceneType)).SetValue(scene, Define.EScene.Scene_BotanicPanic);
            var manager = managerObject.AddComponent<SceneManager>();
            if (manager.CurrentSceneType != Define.EScene.Scene_BotanicPanic || manager.CurrentScene != scene)
                throw new Exception("First CurrentSceneType lookup failed.");
        }
        finally
        {
            UnityEngine.Object.Destroy(sceneObject);
            UnityEngine.Object.Destroy(managerObject);
        }

        await Task.Delay(50);
        UnitySceneManager.LoadScene(Define.EScene.Scene_Loading.ToString());
        for (int i = 0; i < 100; i++)
        {
            await Task.Delay(50);
            if (UnitySceneManager.GetActiveScene().name != Define.EScene.Scene_BotanicPanic.ToString())
                continue;
            if (UnityEngine.Object.FindFirstObjectByType<PlayerController>() == null)
                throw new Exception("Loaded game scene has no player.");
            return new[] { "First current scene/type lookup OK", "Loading scene -> BotanicPanic bootstrap OK" };
        }
        throw new Exception("Loading scene did not open BotanicPanic.");
    }
}
