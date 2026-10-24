using UnityEngine;

/// <summary>
/// Proyectil de fuego de los magos y del jefe. Daña al jugador y se rompe contra el suelo.
/// No se puede destruir golpeándolo: hay que esquivarlo.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Fireball : MonoBehaviour
{
    public float speed = 3.5f;
    public int damage = 12;
    public float lifetime = 4f;
    public LayerMask groundLayer;

    private Vector2 direction = Vector2.left;
    private Rigidbody2D rb;
    private float life;
    private bool spent;

    public void Launch(Vector2 dir, float spd, int dmg)
    {
        direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.left;
        speed = spd;
        damage = dmg;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + 180f); // el sprite mira a la izquierda
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        if (groundLayer == 0) groundLayer = LayerMask.GetMask("Floor");
    }

    void FixedUpdate()
    {
        if (spent) return;
        rb.MovePosition(rb.position + direction * speed * Time.fixedDeltaTime);
        life += Time.fixedDeltaTime;
        if (life >= lifetime) Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (spent) return;

        var player = other.GetComponentInParent<NewMonoBehaviourScript>();
        if (player != null)
        {
            player.TakeDamage(damage, false, transform);
            Burst();
            return;
        }

        if (((1 << other.gameObject.layer) & groundLayer) != 0 && !other.isTrigger) Burst();
    }

    private void Burst()
    {
        spent = true;
        Destroy(gameObject);
    }
}
