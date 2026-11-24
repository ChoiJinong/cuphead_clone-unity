using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

// 이 씬의 전투 흐름만 관리하며 씬을 다시 로드하면 함께 초기화됩니다.
public class GameManager : MonoBehaviour
{
    public enum GameState { Ready, Playing, Win, Defeat }

    [Header("전투 대상")]
    [SerializeField] private PlayerHealth player;
    [SerializeField] private BossBase boss;
    [SerializeField, Tooltip("페이즈 전환을 추가할 때 끄고, 마지막 페이즈 종료 시 Win()을 호출합니다.")]
    private bool winOnBossDeath = true;
    [SerializeField] private BulletPool[] playerBulletPools;
    [SerializeField] private Boss_Bullet_Pool[] bossBulletPools;
    [Header("기존 연출 UI")]
    [SerializeField] private Animator readyAnimator;
    [SerializeField] private AnimationClip readyAnimation;
    [SerializeField] private Animator winAnimator;
    [SerializeField] private AnimationClip winAnimation;
    [SerializeField] private Animator defeatAnimator;
    [SerializeField] private AnimationClip defeatAnimation;
    [SerializeField] private UnityEngine.UI.Button retryButton;
    [Header("현재 진행 상태")]
    [SerializeField] private GameState state = GameState.Ready;

    private PlayerController playerController;
    private PlayerParry playerParry;
    private Rigidbody2D playerBody;
    private float presentationRemaining;
    private bool restarting;

    public GameState CurrentState => state;
    public bool CanRetry => !restarting && presentationRemaining <= 0f
        && (state == GameState.Win || state == GameState.Defeat);

    private void Awake()
    {
        state = GameState.Ready;
        playerController = player.GetComponent<PlayerController>();
        playerParry = player.GetComponent<PlayerParry>();
        playerBody = player.GetComponent<Rigidbody2D>();
        player.SetDamageEnabled(false);
        boss.SetDamageEnabled(false);
    }

    private void OnEnable()
    {
        player.Died += Defeat;
        boss.Died += OnBossDied;
        retryButton.onClick.AddListener(Retry);
    }

    private void Start()
    {
        SetCombatEnabled(false);
        HidePresentations();
        retryButton.gameObject.SetActive(false);
        presentationRemaining = PlayPresentation(readyAnimator, readyAnimation);
    }

    private void Update() => Tick(Time.deltaTime);

    private void Tick(float deltaTime)
    {
        if (state == GameState.Playing || restarting) return;
        presentationRemaining = Mathf.Max(0f, presentationRemaining - deltaTime);
        if (presentationRemaining > 0f) return;
        if (state == GameState.Ready)
        {
            readyAnimator.gameObject.SetActive(false);
            state = GameState.Playing;
            SetCombatEnabled(true);
        }
        else if (!retryButton.gameObject.activeSelf)
        {
            retryButton.gameObject.SetActive(true);
            retryButton.interactable = true;
        }
    }

    private void OnBossDied()
    {
        if (winOnBossDeath) Win();
    }

    public void Win() => EndGame(GameState.Win);
    public void Defeat() => EndGame(GameState.Defeat);

    private void EndGame(GameState result)
    {
        // 먼저 판정을 확정해 같은 프레임의 다른 사망 이벤트를 무시합니다.
        if (state != GameState.Playing) return;
        state = result;
        SetCombatEnabled(false);
        HidePresentations();
        retryButton.gameObject.SetActive(false);
        presentationRemaining = result == GameState.Win
            ? PlayPresentation(winAnimator, winAnimation)
            : PlayPresentation(defeatAnimator, defeatAnimation);
    }

    private void SetCombatEnabled(bool value)
    {
        player.SetDamageEnabled(value);
        boss.SetDamageEnabled(value);
        playerController.enabled = value && !player.IsDead;
        if (playerParry != null) playerParry.enabled = value && !player.IsDead;
        if (!value) playerBody.linearVelocity = Vector2.zero;
        boss.enabled = value && !boss.IsDead;
        // 비활성화 시 풀의 기존 정리 로직이 모든 대여 투사체·이펙트를 회수합니다.
        foreach (var pool in playerBulletPools)
            if (pool != null) pool.enabled = value;
        foreach (var pool in bossBulletPools)
            if (pool != null) pool.enabled = value;
    }

    private void HidePresentations()
    {
        readyAnimator.gameObject.SetActive(false);
        winAnimator.gameObject.SetActive(false);
        defeatAnimator.gameObject.SetActive(false);
    }

    private static float PlayPresentation(Animator animator, AnimationClip clip)
    {
        animator.gameObject.SetActive(true);
        animator.enabled = true;
        animator.speed = 1f;
        animator.Rebind();
        animator.Update(0f);
        animator.Play(0, 0, 0f);
        animator.Update(0f);
        return clip.length;
    }

    public void Retry()
    {
        if (!CanRetry) return;
        restarting = true;
        retryButton.interactable = false;
        UnitySceneManager.LoadSceneAsync(gameObject.scene.path);
    }

    private void OnDisable()
    {
        player.Died -= Defeat;
        boss.Died -= OnBossDied;
        retryButton.onClick.RemoveListener(Retry);
    }
}
