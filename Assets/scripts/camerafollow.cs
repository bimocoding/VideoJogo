using UnityEngine;
using UnityEngine.Tilemaps;

public class camara : MonoBehaviour
{
    [Header("Objetivo")]
    public Transform target;

    [Header("Suavizado")]
    [Range(0.01f, 1f)]
    public float smoothTime = 0.15f;   // tiempo de suavizado horizontal (menor = mas rapido)
    [Range(0.01f, 1f)]
    public float verticalSmoothTime = 0.25f; // la vertical va mas suave para no marear al saltar
    public Vector2 offset = Vector2.zero; // desplazamiento respecto al jugador

    [Header("Zona muerta (dead zone)")]
    public bool useDeadZone = true;
    public float deadZoneX = 0.5f;     // ventana alrededor del foco: el foco solo se arrastra cuando el jugador sale de ella
    public float deadZoneY = 0.3f;

    [Header("Mirar hacia adelante (look-ahead)")]
    public float lookAheadDistance = 1.2f;   // cuanto se adelanta la camara en la direccion de movimiento
    public float lookAheadSmoothTime = 0.5f; // lento para que no de tirones al girar
    public float lookAheadMinSpeed = 0.5f;   // velocidad minima para considerar que se esta moviendo

    [Header("Seguimiento vertical por plataforma")]
    public bool platformSnapping = true;     // en el aire mantiene la altura del ultimo suelo salvo que el jugador se aleje mucho
    public float airFollowMargin = 1.6f;     // distancia vertical en el aire antes de empezar a seguir
    public float fallLookDown = 0.8f;        // al caer rapido, la camara baja un poco para ver donde aterriza
    public float fastFallSpeed = -6f;

    [Header("Limites opcionales")]
    public bool useBounds = false;
    public float minX, maxX, minY, maxY;

    [Header("Bordes del mapa (no ver fuera del nivel)")]
    public bool clampToMapEdges = true;      // usa los bordes del Tilemap para que el borde de la pantalla nunca salga del mapa
    public Tilemap mapTilemap;               // si esta vacio, busca el objeto "Tilemap" dentro de "Grid"
    public float mapEdgePadding = 0f;        // margen extra hacia dentro del mapa

    private Camera cam;
    private Rect mapRect;
    private bool hasMapRect;

    [Header("Cámara fija (arena del jefe)")]
    public float lockTransitionTime = 0.8f;
    private bool isLocked;
    private Vector2 lockCenter;
    private float lockSize;
    private float defaultSize = -1f;
    private float sizeVelocity;

    // Fija la cámara mostrando un rectángulo completo del mundo (p. ej. toda la arena del jefe)
    public static void LockToArea(Rect area)
    {
        if (Instance == null || Instance.cam == null) return;
        var c = Instance.cam;
        Instance.isLocked = true;
        Instance.lockCenter = area.center;
        Instance.lockSize = Mathf.Max(area.height * 0.5f, area.width * 0.5f / c.aspect);
    }

    public static void Unlock()
    {
        if (Instance != null) Instance.isLocked = false;
    }

    [Header("Temblor (camera shake)")]
    public float shakeFrequency = 25f;       // velocidad del temblor (ruido Perlin)

    public static camara Instance { get; private set; }

    private float shakeIntensity;
    private float shakeDuration;
    private float shakeTimer;
    private Vector3 basePos;                 // posicion sin temblor, para que el suavizado no se contamine

    private Rigidbody2D targetRb;
    private NewMonoBehaviourScript targetPlayer;
    private Vector2 focus;
    private float groundedY;
    private float lookAhead;
    private float lookAheadVelocity;
    private float velocityX;
    private float velocityY;

    void Awake()
    {
        Instance = this;
        basePos = transform.position;
    }

    // Llamar desde cualquier script: camara.Shake(0.15f, 0.25f)
    public static void Shake(float intensity, float duration)
    {
        if (Instance == null) return;
        // Un temblor mas fuerte reemplaza al actual; uno mas debil no lo corta
        if (intensity >= Instance.CurrentShakeIntensity())
        {
            Instance.shakeIntensity = intensity;
            Instance.shakeDuration = duration;
            Instance.shakeTimer = duration;
        }
    }

    private float CurrentShakeIntensity()
    {
        return shakeTimer > 0f ? shakeIntensity * (shakeTimer / shakeDuration) : 0f;
    }

    void Start()
    {
        cam = GetComponent<Camera>();
        CacheMapRect();
        CacheTarget();
        if (target != null)
        {
            focus = target.position;
            groundedY = target.position.y;
        }
    }

    private void CacheTarget()
    {
        if (target == null) return;
        targetRb = target.GetComponent<Rigidbody2D>();
        targetPlayer = target.GetComponent<NewMonoBehaviourScript>();
    }

