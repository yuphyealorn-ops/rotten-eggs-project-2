using UnityEngine;

namespace RottenEggs
{
    /// <summary>
    /// Makes the project playable as soon as its scene is opened and Play is
    /// pressed.  It leaves a manually placed RottenEggsGame alone, so scenes
    /// can still opt into an explicit host object when they need one.
    /// </summary>
    public static class RottenEggsBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateGameIfNeeded()
        {
            if (Object.FindAnyObjectByType<RottenEggsGame>() != null)
            {
                return;
            }

            GameObject host = new GameObject("Rotten Eggs");
            host.AddComponent<RottenEggsGame>();
        }
    }
}
