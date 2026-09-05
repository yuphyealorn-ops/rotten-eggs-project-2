using UnityEngine;

/// <summary>
/// One egg on its way down. It falls at whatever speed the GameManager says the
/// match has reached, lands in a basket through a trigger, or cracks on the
/// ground and costs its owner half a life.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class EggController : MonoBehaviour
{
    [Header("What kind of egg this is")]
    public EggType type = EggType.Normal;

    [Header("Where the ground is, in world units")]
    public float groundY = -7f;

    /// <summary>The player who has to catch this one. Set by the SpawnManager.</summary>
    [HideInInspector] public BasketController owner;

    SpriteRenderer spriteRenderer;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        // A kinematic body with full contacts still raises trigger events against
        // the basket's static collider, which is what lets a catch register.
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.useFullKinematicContacts = true;

        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void Start()
    {
        Tint();
    }

    void Update()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null || manager.phase != GamePhase.Playing)
        {
            return;
        }

        float speed = manager.FallSpeedFor(owner);
        transform.position += Vector3.down * speed * Time.deltaTime;

        if (transform.position.y < groundY)
        {
            if (owner != null)
            {
                owner.MissEgg(type);
            }

            Destroy(gameObject);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        BasketController basket = other.GetComponent<BasketController>();
        if (basket == null)
        {
            return;
        }

        // Only the player this egg was aimed at can catch it.
        if (owner != null && basket != owner)
        {
            return;
        }

        basket.CatchEgg(type);
        Destroy(gameObject);
    }

    /// <summary>Colours the egg by kind, using the palette the GDD art uses.</summary>
    void Tint()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.color = ColorFor(type);
    }

    public static Color ColorFor(EggType type)
    {
        switch (type)
        {
            case EggType.Speed:
                return new Color32(67, 198, 219, 255);
            case EggType.Freeze:
                return new Color32(158, 232, 255, 255);
            case EggType.Reverse:
                return new Color32(171, 113, 255, 255);
            case EggType.Golden:
                return new Color32(255, 222, 89, 255);
            default:
                return new Color32(255, 244, 216, 255);
        }
    }

    /// <summary>Sets the kind and recolours, for the SpawnManager to call right after Instantiate.</summary>
    public void SetType(EggType newType)
    {
        type = newType;
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        Tint();
    }
}
