public class LoadingScene : BaseScene
{
    protected override void Awake()
    {
        base.Awake();

        SceneType = Define.EScene.Scene_Loading;
        // 로딩 화면 초기화 후 게임 장면으로 이동합니다.
        SceneManager.Instance.LoadScene(Define.EScene.Scene_BotanicPanic);
    }
}
