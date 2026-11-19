using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

public class SceneManager : Singleton<SceneManager>
{
    private BaseScene currentScene;

    public BaseScene CurrentScene
    {
        get
        {
            if (currentScene == null)
                currentScene = FindFirstObjectByType<BaseScene>();
            return currentScene;
        }
    }
    public Define.EScene CurrentSceneType
    {
        get
        {
            BaseScene scene = CurrentScene;
            return scene != null ? scene.SceneType : Define.EScene.Unknown;
        }
    }
    public void LoadScene(Define.EScene sceneType)
    {
        string sceneName = sceneType.ToString();
        UnitySceneManager.LoadScene(sceneName);
        currentScene = null;
    }
}
