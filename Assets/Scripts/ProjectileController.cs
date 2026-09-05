using UnityEngine;

/// <summary>
/// An egg the player has thrown back up at the chickens. GDD: each chicken
/// takes 4 of these before it goes down.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class ProjectileController : MonoBehaviour
{
    [Header("Flight")]
    public float speed = 14.7f;
    [Tooltip("Once it passes this height the throw has missed.")]
    public float topY = 10f;

    void Awake()
    {
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.useFullKinematicContacts = true;

        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void Update()
    {
        transform.position += Vector3.up * speed * Time.deltaTime;

        if (transform.position.y > topY)
        {
            // A thrown egg that hits nothing breaks the combo.
            GameManager manager = GameManager.Instance;
            if (manager != null && manager.playerOne != null)
            {
                manager.playerOne.BreakCombo();
            }

            Destroy(gameObject);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        ChickenController chicken = other.GetComponent<ChickenController>();
        if (chicken == null || !chicken.IsAlive())
        {
            return;
        }

        chicken.TakeHit();
        Destroy(gameObject);
    }
}
