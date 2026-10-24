using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class ChestGoal : MonoBehaviour
{
    [Header("Animación de Apertura")]
    public float frameRate = 0.08f;
    public Sprite[] openFrames;

    private SpriteRenderer sr;
    private BoxCollider2D col;
    private bool hasTriggered = false;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1.2f, 1.2f);
        col.offset = new Vector2(0f, 0.4f);

        LoadFramesIfEmpty();
    }

    private void LoadFramesIfEmpty()
    {
        if (openFrames == null || openFrames.Length == 0)
        {
#if UNITY_EDITOR
            var all = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Pixel Art Platformer - Village Props v2.3.1/Textures/TX Chest Animation.png");
            List<Sprite> list = new List<Sprite>();
            for (int i = 0; i <= 6; i++)
            {
                foreach (var a in all)
                {
                    if (a is Sprite s && s.name == $"TX Chest Animation_{i}")
                    {
                        list.Add(s);
                        break;
                    }
                }
            }
            openFrames = list.ToArray();
#endif
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered) return;
        if (BossAlive()) return; // el cofre no se abre hasta derrotar al jefe

        // Verificar si es el personaje
        if (other.CompareTag("Player") || other.name == "prota" || other.GetComponent<NewMonoBehaviourScript>() != null)
        {
            hasTriggered = true;
            StartCoroutine(OpenAndCelebrate(other.gameObject));
        }
    }

    private static bool BossAlive()
    {
        var boss = FindAnyObjectByType<AngelBossAI>();
        return boss != null && boss.enabled; // al morir, HitReceiver desactiva la IA y luego destruye al jefe
    }

    // Si el jugador ya está encima del cofre cuando muere el jefe, abrirlo igualmente
    private void OnTriggerStay2D(Collider2D other)
    {
        OnTriggerEnter2D(other);
    }

    void Update()
    {
        // Atenuado mientras está bloqueado para que se entienda que aún no se puede abrir
        if (sr != null && !hasTriggered) sr.color = BossAlive() ? new Color(0.55f, 0.55f, 0.6f, 1f) : Color.white;
    }

    private IEnumerator OpenAndCelebrate(GameObject player)
    {
        // 1. Congelar movimiento del jugador para la celebración
        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        var playerScript = player.GetComponent<NewMonoBehaviourScript>();
        if (playerScript != null)
        {
            playerScript.enabled = false;
        }

        // 2. Animar apertura del cofre
        if (openFrames != null && openFrames.Length > 0)
        {
            for (int i = 0; i < openFrames.Length; i++)
            {
                if (sr != null && openFrames[i] != null)
                {
                    sr.sprite = openFrames[i];
                }
                yield return new WaitForSeconds(frameRate);
            }
        }

        yield return new WaitForSeconds(0.2f);

        // 3. Disparar pantalla de GANASTE y posterior reinicio
        if (VictoryUI.Instance != null)
        {
            VictoryUI.Instance.TriggerVictory();
        }
    }
}
