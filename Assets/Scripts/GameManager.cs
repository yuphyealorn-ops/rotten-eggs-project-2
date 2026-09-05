using UnityEngine;

/// <summary>
/// Owns the match: which mode is running, how long it has been going, how fast
/// things should be falling by now, and who has won. Every other script asks
/// this one for those answers.
///
/// The tuning numbers come straight from the GDD and are public so you can
/// change them in the Inspector without touching code.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Match")]
    public GameMode mode = GameMode.Single;
    public GamePhase phase = GamePhase.Menu;

    [Header("Difficulty - GDD: difficulty slowly increases by making objects fall faster")]
    [Tooltip("Seconds of play before the difficulty tier goes up by one.")]
    public float secondsPerTier = 20f;
    [Tooltip("Difficulty stops climbing once it reaches this tier.")]
    public int maxTier = 5;

    [Header("Falling speed, in world units per second")]
    public float baseFallSpeed = 3.5f;
    public float fallSpeedPerTier = 0.53f;
    [Tooltip("GDD: a golden egg speeds up the opponent's eggs for 5 seconds.")]
    public float sabotageFallMultiplier = 1.65f;

    [Header("Single player - GDD: defeat 3 chickens")]
    public int chickensToDefeat = 3;

    [Header("Scene references")]
    public BasketController playerOne;
    public BasketController playerTwo;
    public SpawnManager spawnManager;
    public HUDController hud;
    public GameObject singleModeRoot;
    public GameObject duoModeRoot;

    /// <summary>Seconds since the round began. This is what drives the difficulty tier.</summary>
    [HideInInspector] public float elapsed;

    /// <summary>How many chickens the single player has finished off.</summary>
    [HideInInspector] public int chickensDefeated;

    /// <summary>0 means a draw, 1 means player one, 2 means player two.</summary>
    [HideInInspector] public int winnerPlayer;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        ShowMenu();
    }

    void Update()
    {
        if (phase == GamePhase.Playing)
        {
            elapsed += Time.deltaTime;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ShowMenu();
        }

        if (Input.GetKeyDown(KeyCode.R) && phase != GamePhase.Menu)
        {
            StartRound(mode);
        }

        if (phase == GamePhase.Menu)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                StartRound(GameMode.Single);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                StartRound(GameMode.Duo);
            }
        }
    }

    /// <summary>Difficulty step the match has reached, from 0 up to maxTier.</summary>
    public int CurrentTier()
    {
        return Mathf.Min(maxTier, (int)(elapsed / secondsPerTier));
    }

    /// <summary>How fast an egg belonging to <paramref name="owner"/> should fall right now.</summary>
    public float FallSpeedFor(BasketController owner)
    {
        float speed = baseFallSpeed + CurrentTier() * fallSpeedPerTier;
        if (owner != null && owner.sabotageTimer > 0f)
        {
            speed *= sabotageFallMultiplier;
        }

        return speed;
    }

    public void ShowMenu()
    {
        phase = GamePhase.Menu;
        elapsed = 0f;
        chickensDefeated = 0;
        winnerPlayer = 0;
        ClearSpawnedObjects();

        if (singleModeRoot != null)
        {
            singleModeRoot.SetActive(false);
        }

        if (duoModeRoot != null)
        {
            duoModeRoot.SetActive(false);
        }

        if (playerTwo != null)
        {
            playerTwo.gameObject.SetActive(false);
        }
    }

    public void StartRound(GameMode selectedMode)
    {
        mode = selectedMode;
        phase = GamePhase.Playing;
        elapsed = 0f;
        chickensDefeated = 0;
        winnerPlayer = 0;

        ClearSpawnedObjects();

        if (singleModeRoot != null)
        {
            singleModeRoot.SetActive(mode == GameMode.Single);
        }

        if (duoModeRoot != null)
        {
            duoModeRoot.SetActive(mode == GameMode.Duo);
        }

        if (playerOne != null)
        {
            playerOne.ResetForRound(mode);
        }

        if (playerTwo != null)
        {
            playerTwo.gameObject.SetActive(mode == GameMode.Duo);
            playerTwo.ResetForRound(mode);
        }

        ChickenController[] chickens = FindObjectsByType<ChickenController>(FindObjectsSortMode.None);
        for (int i = 0; i < chickens.Length; i++)
        {
            chickens[i].ResetForRound();
        }

        if (spawnManager != null)
        {
            spawnManager.ResetForRound(mode);
        }
    }

    /// <summary>Called by a chicken when its last hit point is gone.</summary>
    public void OnChickenDefeated()
    {
        chickensDefeated++;
        if (mode == GameMode.Single && chickensDefeated >= chickensToDefeat)
        {
            phase = GamePhase.Won;
            winnerPlayer = 1;
            ClearSpawnedObjects();
        }
    }

    /// <summary>Called by a basket the moment its last half heart is spent.</summary>
    public void OnPlayerOut(BasketController loser)
    {
        if (phase != GamePhase.Playing)
        {
            return;
        }

        if (mode == GameMode.Single)
        {
            phase = GamePhase.Lost;
            winnerPlayer = 0;
        }
        else
        {
            // GDD: the goal in Duo is simply to make the other person lose.
            phase = GamePhase.Won;
            winnerPlayer = loser == playerOne ? 2 : 1;
        }

        ClearSpawnedObjects();
    }

    /// <summary>Removes every egg and thrown egg still in the air.</summary>
    void ClearSpawnedObjects()
    {
        EggController[] eggs = FindObjectsByType<EggController>(FindObjectsSortMode.None);
        for (int i = 0; i < eggs.Length; i++)
        {
            Destroy(eggs[i].gameObject);
        }

        ProjectileController[] shots = FindObjectsByType<ProjectileController>(FindObjectsSortMode.None);
        for (int i = 0; i < shots.Length; i++)
        {
            Destroy(shots[i].gameObject);
        }
    }
}
