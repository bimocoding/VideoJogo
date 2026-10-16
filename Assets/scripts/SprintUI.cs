using UnityEngine;
using TMPro;

public class SprintUI : MonoBehaviour
{
    public static SprintUI Instance { get; private set; }

    [Header("Referencias UI")]
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI sprintText;
    public RectTransform container;

    [Header("Ajustes Visuales")]
    public float fadeSpeed = 14f;

    private NewMonoBehaviourScript playerRef;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }

    void Start()
    {
        FindPlayer();
    }

    private void FindPlayer()
    {
        if (playerRef == null)
        {
            playerRef = Object.FindAnyObjectByType<NewMonoBehaviourScript>();
        }
    }

    void Update()
    {
        if (playerRef == null)
        {
            FindPlayer();
            return;
        }

        bool shouldShow = playerRef.IsEffectiveRunning;
        float targetAlpha = shouldShow ? 1f : 0f;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
        }
    }
}
