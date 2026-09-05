using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the whole playable scene from scratch: cuts the chicken sheets into
/// sprites, makes the animation clips and the controller, saves the prefabs,
/// then lays out and wires the scene.
///
/// It is safe to run again at any time - everything it makes, it overwrites.
/// Once it has run you can forget it exists and edit the scene by hand.
/// </summary>
public static class EggCatcherBuilder
{
    const string SpritesFolder = "Assets/Sprites";
    const string ChickenFolder = "Assets/Sprites/Chicken";
    const string AnimationFolder = "Assets/Animation";
    const string PrefabFolder = "Assets/Prefabs";
    const string ScenesFolder = "Assets/Scenes";
    const string ScenePath = "Assets/Scenes/RottenEggs.unity";
    const string SheetFolder = "Assets/StreamingAssets/RottenEggs/Sprites";

    /// <summary>One world unit is 15 of the original game's pixels.</summary>
    const float PixelsPerUnit = 15f;

    /// <summary>The chicken artwork was always drawn at double size.</summary>
    const float ChickenPixelsPerUnit = 7.5f;

    const int FrameWidth = 20;
    const int FrameHeight = 21;

    const float GroundTopY = -6.9f;
    const float BasketY = -6.1f;
    const float EggDeathY = -7.0f;

    struct ClipDef
    {
        public string Name;
        public string Sheet;
        public int Frames;
        public bool Loop;

        public ClipDef(string name, string sheet, int frames, bool loop)
        {
            Name = name;
            Sheet = sheet;
            Frames = frames;
            Loop = loop;
        }
    }

    static readonly ClipDef[] Clips =
    {
        new ClipDef("Idle", "chicken-idle", 5, true),
        new ClipDef("Walking", "chicken-walking", 4, true),
        new ClipDef("Jumping", "chicken-jumping", 6, false),
        new ClipDef("Damage", "chicken-damage", 6, false),
        new ClipDef("Die", "chicken-die", 4, false)
    };

