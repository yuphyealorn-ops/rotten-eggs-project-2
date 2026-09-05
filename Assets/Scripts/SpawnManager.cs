using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Decides when a chicken lays an egg and what kind it is.
///
/// The gap between eggs shrinks as the difficulty tier climbs, which together
/// with the falling speed is the GDD's "difficulty slowly increases".
/// </summary>
public class SpawnManager : MonoBehaviour
{
    [Header("What to spawn")]
    public GameObject eggPrefab;

    [Header("Gap between eggs, in seconds")]
    public float startingInterval = 1.55f;
    public float intervalPerTier = 0.13f;
    public float shortestInterval = 0.86f;
    [Tooltip("Randomly stretches or squeezes each gap so the rhythm is not exact.")]
    public float intervalJitter = 0.12f;

    [Header("Power up eggs")]
    [Tooltip("No power ups at all until the round is this many seconds old.")]
    public float firstPowerUpAfter = 10f;
    [Tooltip("Shortest gap between two power ups.")]
    public float powerUpCooldown = 12f;
    [Range(0f, 1f)]
    [Tooltip("Chance a given egg is a power up, once the timings above allow it.")]
    public float powerUpChance = 0.12f;

    [Header("Duo timings")]
    public float duoStartingInterval = 1.42f;
    public float duoIntervalPerTier = 0.11f;
    public float duoShortestInterval = 0.82f;
    [Tooltip("Duo is more generous with power ups, because they are how you attack.")]
    public float duoFirstPowerUpAfter = 8f;
    public float duoPowerUpCooldown = 7f;
    [Range(0f, 1f)]
    public float duoPowerUpChance = 0.22f;

    readonly float[] timers = new float[2];
    float lastPowerUpTime = -999f;

    void Update()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null || manager.phase != GamePhase.Playing)
        {
            return;
        }

        int activePlayers = manager.mode == GameMode.Single ? 1 : 2;
        for (int i = 0; i < activePlayers; i++)
        {
            timers[i] -= Time.deltaTime;
            if (timers[i] <= 0f)
            {
                SpawnFor(i + 1);
                timers[i] = NextInterval(manager);
            }
        }
    }

    float NextInterval(GameManager manager)
    {
        bool duo = manager.mode == GameMode.Duo;
        float start = duo ? duoStartingInterval : startingInterval;
        float perTier = duo ? duoIntervalPerTier : intervalPerTier;
        float shortest = duo ? duoShortestInterval : shortestInterval;

        float interval = Mathf.Max(shortest, start - manager.CurrentTier() * perTier);
        return interval * Random.Range(1f - intervalJitter, 1f + intervalJitter);
    }

    /// <summary>Picks one of that player's living chickens and has it lay an egg.</summary>
    void SpawnFor(int targetPlayer)
    {
        GameManager manager = GameManager.Instance;
        if (manager == null || eggPrefab == null)
        {
            return;
        }

        ChickenController chicken = PickChicken(targetPlayer);
        if (chicken == null)
        {
            return;
        }

        chicken.PlayLay();

        BasketController owner = targetPlayer == 2 ? manager.playerTwo : manager.playerOne;
        Vector3 where = chicken.EggSpawnPoint();
        where.x += Random.Range(-0.5f, 0.5f);

        GameObject egg = Instantiate(eggPrefab, where, Quaternion.identity);
        EggController controller = egg.GetComponent<EggController>();
        if (controller != null)
        {
            controller.owner = owner;
            controller.SetType(RollType(manager));
        }
    }

    ChickenController PickChicken(int targetPlayer)
    {
        ChickenController[] all = FindObjectsByType<ChickenController>(FindObjectsSortMode.None);
        List<ChickenController> candidates = new List<ChickenController>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].IsAlive() && all[i].targetPlayer == targetPlayer)
            {
                candidates.Add(all[i]);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    /// <summary>
    /// Rolls the egg kind. Single player only ever sees the speed egg; the
    /// freeze, reverse and golden eggs are Duo only, as the GDD says.
    /// </summary>
    EggType RollType(GameManager manager)
    {
        bool duo = manager.mode == GameMode.Duo;
        float readyAt = duo ? duoFirstPowerUpAfter : firstPowerUpAfter;
        float cooldown = duo ? duoPowerUpCooldown : powerUpCooldown;
        float chance = duo ? duoPowerUpChance : powerUpChance;

        if (manager.elapsed < readyAt
            || manager.elapsed - lastPowerUpTime < cooldown
            || Random.value >= chance)
        {
            return EggType.Normal;
        }

        lastPowerUpTime = manager.elapsed;

        if (!duo)
        {
            return EggType.Speed;
        }

        float roll = Random.value;
        if (roll < 0.40f)
        {
            return EggType.Speed;
        }

        if (roll < 0.65f)
        {
            return EggType.Freeze;
        }

        if (roll < 0.87f)
        {
            return EggType.Reverse;
        }

        return EggType.Golden;
    }

    public void ResetForRound(GameMode mode)
    {
        lastPowerUpTime = -999f;
        if (mode == GameMode.Single)
        {
            timers[0] = 0.85f;
            timers[1] = float.MaxValue;
        }
        else
        {
            timers[0] = 0.72f;
            timers[1] = 1.08f;
        }
    }
}
