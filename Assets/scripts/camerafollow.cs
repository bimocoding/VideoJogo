using UnityEngine;

public class camara : MonoBehaviour
{
    [Header("Objetivo")]
    public Transform target;

    [Header("Suavizado")]
    [Range(0.01f, 1f)]
    public float smoothTime = 0.15f;   // tiempo de suavizado (menor = mas rapido)
    public Vector2 offset = Vector2.zero; // desplazamiento respecto al jugador

    [Header("Zona muerta (dead zone)")]
    public bool useDeadZone = true;
    public float deadZoneX = 0.5f;     // la camara no se mueve si el jugador esta dentro de este rango
    public float deadZoneY = 0.3f;

    [Header("Limites opcionales")]
    public bool useBounds = false;
    public float minX, maxX, minY, maxY;

    private Vector3 velocity = Vector3.zero;
    private Vector3 desiredPos;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 targetPos = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z
        );

        // Dead zone: solo mueve la camara si el jugador sale de la zona central
        if (useDeadZone)
        {
            float dx = targetPos.x - transform.position.x;
            float dy = targetPos.y - transform.position.y;

            if (Mathf.Abs(dx) < deadZoneX) targetPos.x = transform.position.x;
            if (Mathf.Abs(dy) < deadZoneY) targetPos.y = transform.position.y;
        }

        // Suavizado
        desiredPos = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, smoothTime);

        // Limites de camara
        if (useBounds)
        {
            desiredPos.x = Mathf.Clamp(desiredPos.x, minX, maxX);
            desiredPos.y = Mathf.Clamp(desiredPos.y, minY, maxY);
        }

        transform.position = desiredPos;
    }
}
 