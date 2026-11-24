using UnityEngine;

public class PlayerHUD : MonoBehaviour
{
    [Header("플레이어 데이터")]
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerCardGauge cardGauge;
    [Header("HP: 0은 사망, 1부터 해당 체력의 이미지")]
    [SerializeField] private UnityEngine.UI.Image healthImage;
    [SerializeField] private Sprite[] healthSprites = System.Array.Empty<Sprite>();
    [Header("카드: 왼쪽부터 순서대로 연결")]
    [SerializeField] private UnityEngine.UI.Image[] cardImages = System.Array.Empty<UnityEngine.UI.Image>();
    [SerializeField] private Sprite chargedCardSprite;
    [SerializeField] private AnimationClip cardFlipAnimation;

    private static readonly int CardState = Animator.StringToHash("Base Layer.Card");
    private Animator[] cardAnimators;
    private float[] flipRemaining = System.Array.Empty<float>();
    private int completedCards;

    private void Awake()
    {
        cardAnimators = new Animator[cardImages.Length];
        flipRemaining = new float[cardImages.Length];
        for (int i = 0; i < cardImages.Length; i++)
            if (cardImages[i] != null) cardAnimators[i] = cardImages[i].GetComponent<Animator>();
    }

    private void OnEnable()
    {
        if (health != null) health.HealthChanged += UpdateHealth;
        if (cardGauge != null) cardGauge.GaugeChanged += UpdateCards;
        Refresh();
    }

    // 다른 오브젝트의 Awake에서 초기화된 체력·게이지를 다시 읽습니다.
    private void Start() => Refresh();

    private void Refresh()
    {
        if (health != null) UpdateHealth(health.CurrentHealth);
        if (cardGauge != null) ShowCards(cardGauge.CurrentCards, false);
    }

    private void UpdateHealth(int currentHealth)
    {
        if (healthImage == null || healthSprites.Length == 0) return;
        healthImage.sprite = healthSprites[Mathf.Clamp(currentHealth, 0, healthSprites.Length - 1)];
    }

    private void UpdateCards(float value) => ShowCards(value, true);

    private void ShowCards(float value, bool animate)
    {
        int fullCards = Mathf.Clamp(Mathf.FloorToInt(value + .00001f), 0, cardImages.Length);
        bool decreased = fullCards < completedCards;
        for (int i = 0; i < cardImages.Length; i++)
        {
            var image = cardImages[i];
            if (image == null) continue;
            float fill = i < fullCards ? 1f : Mathf.Clamp01(value - i);
            image.enabled = fill > .00001f;
            if (animate && i >= completedCards && i < fullCards && cardAnimators[i] != null && cardFlipAnimation != null)
            {
                image.type = UnityEngine.UI.Image.Type.Simple;
                flipRemaining[i] = cardFlipAnimation.length;
                cardAnimators[i].enabled = true;
                cardAnimators[i].Play(CardState, 0, 0f);
                cardAnimators[i].Update(0f);
            }
            else if (!animate || decreased || flipRemaining[i] <= 0f)
            {
                StopFlip(i);
                image.type = UnityEngine.UI.Image.Type.Filled;
                image.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical;
                image.fillOrigin = (int)UnityEngine.UI.Image.OriginVertical.Bottom;
                image.fillAmount = fill;
            }
        }
        completedCards = fullCards;
    }

    private void Update() => Tick(Time.deltaTime);

    private void Tick(float deltaTime)
    {
        for (int i = 0; i < flipRemaining.Length; i++)
        {
            if (flipRemaining[i] <= 0f) continue;
            flipRemaining[i] = Mathf.Max(0f, flipRemaining[i] - deltaTime);
            if (flipRemaining[i] <= 0f) StopFlip(i);
        }
    }

    private void StopFlip(int index)
    {
        flipRemaining[index] = 0f;
        if (cardAnimators[index] != null) cardAnimators[index].enabled = false;
        if (cardImages[index] != null) cardImages[index].sprite = chargedCardSprite;
    }

    private void OnDisable()
    {
        if (health != null) health.HealthChanged -= UpdateHealth;
        if (cardGauge != null) cardGauge.GaugeChanged -= UpdateCards;
        for (int i = 0; i < flipRemaining.Length; i++) StopFlip(i);
    }
}
