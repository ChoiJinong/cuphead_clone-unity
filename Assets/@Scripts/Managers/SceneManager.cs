using UnityEngine;

public class SceneManager : Singleton<SceneManager>
{
    private BaseScene _curScene;
    public BaseScene CurrentScene
    {
        get
        {
            if(_curScene == null )
                _curScene = FindFirstObjectByType<BaseScene>();
            return _curScene;
        }
    }
    public Define.EScene CurrentSceneType
    {
        get
        {
            if (_curScene == null)
                return Define.EScene.Unknown;
            return CurrentScene.SceneType;
        }
    }
    public void LoadScene(Define.EScene sceneType)
    {
        string sceneName = sceneType.ToString();
        UnityEngine.SceneManagement.SceneManager.LoadScene( sceneName );
        _curScene = null;
    }
}
