using UnityEngine;

/// <summary>
/// A chicken patrolling its perch. It lays the eggs that fall at the player and
/// takes 4 thrown eggs before it goes down, which is the GDD rule.
///
/// The five clips on the sprite sheet are played by name rather than through
/// Animator transitions, so the clip that is up is always exactly the one this
/// script asked for.
/// </summary>
public class ChickenController : MonoBehaviour
{
    [Header("Health - GDD: each chicken requires 4 eggs hit")]
    public int maxHitPoints = 4;
    public int hitPoints = 4;

    [Header("Which player's side this chicken is on")]
    [Tooltip("1 means it drops eggs on player one, 2 means player two. Single player only uses 1.")]
    public int targetPlayer = 1;

    [Header("Patrol, in world units")]
    public float minX = -14f;
    public float maxX = -9f;
    [Tooltip("Multiplies this chicken's speed so they do not all march in step.")]
    public float speedScale = 1f;
    public float patrolSpeed = 1.2f;
    public float patrolSpeedPerTier = 0.2f;
    [Tooltip("Beat spent standing still after turning around at the end of the lane.")]
    public float turnPause = 0.35f;
    [Tooltip("Which way it sets off: 1 walks right, -1 walks left.")]
    public int startingDirection = 1;

    [Header("Clip lengths, in seconds")]
    public float layClipSeconds = 0.6f;
    public float damageClipSeconds = 0.6f;
    public float dieClipSeconds = 0.4f;

    [Header("Where an egg leaves the chicken")]
    public float eggDropOffsetY = -1.2f;

    Animator animator;
    SpriteRenderer spriteRenderer;
    Collider2D bodyCollider;

    int direction = 1;
    float standTimer;
    float oneShotTimer;
    float startX;
    string currentState = "";

    void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        bodyCollider = GetComponent<Collider2D>();
        startX = transform.position.x;
        direction = startingDirection >= 0 ? 1 : -1;
    }

    void Update()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null || manager.phase != GamePhase.Playing)
        {
            return;
        }

        if (!IsAlive())
        {
            return;
        }

        oneShotTimer = Mathf.Max(0f, oneShotTimer - Time.deltaTime);
        standTimer = Mathf.Max(0f, standTimer - Time.deltaTime);

        if (standTimer <= 0f)
        {
            Patrol(manager);
        }

        if (oneShotTimer <= 0f)
        {
            Play(standTimer > 0f ? "Idle" : "Walking");
        }
    }

    void Patrol(GameManager manager)
    {
        float speed = (patrolSpeed + manager.CurrentTier() * patrolSpeedPerTier) * speedScale;
        float x = transform.position.x + direction * speed * Time.deltaTime;

        if (x < minX)
        {
            x = minX;
            direction = 1;
            standTimer = turnPause;
        }
        else if (x > maxX)
        {
            x = maxX;
            direction = -1;
            standTimer = turnPause;
        }

        transform.position = new Vector3(x, transform.position.y, transform.position.z);
        FaceDirection();
    }

    void FaceDirection()
    {
        if (spriteRenderer != null)
        {
            // The artwork faces right, so walking left just mirrors it.
            spriteRenderer.flipX = direction < 0;
        }
    }

    /// <summary>Plays the laying clip. The SpawnManager calls this as it drops an egg.</summary>
    public void PlayLay()
    {
        if (!IsAlive())
        {
            return;
        }

        Play("Jumping", true);
        oneShotTimer = layClipSeconds;
        standTimer = Mathf.Max(standTimer, layClipSeconds);
    }

    /// <summary>Takes one thrown egg. Four of these finish a chicken off.</summary>
    public void TakeHit()
    {
        if (!IsAlive())
        {
            return;
        }

        hitPoints--;

        GameManager manager = GameManager.Instance;
        if (manager != null && manager.playerOne != null)
        {
            manager.playerOne.AddScore(50);
        }

        if (hitPoints <= 0)
        {
            hitPoints = 0;
            Play("Die", true);
            oneShotTimer = dieClipSeconds;

            if (bodyCollider != null)
            {
                bodyCollider.enabled = false;
            }

            if (manager != null)
            {
                if (manager.playerOne != null)
                {
                    manager.playerOne.score += 200;
                }

                manager.OnChickenDefeated();
            }
        }
        else
        {
            // The flash plays over the patrol so a hit never roots the chicken.
            Play("Damage", true);
            oneShotTimer = damageClipSeconds;
        }
    }

    public bool IsAlive()
    {
        return hitPoints > 0;
    }

    /// <summary>Where an egg should appear when this chicken lays one.</summary>
    public Vector3 EggSpawnPoint()
    {
        return transform.position + Vector3.up * eggDropOffsetY;
    }

    public void ResetForRound()
    {
        hitPoints = maxHitPoints;
        direction = startingDirection >= 0 ? 1 : -1;
        standTimer = 0f;
        oneShotTimer = 0f;
        currentState = "";
        transform.position = new Vector3(startX, transform.position.y, transform.position.z);

        if (bodyCollider != null)
        {
            bodyCollider.enabled = true;
        }

        FaceDirection();
        Play("Walking", true);
    }

    void Play(string stateName)
    {
        Play(stateName, false);
    }

    void Play(string stateName, bool restart)
    {
        if (animator == null)
        {
            return;
        }

        if (!restart && currentState == stateName)
        {
            return;
        }

        currentState = stateName;
        animator.Play(stateName, 0, 0f);
    }
}
