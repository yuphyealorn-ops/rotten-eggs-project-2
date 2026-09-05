using UnityEngine;

/// <summary>
/// One player's basket: moving it, holding the eggs it has caught, throwing
/// them back, and tracking the power up effects the GDD gives the opponent.
///
/// A caught egg is added to <see cref="ammo"/>; pressing the throw key spends
/// one and sends it back up at the chickens. Only the single player mode lets
/// you throw, because in Duo the GDD says players cannot attack each other.
/// </summary>
public class BasketController : MonoBehaviour
{
    [Header("Who this is")]
    [Tooltip("1 for player one, 2 for player two.")]
    public int playerNumber = 1;

    [Header("Controls")]
    public KeyCode moveLeftKey = KeyCode.A;
    public KeyCode moveRightKey = KeyCode.D;
    public KeyCode alternateLeftKey = KeyCode.LeftArrow;
    public KeyCode alternateRightKey = KeyCode.RightArrow;
    public KeyCode throwKey = KeyCode.Space;

    [Header("Movement, in world units per second")]
    public float moveSpeed = 9.7f;
    [Tooltip("GDD: the speed egg gives a temporary speed boost.")]
    public float boostedMoveSpeed = 14.7f;

    [Header("Lane limits, in world units")]
    public float minX = -15f;
    public float maxX = 15f;
    [Tooltip("Where this basket is put back at the start of every round.")]
    public float homeX;

    [Header("Lives - GDD: the player starts with 3 lives")]
    [Tooltip("Lives are counted in halves because a missed egg costs half a life.")]
    public int startingHalfLives = 6;

    [Header("Throwing")]
    public GameObject projectilePrefab;
    public float throwCooldown = 0.22f;

    [Header("Power up durations, in seconds")]
    public float speedDuration = 5f;
    public float freezeDuration = 2f;
    public float reverseDuration = 3f;
    public float sabotageDuration = 5f;

    [Header("Live values, shown here so you can watch them while playing")]
    public int halfLives = 6;
    public int ammo;
    public int score;
    public int combo;

    [HideInInspector] public float speedTimer;
    [HideInInspector] public float freezeTimer;
    [HideInInspector] public float reverseTimer;
    [HideInInspector] public float sabotageTimer;

    float throwTimer;

    void Update()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null || manager.phase != GamePhase.Playing)
        {
            return;
        }

        CountDownTimers();
        Move();

        if (manager.mode == GameMode.Single && Input.GetKeyDown(throwKey))
        {
            Throw();
        }
    }

    void CountDownTimers()
    {
        speedTimer = Mathf.Max(0f, speedTimer - Time.deltaTime);
        freezeTimer = Mathf.Max(0f, freezeTimer - Time.deltaTime);
        reverseTimer = Mathf.Max(0f, reverseTimer - Time.deltaTime);
        sabotageTimer = Mathf.Max(0f, sabotageTimer - Time.deltaTime);
        throwTimer = Mathf.Max(0f, throwTimer - Time.deltaTime);
    }

    void Move()
    {
        float direction = 0f;
        if (Input.GetKey(moveLeftKey) || Input.GetKey(alternateLeftKey))
        {
            direction -= 1f;
        }

        if (Input.GetKey(moveRightKey) || Input.GetKey(alternateRightKey))
        {
            direction += 1f;
        }

        // GDD: a freeze egg stops the opponent, a reverse egg flips their keys.
        if (freezeTimer > 0f)
        {
            direction = 0f;
        }
        else if (reverseTimer > 0f)
        {
            direction = -direction;
        }

        float speed = speedTimer > 0f ? boostedMoveSpeed : moveSpeed;
        float x = transform.position.x + direction * speed * Time.deltaTime;
        x = Mathf.Clamp(x, minX, maxX);
        transform.position = new Vector3(x, transform.position.y, transform.position.z);
    }

    /// <summary>Spends one stacked egg and sends it back up at the chickens.</summary>
    void Throw()
    {
        if (ammo <= 0 || throwTimer > 0f || projectilePrefab == null)
        {
            return;
        }

        ammo--;
        throwTimer = throwCooldown;
        Instantiate(projectilePrefab, transform.position + Vector3.up * 0.8f, Quaternion.identity);
    }

    /// <summary>Called by an egg that landed in this basket.</summary>
    public void CatchEgg(EggType type)
    {
        GameManager manager = GameManager.Instance;
        BasketController opponent = Opponent();

        switch (type)
        {
            case EggType.Normal:
                // GDD: eggs can be stacked and thrown back. Only single player throws.
                if (manager != null && manager.mode == GameMode.Single)
                {
                    ammo++;
                }

                AddScore(10);
                break;

            case EggType.Speed:
                speedTimer = speedDuration;
                AddScore(25);
                break;

            case EggType.Freeze:
                if (opponent != null)
                {
                    opponent.freezeTimer = freezeDuration;
                }

                AddScore(30);
                break;

            case EggType.Reverse:
                if (opponent != null)
                {
                    opponent.reverseTimer = reverseDuration;
                }

                AddScore(30);
                break;

            case EggType.Golden:
                // GDD: golden eggs speed up the opponent's incoming eggs.
                if (opponent != null)
                {
                    opponent.sabotageTimer = sabotageDuration;
                    opponent.combo = 0;
                }

                AddScore(50);
                break;
        }
    }

    /// <summary>Called by an egg that hit the ground on this player's side.</summary>
    public void MissEgg(EggType type)
    {
        // GDD: missing a power up is safe, only a normal egg costs you.
        if (type != EggType.Normal)
        {
            return;
        }

        combo = 0;
        halfLives--;
        if (halfLives <= 0)
        {
            halfLives = 0;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayerOut(this);
            }
        }
    }

    /// <summary>Catching things in a row multiplies what they are worth.</summary>
    public int AddScore(int basePoints)
    {
        combo++;
        int points = basePoints * ScoreMultiplier();
        score += points;
        return points;
    }

    public int ScoreMultiplier()
    {
        if (combo >= 10)
        {
            return 3;
        }

        if (combo >= 5)
        {
            return 2;
        }

        return 1;
    }

    public void BreakCombo()
    {
        combo = 0;
    }

    /// <summary>Whole hearts left, for the HUD. Two halves make one heart.</summary>
    public int WholeHearts()
    {
        return halfLives / 2;
    }

    public bool HasHalfHeart()
    {
        return halfLives % 2 == 1;
    }

    public BasketController Opponent()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null)
        {
            return null;
        }

        return this == manager.playerOne ? manager.playerTwo : manager.playerOne;
    }

    public void ResetForRound(GameMode mode)
    {
        halfLives = startingHalfLives;
        ammo = 0;
        score = 0;
        combo = 0;
        speedTimer = 0f;
        freezeTimer = 0f;
        reverseTimer = 0f;
        sabotageTimer = 0f;
        throwTimer = 0f;
        transform.position = new Vector3(homeX, transform.position.y, transform.position.z);
    }
}
