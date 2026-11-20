using System.Collections;
using UnityEngine;

/// <summary>
/// Gestor global de música de fondo (Singleton, persiste entre escenas).
/// Reproduce la canción principal en loop con volúmenes distintos
/// según el estado del juego:
///   - Menú principal / Pausa / Game Over → menuVolume (0.40 = 40%)
///   - Gameplay activo                    → gameplayVolume (0.04 = 4%)
/// Las transiciones entre volúmenes se hacen con un fade suave.
/// </summary>
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Clip de Música")]
    [Tooltip("Arrastra aquí el AudioClip de la canción (CelestialResort_GoodKarmaMix)")]
    public AudioClip musicClip;

    [Header("Volúmenes")]
    [Range(0f, 1f)] public float menuVolume     = 0.40f;   // 40% en menú/pausa
    [Range(0f, 1f)] public float gameplayVolume  = 0.04f;   // 4% durante gameplay
    [Range(0f, 1f)] public float gameOverVolume  = 0.40f;   // 40% en game over

    [Header("Transición")]
    public float fadeDuration = 0.8f;   // Tiempo del fade entre volúmenes

    private AudioSource source;
    private Coroutine fadeRoutine;
    private float targetVolume;

    // ═══════════════════════════════════════════════════
    //  SINGLETON (DontDestroyOnLoad)
    // ═══════════════════════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Configurar AudioSource
        source = gameObject.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;    // 2D global
        source.priority = 0;        // Máxima prioridad
        source.volume = 0f;

        // Cargar clip por código si no está asignado en el Inspector
        if (musicClip == null)
        {
            musicClip = Resources.Load<AudioClip>("Music/CelestialResort_GoodKarmaMix");
        }
    }

    void Start()
    {
        // Arrancar la música al volumen de menú
        if (musicClip != null)
        {
            source.clip = musicClip;
            source.Play();
            FadeToVolume(menuVolume);
        }
        else
        {
            Debug.LogWarning("[MusicManager] No se encontró el AudioClip de música. " +
                "Asígnalo en el Inspector o colócalo en Assets/Resources/Music/CelestialResort_GoodKarmaMix");
        }
    }

    // ═══════════════════════════════════════════════════
    //  API PÚBLICA — llamada desde menús y gameplay
    // ═══════════════════════════════════════════════════

    /// <summary>Volumen alto (menú principal o pausa).</summary>
    public void SetMenuVolume()
    {
        FadeToVolume(menuVolume);
    }

    /// <summary>Volumen bajo (gameplay activo).</summary>
    public void SetGameplayVolume()
    {
        FadeToVolume(gameplayVolume);
    }

    /// <summary>Volumen de game over (sube como menú).</summary>
    public void SetGameOverVolume()
    {
        FadeToVolume(gameOverVolume);
    }

    /// <summary>Fade genérico a cualquier volumen.</summary>
    public void FadeToVolume(float target)
    {
        targetVolume = target;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(target));
    }

    /// <summary>Silencia inmediatamente (p.ej. boss music toma el control).</summary>
    public void MuteInstant()
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        source.volume = 0f;
        targetVolume = 0f;
    }

    /// <summary>Restaura al volumen de gameplay después de que BossMusic termina.</summary>
    public void RestoreAfterBoss()
    {
        FadeToVolume(gameplayVolume);
    }

    // ═══════════════════════════════════════════════════
    //  FADE COROUTINE (usa unscaledDeltaTime para funcionar en pausa)
    // ═══════════════════════════════════════════════════

    private IEnumerator FadeRoutine(float target)
    {
        float startVol = source.volume;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVol, target, elapsed / fadeDuration);
            yield return null;
        }

        source.volume = target;
        fadeRoutine = null;
    }
}
