using UnityEngine;

public class HitEffect : MonoBehaviour
{
    [Header("Configuración del Efecto")]
    public float lifetime = 0.25f;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