    void LateUpdate()
    {
        if (target == null) return;
        if (targetRb == null && targetPlayer == null) CacheTarget();

        Vector2 pos = target.position;
        Vector2 vel = targetRb != null ? targetRb.linearVelocity : Vector2.zero;
        bool grounded = targetPlayer != null ? targetPlayer.IsGrounded : Mathf.Abs(vel.y) < 0.01f;

        // Foco con ventana: se arrastra suavemente en vez de saltar al salir de la zona muerta
        if (useDeadZone)
        {
            focus.x = DragInsideWindow(focus.x, pos.x, deadZoneX);
            focus.y = DragInsideWindow(focus.y, pos.y, deadZoneY);
        }
        else
        {
            focus = pos;
        }

        // Altura objetivo: en el suelo sigue al jugador; en el aire mantiene la del ultimo suelo
        if (!platformSnapping || grounded)
        {
            groundedY = focus.y;
        }
        else
        {
            float diff = pos.y - groundedY;
            if (diff > airFollowMargin) groundedY = pos.y - airFollowMargin;
            else if (diff < 0f) groundedY = pos.y; // al caer por debajo del suelo previo, seguir sin retraso
        }

        float targetY = groundedY;
        if (vel.y < fastFallSpeed) targetY -= fallLookDown;

        // Look-ahead en la direccion de movimiento
        float targetLook = 0f;
        if (Mathf.Abs(vel.x) > lookAheadMinSpeed) targetLook = Mathf.Sign(vel.x) * lookAheadDistance;
        lookAhead = Mathf.SmoothDamp(lookAhead, targetLook, ref lookAheadVelocity, lookAheadSmoothTime);

        float goalX = focus.x + lookAhead + offset.x;
        float goalY = targetY + offset.y;

        // Zoom suave entre el tamaño normal y el de la cámara fija
        if (cam != null && cam.orthographic)
        {
            if (defaultSize < 0f) defaultSize = cam.orthographicSize;
            float targetSize = isLocked ? lockSize : defaultSize;
            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetSize, ref sizeVelocity, lockTransitionTime);
        }

        float followTime = smoothTime, followTimeY = verticalSmoothTime;
        if (isLocked)
        {
            // Cámara estática: todo el escenario a la vista para moverse con precisión
            goalX = lockCenter.x;
            goalY = lockCenter.y;
            followTime = followTimeY = lockTransitionTime;
        }
        else
        {
            if (useBounds)
            {
                if (!(clampToMapEdges && hasMapRect)) goalX = Mathf.Clamp(goalX, minX, maxX);
                goalY = Mathf.Clamp(goalY, minY, maxY);
            }
            if (clampToMapEdges && hasMapRect) ClampToMap(ref goalX, ref goalY);
        }

        float x = Mathf.SmoothDamp(basePos.x, goalX, ref velocityX, followTime);
        float y = Mathf.SmoothDamp(basePos.y, goalY, ref velocityY, followTimeY);
        basePos = new Vector3(x, y, transform.position.z);

        // Temblor que se desvanece; usa tiempo real para seguir durante pausas de impacto
        Vector3 shake = Vector3.zero;
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.unscaledDeltaTime;
            float strength = CurrentShakeIntensity();
            float t = Time.unscaledTime * shakeFrequency;
            shake.x = (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f * strength;
            shake.y = (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f * strength;
        }

        transform.position = basePos + shake;
    }

    private void CacheMapRect()
    {
        if (mapTilemap == null)
        {
            var grid = GameObject.Find("Grid");
            var t = grid != null ? grid.transform.Find("Tilemap") : null;
            if (t != null) mapTilemap = t.GetComponent<Tilemap>();
        }
        if (mapTilemap == null) return;

        mapTilemap.CompressBounds();
        BoundsInt cells = mapTilemap.cellBounds;
        Vector3 min = mapTilemap.CellToWorld(cells.min);
        Vector3 max = mapTilemap.CellToWorld(cells.max);
        mapRect = Rect.MinMaxRect(min.x + mapEdgePadding, min.y + mapEdgePadding, max.x - mapEdgePadding, max.y - mapEdgePadding);
        hasMapRect = true;
    }

    // Limita el centro de la camara para que sus bordes (no su centro) queden dentro del mapa
    private void ClampToMap(ref float x, ref float y)
    {
        if (cam == null || !cam.orthographic) return;
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        x = (mapRect.width > halfW * 2f) ? Mathf.Clamp(x, mapRect.xMin + halfW, mapRect.xMax - halfW) : mapRect.center.x;
        // Solo el suelo del mapa limita en vertical: arriba es cielo y debe verse al subir
        y = Mathf.Max(y, mapRect.yMin + halfH);
    }

    private static float DragInsideWindow(float current, float targetValue, float halfSize)
    {
        float diff = targetValue - current;
        if (diff > halfSize) return targetValue - halfSize;
        if (diff < -halfSize) return targetValue + halfSize;
        return current;
    }
}
