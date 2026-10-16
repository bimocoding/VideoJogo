using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    public Transform cameraTransform;

    [Header("Parallax Factors (0 = se mueve con la cámara, 1 = estático en el mundo)")]
    [Range(0f, 1f)]
    public float parallaxEffectX = 0.5f;   // 0.5 da un efecto de profundidad montañosa natural
    [Range(0f, 1f)]
    public float parallaxEffectY = 0.35f;  // Se mueve suavemente al escalar plataformas

    private Vector3 startPosition;
    private Vector3 startCameraPosition;

    void Start()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        startPosition = transform.position;
        if (cameraTransform != null)
            startCameraPosition = cameraTransform.position;
    }

    void LateUpdate()
    {
        if (cameraTransform == null) return;

        // Cálculo exacto y libre de acumulación de errores de flotante
        Vector3 cameraDelta = cameraTransform.position - startCameraPosition;
        transform.position = new Vector3(
            startPosition.x + cameraDelta.x * parallaxEffectX,
            startPosition.y + cameraDelta.y * parallaxEffectY,
            startPosition.z
        );
    }
}
