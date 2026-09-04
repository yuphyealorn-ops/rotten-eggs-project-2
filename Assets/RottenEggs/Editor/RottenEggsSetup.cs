using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RottenEggs.EditorTools
{
    /// <summary>
    /// One-click scene setup. The game builds its own canvas, sprites and audio
    /// at runtime, so all a scene needs is a single object carrying the driver.
    /// </summary>
    public static class RottenEggsSetup
    {
        [MenuItem("Rotten Eggs/Add Game To Scene")]
        public static void AddGameToScene()
        {
            RottenEggsGame existing = Object.FindAnyObjectByType<RottenEggsGame>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("Rotten Eggs is already in this scene.", existing.gameObject);
                return;
            }

            GameObject host = new GameObject("Rotten Eggs");
            host.AddComponent<RottenEggsGame>();
            Undo.RegisterCreatedObjectUndo(host, "Add Rotten Eggs");
            Selection.activeGameObject = host;
            EditorSceneManager.MarkSceneDirty(host.scene);
            Debug.Log("Added Rotten Eggs to the scene. Press Play to run it.", host);
        }

        [MenuItem("Rotten Eggs/Verify Ported Rules And Assets")]
        public static void VerifyPort()
        {
            Debug.Log(GameModelSelfTest.Run());
            Debug.Log(AudioManager.VerifyBundledAssets());
            Debug.Log(ChickenSprites.VerifyBundledAssets());
        }
    }
}