    [MenuItem("Rotten Eggs/Build Unity Scene", false, 0)]
    public static void Build()
    {
        EnsureFolders();

        Dictionary<string, Sprite[]> chickenFrames = SliceChickenSheets();
        Sprite pixel = MakePixelSprite();
        Sprite eggSprite = MakeEggSprite();
        Sprite basketSprite = MakeBasketSprite();

        AnimatorController controller = BuildAnimator(chickenFrames);
        Material spriteMaterial = MakeSpriteMaterial();

        GameObject eggPrefab = BuildEggPrefab(eggSprite, spriteMaterial);
        GameObject shotPrefab = BuildProjectilePrefab(eggSprite, spriteMaterial);
        GameObject chickenPrefab = BuildChickenPrefab(chickenFrames["Idle"][0], controller, spriteMaterial);

        BuildScene(pixel, basketSprite, spriteMaterial, eggPrefab, shotPrefab, chickenPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Rotten Eggs: built " + ScenePath + ". Open it and press Play.");
    }

    static void EnsureFolders()
    {
        string[] folders = { SpritesFolder, ChickenFolder, AnimationFolder, PrefabFolder, ScenesFolder };
        foreach (string folder in folders)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            }
        }
    }

    // ---------------------------------------------------------------- sprites

    /// <summary>
    /// Cuts each 20 x 21 frame out of the bundled strips and saves it as its own
    /// sprite, so an artist can replace a single frame without touching a sheet.
    /// </summary>
    static Dictionary<string, Sprite[]> SliceChickenSheets()
    {
        Dictionary<string, Sprite[]> result = new Dictionary<string, Sprite[]>();

        foreach (ClipDef clip in Clips)
        {
            string sourcePath = SheetFolder + "/" + clip.Sheet + ".png";
            if (!File.Exists(sourcePath))
            {
                Debug.LogError("Rotten Eggs: missing chicken sheet " + sourcePath);
                result[clip.Name] = new Sprite[0];
                continue;
            }

            Texture2D sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            sheet.LoadImage(File.ReadAllBytes(sourcePath));

            List<string> written = new List<string>();
            for (int i = 0; i < clip.Frames; i++)
            {
                Texture2D frame = new Texture2D(FrameWidth, FrameHeight, TextureFormat.RGBA32, false);
                frame.SetPixels(sheet.GetPixels(i * FrameWidth, 0, FrameWidth, FrameHeight));
                frame.Apply();

                string outPath = ChickenFolder + "/" + clip.Name + "_" + i + ".png";
                File.WriteAllBytes(outPath, frame.EncodeToPNG());
                written.Add(outPath);
                Object.DestroyImmediate(frame);
            }

            Object.DestroyImmediate(sheet);
            AssetDatabase.Refresh();

            Sprite[] sprites = new Sprite[written.Count];
            for (int i = 0; i < written.Count; i++)
            {
                ApplySpriteImport(written[i], ChickenPixelsPerUnit);
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(written[i]);
            }

            result[clip.Name] = sprites;
        }

        return result;
    }

    static void ApplySpriteImport(string path, float pixelsPerUnit)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    static Sprite WriteSprite(string path, Texture2D texture, float pixelsPerUnit)
    {
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.Refresh();
        ApplySpriteImport(path, pixelsPerUnit);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>A single white pixel, stretched and tinted for every flat shape.</summary>
    static Sprite MakePixelSprite()
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return WriteSprite(SpritesFolder + "/pixel.png", texture, 1f);
    }

    /// <summary>
    /// A 7 x 9 egg, left white so the EggController can tint it per kind.
    /// Replace this file with real art and every egg picks it up.
    /// </summary>
    static Sprite MakeEggSprite()
    {
        const int w = 7;
        const int h = 9;
        Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f - w / 2f) / (w / 2f);
                float dy = (y + 0.5f - h / 2f) / (h / 2f);

                // Slightly narrower at the top, the way an egg actually sits.
                float squeeze = dy > 0f ? 1f + dy * 0.25f : 1f;
                bool inside = (dx * squeeze) * (dx * squeeze) + dy * dy <= 1f;

                texture.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        return WriteSprite(SpritesFolder + "/egg.png", texture, PixelsPerUnit);
    }

    /// <summary>A 48 x 15 basket outline, also white so it can be tinted.</summary>
    static Sprite MakeBasketSprite()
    {
        const int w = 48;
        const int h = 15;
        Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);

        for (int y = 0; y < h; y++)
        {
            // The basket tapers in towards its base.
            int inset = Mathf.RoundToInt((1f - y / (float)(h - 1)) * 5f);
            for (int x = 0; x < w; x++)
            {
                bool rim = y >= h - 3;
                bool body = x >= inset && x < w - inset;
                texture.SetPixel(x, y, rim || body ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        return WriteSprite(SpritesFolder + "/basket.png", texture, PixelsPerUnit);
    }

    /// <summary>
    /// URP's 2D renderer needs its own sprite shader. Unlit keeps the art at the
    /// exact colours it was drawn in, with no scene lighting to set up.
    /// </summary>
    static Material MakeSpriteMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
        {
            Debug.LogWarning("Rotten Eggs: URP sprite shader not found, using the default sprite material.");
            return null;
        }

        string path = SpritesFolder + "/SpriteUnlit.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
        }

        return material;
    }

    // -------------------------------------------------------------- animation

    static AnimatorController BuildAnimator(Dictionary<string, Sprite[]> frames)
    {
        string controllerPath = AnimationFolder + "/Chicken.controller";
        AssetDatabase.DeleteAsset(controllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        foreach (ClipDef clip in Clips)
        {
            AnimationClip asset = BuildClip(clip, frames[clip.Name]);
            AnimatorState state = machine.AddState(clip.Name);
            state.motion = asset;

            // Every clip is chosen from code with animator.Play, so the graph
            // deliberately has no transitions to second-guess that choice.
            if (clip.Name == "Walking")
            {
                machine.defaultState = state;
            }
        }

        EditorUtility.SetDirty(controller);
        return controller;
    }

    static AnimationClip BuildClip(ClipDef def, Sprite[] frames)
    {
        string path = AnimationFolder + "/Chicken" + def.Name + ".anim";
        AssetDatabase.DeleteAsset(path);

        AnimationClip clip = new AnimationClip();
        clip.frameRate = 10f;

        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++)
        {
            keys[i] = new ObjectReferenceKeyframe();
            keys[i].time = i / 10f;
            keys[i].value = frames[i];
        }

        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = def.Loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    // ---------------------------------------------------------------- prefabs

    static SpriteRenderer AddSprite(GameObject host, Sprite sprite, Material material, int order)
    {
        SpriteRenderer renderer = host.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        if (material != null)
        {
            renderer.sharedMaterial = material;
        }

        return renderer;
    }

    static GameObject SavePrefab(GameObject instance, string name)
    {
        string path = PrefabFolder + "/" + name + ".prefab";
        GameObject asset = PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        return asset;
    }

    static GameObject BuildEggPrefab(Sprite sprite, Material material)
    {
        GameObject egg = new GameObject("Egg");
        AddSprite(egg, sprite, material, 5);

        Rigidbody2D body = egg.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.useFullKinematicContacts = true;

        BoxCollider2D collider = egg.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(7f / PixelsPerUnit, 9f / PixelsPerUnit);

        EggController controller = egg.AddComponent<EggController>();
        controller.groundY = EggDeathY;

        return SavePrefab(egg, "Egg");
    }

    static GameObject BuildProjectilePrefab(Sprite sprite, Material material)
    {
        GameObject shot = new GameObject("Thrown Egg");
        SpriteRenderer renderer = AddSprite(shot, sprite, material, 6);
        renderer.color = new Color32(255, 244, 216, 255);

        Rigidbody2D body = shot.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.useFullKinematicContacts = true;

        BoxCollider2D collider = shot.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(7f / PixelsPerUnit, 9f / PixelsPerUnit);

        shot.AddComponent<ProjectileController>();

        return SavePrefab(shot, "Thrown Egg");
    }

    static GameObject BuildChickenPrefab(Sprite firstFrame, AnimatorController controller, Material material)
    {
        GameObject chicken = new GameObject("Chicken");
        AddSprite(chicken, firstFrame, material, 4);

        Animator animator = chicken.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        BoxCollider2D collider = chicken.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(36f / PixelsPerUnit, 36f / PixelsPerUnit);

        chicken.AddComponent<ChickenController>();

        return SavePrefab(chicken, "Chicken");
    }

    // ------------------------------------------------------------------ scene

    static GameObject Flat(string name, Sprite pixel, Material material, Color color,
        float centerX, float centerY, float width, float height, int order, Transform parent)
    {
        GameObject block = new GameObject(name);
        SpriteRenderer renderer = AddSprite(block, pixel, material, order);
        renderer.color = color;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = new Vector2(width, height);
        block.transform.position = new Vector3(centerX, centerY, 0f);
        if (parent != null)
        {
            block.transform.SetParent(parent, true);
        }

        return block;
    }

    static void BuildScene(Sprite pixel, Sprite basketSprite, Material material,
        GameObject eggPrefab, GameObject shotPrefab, GameObject chickenPrefab)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera: 32 x 18 world units, which is the original 480 x 270 at 15 px per unit.
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 9f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(158, 216, 245, 255);
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);

        GameObject world = new GameObject("World");
        Flat("Sky", pixel, material, new Color32(158, 216, 245, 255), 0f, 1f, 32f, 16f, -10, world.transform);
        Flat("Ground", pixel, material, new Color32(106, 190, 78, 255), 0f, GroundTopY - 1.05f, 32f, 2.1f, 0, world.transform);
        Flat("Dirt", pixel, material, new Color32(84, 140, 60, 255), 0f, -8.6f, 32f, 0.8f, 1, world.transform);

        // Managers
        GameObject managerObject = new GameObject("Game Manager");
        GameManager manager = managerObject.AddComponent<GameManager>();
        SpawnManager spawner = managerObject.AddComponent<SpawnManager>();
        spawner.eggPrefab = eggPrefab;
        manager.spawnManager = spawner;

        // Baskets
        BasketController one = BuildBasket("Player 1 Basket", basketSprite, material, shotPrefab,
            1, -9.0f, -14.1f, 14.1f, new Color32(214, 158, 92, 255));
        BasketController two = BuildBasket("Player 2 Basket", basketSprite, material, null,
            2, 9.0f, 1.87f, 14.1f, new Color32(219, 122, 160, 255));

        one.moveLeftKey = KeyCode.A;
        one.moveRightKey = KeyCode.D;
        one.alternateLeftKey = KeyCode.LeftArrow;
        one.alternateRightKey = KeyCode.RightArrow;

        // Player two only ever uses the arrows, so the two players never share a key.
        two.moveLeftKey = KeyCode.LeftArrow;
        two.moveRightKey = KeyCode.RightArrow;
        two.alternateLeftKey = KeyCode.None;
        two.alternateRightKey = KeyCode.None;

        manager.playerOne = one;
        manager.playerTwo = two;

        // Chicken lanes, converted straight from the original 480 x 270 layout.
        GameObject singleRoot = new GameObject("Single Mode");
        AddChicken(chickenPrefab, singleRoot.transform, pixel, material, "Chicken 1", -9.47f, 4.4f, -12.8f, -6.67f, 0.86f, 1, 1);
        AddChicken(chickenPrefab, singleRoot.transform, pixel, material, "Chicken 2", 0f, 4.8f, -3.07f, 3.07f, 1.05f, -1, 1);
        AddChicken(chickenPrefab, singleRoot.transform, pixel, material, "Chicken 3", 9.47f, 4.4f, 6.67f, 12.8f, 0.94f, 1, 1);

        GameObject duoRoot = new GameObject("Duo Mode");
        Flat("Divider", pixel, material, new Color32(36, 49, 60, 120), 0f, 0f, 0.12f, 18f, 3, duoRoot.transform);
        AddChicken(chickenPrefab, duoRoot.transform, pixel, material, "P1 Chicken 1", -11.67f, 4.33f, -13.0f, -9.67f, 0.88f, 1, 1);
        AddChicken(chickenPrefab, duoRoot.transform, pixel, material, "P1 Chicken 2", -4.27f, 3.8f, -6.0f, -2.33f, 1.04f, -1, 1);
        AddChicken(chickenPrefab, duoRoot.transform, pixel, material, "P2 Chicken 1", 4.27f, 3.8f, 2.33f, 6.0f, 1.04f, 1, 2);
        AddChicken(chickenPrefab, duoRoot.transform, pixel, material, "P2 Chicken 2", 11.67f, 4.33f, 9.67f, 13.0f, 0.88f, -1, 2);

        manager.singleModeRoot = singleRoot;
        manager.duoModeRoot = duoRoot;

        manager.hud = BuildHud();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    static BasketController BuildBasket(string name, Sprite sprite, Material material, GameObject shotPrefab,
        int playerNumber, float x, float minX, float maxX, Color color)
    {
        GameObject basket = new GameObject(name);
        SpriteRenderer renderer = AddSprite(basket, sprite, material, 7);
        renderer.color = color;
        basket.transform.position = new Vector3(x, BasketY, 0f);

        BoxCollider2D collider = basket.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(48f / PixelsPerUnit, 15f / PixelsPerUnit);

        BasketController controller = basket.AddComponent<BasketController>();
        controller.playerNumber = playerNumber;
        controller.homeX = x;
        controller.minX = minX;
        controller.maxX = maxX;
        controller.projectilePrefab = shotPrefab;

        return controller;
    }

    static void AddChicken(GameObject prefab, Transform parent, Sprite pixel, Material material, string name,
        float x, float y, float minX, float maxX, float speedScale, int direction, int targetPlayer)
    {
        GameObject chicken = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        chicken.name = name;
        chicken.transform.position = new Vector3(x, y, 0f);
        chicken.transform.SetParent(parent, true);

        ChickenController controller = chicken.GetComponent<ChickenController>();
        controller.minX = minX;
        controller.maxX = maxX;
        controller.speedScale = speedScale;
        controller.startingDirection = direction;
        controller.targetPlayer = targetPlayer;

        // A perch under the whole lane, so no chicken is left standing on air.
        float margin = 26f / PixelsPerUnit;
        float left = minX - margin;
        float right = maxX + margin;
        Flat(name + " Perch", pixel, material, new Color32(140, 100, 62, 255),
            (left + right) / 2f, y - 1.4f - 0.235f, right - left, 0.47f, 2, parent);
    }

    static HUDController BuildHud()
    {
        GameObject canvasObject = new GameObject("HUD Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960f, 540f);
        canvasObject.AddComponent<GraphicRaycaster>();

        HUDController hud = canvasObject.AddComponent<HUDController>();
        hud.playerOneText = MakeText(canvasObject.transform, "P1 Readout",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -18f), new Vector2(460f, 70f),
            TextAnchor.UpperLeft, 20);
        hud.playerTwoText = MakeText(canvasObject.transform, "P2 Readout",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -18f), new Vector2(460f, 70f),
            TextAnchor.UpperRight, 20);
        hud.difficultyText = MakeText(canvasObject.transform, "Difficulty",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(460f, 30f),
            TextAnchor.UpperCenter, 18);
        hud.messageText = MakeText(canvasObject.transform, "Message",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(820f, 300f),
            TextAnchor.MiddleCenter, 26);

        return hud;
    }

    static Text MakeText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offset, Vector2 size, TextAnchor alignment, int fontSize)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(anchorMin.x, anchorMax.y);
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;

        Text text = textObject.AddComponent<Text>();
        text.font = BuiltinFont();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color32(28, 40, 52, 255);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = false;
        text.text = "";

        return text;
    }

    static Font BuiltinFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return font;
    }

    // ------------------------------------------------------------ validation

    /// <summary>
    /// Opens the built scene and checks that everything it needs is actually
    /// hooked up. Run this after changing things by hand to catch a reference
    /// you cleared by accident, before you find it mid-game.
    /// </summary>
    [MenuItem("Rotten Eggs/Check Scene", false, 1)]
    public static void CheckScene()
    {
        List<string> problems = new List<string>();

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("Rotten Eggs: could not open " + ScenePath);
            return;
        }

        GameManager manager = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            Debug.LogError("Rotten Eggs: no GameManager in the scene.");
            return;
        }

        Require(manager.playerOne != null, "GameManager.playerOne is empty", problems);
        Require(manager.playerTwo != null, "GameManager.playerTwo is empty", problems);
        Require(manager.spawnManager != null, "GameManager.spawnManager is empty", problems);
        Require(manager.hud != null, "GameManager.hud is empty", problems);
        Require(manager.singleModeRoot != null, "GameManager.singleModeRoot is empty", problems);
        Require(manager.duoModeRoot != null, "GameManager.duoModeRoot is empty", problems);

        if (manager.spawnManager != null)
        {
            Require(manager.spawnManager.eggPrefab != null, "SpawnManager.eggPrefab is empty", problems);
        }

        if (manager.playerOne != null)
        {
            Require(manager.playerOne.projectilePrefab != null,
                "Player 1 has no projectilePrefab, so throwing will do nothing", problems);
            Require(manager.playerOne.startingHalfLives == 6,
                "Player 1 should start with 6 half lives, which is the GDD's 3 lives", problems);
        }

        if (manager.hud != null)
        {
            Require(manager.hud.playerOneText != null, "HUD is missing the player one readout", problems);
            Require(manager.hud.messageText != null, "HUD is missing the middle message", problems);
        }

        ChickenController[] chickens = Object.FindObjectsByType<ChickenController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        Require(chickens.Length == 7, "expected 7 chickens (3 single + 4 duo), found " + chickens.Length, problems);

        int singleChickens = 0;
        foreach (ChickenController chicken in chickens)
        {
            Require(chicken.maxHitPoints == 4,
                chicken.name + " should take 4 eggs, per the GDD", problems);
            Require(chicken.minX < chicken.maxX,
                chicken.name + " has an inside out patrol range", problems);

            Animator animator = chicken.GetComponent<Animator>();
            Require(animator != null && animator.runtimeAnimatorController != null,
                chicken.name + " has no animator controller", problems);

            if (chicken.transform.parent != null && chicken.transform.parent.name == "Single Mode")
            {
                singleChickens++;
            }
        }

        Require(singleChickens == manager.chickensToDefeat,
            "single player should have " + manager.chickensToDefeat + " chickens, found " + singleChickens, problems);

        if (problems.Count == 0)
        {
            Debug.Log("Rotten Eggs: scene check passed. " + chickens.Length + " chickens, all references wired.");
        }
        else
        {
            foreach (string problem in problems)
            {
                Debug.LogError("Rotten Eggs: " + problem);
            }

            Debug.LogError("Rotten Eggs: scene check found " + problems.Count + " problem(s).");
        }
    }

    static void Require(bool condition, string message, List<string> problems)
    {
        if (!condition)
        {
            problems.Add(message);
        }
    }
}
