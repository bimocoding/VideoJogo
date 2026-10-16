using System.Collections;
using UnityEngine;
using TMPro;

public class ComboUI : MonoBehaviour
{
    public static ComboUI Instance { get; private set; }

    [Header("Referencias UI")]
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI comboCountText;
    public TextMeshProUGUI comboTitleText;
    public RectTransform container;

    [Header("Tiempos y Animación")]
    public float comboTimeout = 1.35f;
    public float punchScale = 1.35f;
    public float scaleSpeed = 10f;

    private int currentCombo = 0;
    private float timeoutTimer = 0f;
    private Vector3 targetScale = Vector3.one;

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

    void OnEnable()
    {
        NewMonoBehaviourScript.OnComboHit += RegisterHit;
        NewMonoBehaviourScript.OnComboReset += ResetCombo;
    }

    void OnDisable()
    {
        NewMonoBehaviourScript.OnComboHit -= RegisterHit;
        NewMonoBehaviourScript.OnComboReset -= ResetCombo;
    }

    void Update()
    {
        if (timeoutTimer > 0f)
        {
            timeoutTimer -= Time.deltaTime;

            if (timeoutTimer <= 0f)
            {
                ResetCombo();
            }
            else if (timeoutTimer < 0.4f && canvasGroup != null)
            {
                canvasGroup.alpha = timeoutTimer / 0.4f;
            }
        }

        if (container != null)
        {
            container.localScale = Vector3.Lerp(container.localScale, targetScale, Time.deltaTime * scaleSpeed);
        }
    }

    public void RegisterHit(int count)
    {
        currentCombo = count;
        timeoutTimer = comboTimeout;

        // Mostrar contador a partir del segundo golpe: COMBO x2, COMBO x3...
        if (currentCombo >= 2)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            if (comboTitleText != null)
            {
                comboTitleText.text = "COMBO";
            }

            if (comboCountText != null)
            {
                comboCountText.text = $"x{currentCombo}";
            }

            if (container != null)
            {
                container.localScale = Vector3.one * punchScale;
            }
        }
    }

    public void ResetCombo()
    {
        currentCombo = 0;
        timeoutTimer = 0f;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }
}
