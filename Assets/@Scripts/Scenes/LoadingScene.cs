using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingScene : BaseScene
{
    protected override void Awake()
    {
        base.Awake();

        SceneType = Define.EScene.Scene_Loading;

        //·Îµù Todo
        SceneManager.Instance.LoadScene(Define.EScene.Scene_BotanicPanic);
    }
}
