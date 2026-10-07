using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CoinRush3DBuilder
{
    const string ScenePath = "Assets/Scenes/CoinRush3D.unity";

    static Material mGrass, mStone, mWood, mTiles, mTrunk, mLeaves, mRock, mGold, mEnemy, mSpike, mEyeWhite, mPupil,
        mEnemyGlow, mCyan, mMagenta, mPortalDisc, mPortalBase, mLampGlow, mPole, mWater, mParticle, mConfetti;
    static Mesh torusOuter, torusInner, torusFlat, cone;
    static ParticleSystem fxCoin, fxHit, fxVictory;
    static Material textOutline;
    static Sprite uiSprite;

    // ---------------- Entry points ----------------

    [MenuItem("Coin Rush 3D/1. Build Project Content")]
    public static void BuildAll()
    {
        try
        {
            if (TMP_Settings.defaultFontAsset == null) throw new Exception("TMP Essentials missing");
            foreach (string d in new[] { "Assets/Scenes", "Assets/Materials", "Assets/Meshes", "Assets/Prefabs", "Assets/Audio" })
                EnsureFolder(d);
            PlayerSettings.productName = "Coin Rush 3D";
            PlayerSettings.companyName = "College Project";
            CreateMeshes();
            CreateMaterials();
            CreateEffects();
            BuildScene();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[CoinRush3D] Project content built successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError("[CoinRush3D] Build content failed: " + e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else throw;
        }
    }

    [MenuItem("Coin Rush 3D/2. Build Windows")]
    public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/CoinRush3D.exe");

    [MenuItem("Coin Rush 3D/3. Build WebGL")]
    public static void BuildWebGL()
    {
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        Build(BuildTarget.WebGL, "Builds/WebGL");
    }

    static void Build(BuildTarget target, string relative)
    {
        string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, relative);
        Directory.CreateDirectory(target == BuildTarget.WebGL ? output : Path.GetDirectoryName(output));
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = output,
            target = target,
            options = BuildOptions.None
        });
        BuildSummary s = report.summary;
        Debug.Log($"[CoinRush3D] {target} build {s.result}: {s.totalSize / (1024f * 1024f):F1} MB, {s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalTime}");
        if (s.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
    }

    [MenuItem("Coin Rush 3D/4. Save Preview Screenshot")]
    public static void Screenshot()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Camera cam = Camera.main;
        string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Screenshots");
        Directory.CreateDirectory(dir);
        Capture(cam, Path.Combine(dir, "player_view.png"));

        cam.transform.SetPositionAndRotation(new Vector3(-22f, 18f, -26f), Quaternion.Euler(32f, 40f, 0f));
        Capture(cam, Path.Combine(dir, "overview.png"));
        EditorSceneManager.OpenScene(ScenePath);
        Debug.Log("[CoinRush3D] Screenshots saved to " + dir);
    }

    static void Capture(Camera cam, string path)
    {
        RenderTexture rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.aspect = 16f / 9f;
        for (int i = 0; i < 4; i++) cam.Render();
        RenderTexture.active = rt;
        Texture2D img = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        img.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        img.Apply();
        File.WriteAllBytes(path, img.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
    }

    // ---------------- Helpers ----------------

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }

    static T SaveAsset<T>(T asset, string path) where T : UnityEngine.Object
    {
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(asset, path);
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    static Texture2D SaveTexture(string name, int size, Func<int, int, Color> pixel, bool repeat = true)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, true);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                t.SetPixel(x, y, pixel(x, y));
        t.Apply();
        string path = $"Assets/Materials/{name}.png";
        File.WriteAllBytes(path, t.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        imp.alphaIsTransparency = true;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material Lit(string name, Color color, float smooth = 0.3f, float metal = 0f, Texture2D tex = null, Vector2? tiling = null, Color? emission = null)
    {
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", metal);
        if (tex)
        {
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
        }
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        return SaveAsset(m, $"Assets/Materials/{name}.mat");
    }

    static Material Transparent(string name, string shader, Color color, bool additive, Texture2D tex = null)
    {
        Material m = new Material(Shader.Find(shader)) { name = name };
        m.SetColor("_BaseColor", color);
        if (tex) m.SetTexture("_BaseMap", tex);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", additive ? 2f : 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", additive ? (int)BlendMode.One : (int)BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.SetFloat("_Cull", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        return SaveAsset(m, $"Assets/Materials/{name}.mat");
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat,
        bool collider = true, Vector3? euler = null)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.transform.localEulerAngles = euler ?? Vector3.zero;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static GameObject MeshObj(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 euler, Vector3 scale)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static Transform Group(string name, Transform parent = null)
    {
        Transform t = new GameObject(name).transform;
        if (parent) t.SetParent(parent, false);
        return t;
    }

    // ---------------- Meshes ----------------

    static Mesh Torus(string name, float radius, float tube, int segments, int sides)
    {
        List<Vector3> v = new List<Vector3>();
        List<Vector3> n = new List<Vector3>();
        List<int> tri = new List<int>();
        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            Vector3 center = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
            for (int j = 0; j <= sides; j++)
            {
                float b = j / (float)sides * Mathf.PI * 2f;
                Vector3 normal = new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(a) * Mathf.Cos(b), Mathf.Sin(b));
                v.Add(center + normal * tube);
                n.Add(normal);
            }
        }
        for (int i = 0; i < segments; i++)
            for (int j = 0; j < sides; j++)
            {
                int a0 = i * (sides + 1) + j, a1 = a0 + 1, b0 = a0 + sides + 1, b1 = b0 + 1;
                tri.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
            }
        Mesh m = new Mesh { name = name };
        m.SetVertices(v);
        m.SetNormals(n);
        m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        return SaveAsset(m, $"Assets/Meshes/{name}.asset");
    }

    static Mesh Cone(string name, int sides)
    {
        List<Vector3> v = new List<Vector3>();
        List<int> tri = new List<int>();
        for (int i = 0; i < sides; i++)
        {
            float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
            Vector3 p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f);
            Vector3 p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);
            int s = v.Count;
            v.AddRange(new[] { p0, Vector3.up, p1 });
            tri.AddRange(new[] { s, s + 1, s + 2 });
            s = v.Count;
            v.AddRange(new[] { p0, p1, Vector3.zero });
            tri.AddRange(new[] { s, s + 1, s + 2 });
        }
        Mesh m = new Mesh { name = name };
        m.SetVertices(v);
        m.SetTriangles(tri, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return SaveAsset(m, $"Assets/Meshes/{name}.asset");
    }

    static void CreateMeshes()
    {
        torusOuter = Torus("TorusOuter", 2.2f, 0.18f, 64, 16);
        torusInner = Torus("TorusInner", 1.85f, 0.07f, 64, 10);
        torusFlat = Torus("TorusGround", 2.2f, 0.08f, 64, 8);
        cone = Cone("Cone", 16);
    }

    // ---------------- Materials ----------------

    static void CreateMaterials()
    {
        System.Random rng = new System.Random(4);
        Texture2D grass = SaveTexture("Grass", 256, (x, y) =>
        {
            float n = Mathf.PerlinNoise(x * 0.06f, y * 0.06f) * 0.25f + (float)rng.NextDouble() * 0.08f;
            return new Color(0.26f + n * 0.4f, 0.55f + n * 0.5f, 0.2f + n * 0.3f);
        });
        Texture2D stone = SaveTexture("Stone", 256, (x, y) =>
        {
            int row = y / 32;
            int bx = (x + (row % 2) * 32) % 64;
            bool mortar = y % 32 < 3 || bx < 3;
            float n = Mathf.PerlinNoise(x * 0.1f, y * 0.1f) * 0.15f;
            return mortar ? new Color(0.28f, 0.28f, 0.32f) : new Color(0.55f + n, 0.56f + n, 0.6f + n);
        });
        Texture2D wood = SaveTexture("Wood", 128, (x, y) =>
        {
            bool frame = x < 10 || y < 10 || x > 117 || y > 117;
            bool plank = Mathf.Abs(x - y) < 8 || Mathf.Abs(x - (127 - y)) < 8;
            float g = Mathf.Sin(x * 0.4f + Mathf.Sin(y * 0.05f) * 3f) * 0.05f;
            return frame || plank ? new Color(0.42f, 0.26f, 0.12f) : new Color(0.72f + g, 0.5f + g, 0.26f + g);
        });
        Texture2D tiles = SaveTexture("Tiles", 128, (x, y) =>
        {
            bool grout = x % 32 < 2 || y % 32 < 2;
            float n = ((x / 32 + y / 32) % 2) * 0.05f;
            return grout ? new Color(0.55f, 0.5f, 0.42f) : new Color(0.86f - n, 0.8f - n, 0.66f - n);
        });
        Texture2D soft = SaveTexture("SoftDot", 64, (x, y) =>
        {
            float d = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f);
            return new Color(1f, 1f, 1f, d * d);
        }, false);

        mGrass = Lit("Grass", Color.white, 0.1f, 0f, grass, new Vector2(15f, 15f));
        mStone = Lit("Stone", Color.white, 0.2f, 0f, stone, new Vector2(1f, 1f));
        mWood = Lit("Wood", Color.white, 0.25f, 0f, wood);
        mTiles = Lit("Tiles", Color.white, 0.4f, 0f, tiles, new Vector2(4f, 4f));
        mTrunk = Lit("Trunk", new Color(0.42f, 0.26f, 0.15f), 0.1f);
        mLeaves = Lit("Leaves", new Color(0.18f, 0.56f, 0.3f), 0.15f);
        mRock = Lit("Rock", new Color(0.54f, 0.56f, 0.6f), 0.1f);
        mGold = Lit("Gold", new Color(1f, 0.76f, 0.18f), 0.85f, 1f, null, null, new Color(1f, 0.55f, 0.05f) * 0.3f);
        mEnemy = Lit("EnemyBody", new Color(0.88f, 0.14f, 0.32f), 0.5f, 0.2f, null, null, new Color(1f, 0.05f, 0.2f) * 0.6f);
        mSpike = Lit("EnemySpike", new Color(0.25f, 0.05f, 0.1f), 0.4f);
        mEyeWhite = Lit("EyeWhite", Color.white, 0.6f, 0f, null, null, Color.white * 0.3f);
        mPupil = Lit("Pupil", new Color(0.05f, 0.05f, 0.05f), 0.8f);
        mEnemyGlow = Transparent("EnemyGlow", "Universal Render Pipeline/Unlit", new Color(1f, 0.15f, 0.3f, 0.12f), true);
        mCyan = Lit("PortalCyan", new Color(0f, 0.9f, 1f), 0.6f, 0.3f, null, null, new Color(0f, 0.9f, 1f) * 3f);
        mMagenta = Lit("PortalMagenta", new Color(0.88f, 0.25f, 0.98f), 0.6f, 0.3f, null, null, new Color(0.88f, 0.25f, 0.98f) * 3f);
        mPortalDisc = Transparent("PortalDisc", "Universal Render Pipeline/Unlit", new Color(0.5f, 1f, 1f, 0.35f), true);
        mPortalBase = Lit("PortalBase", new Color(0.15f, 0.2f, 0.23f), 0.5f);
        mLampGlow = Lit("LampGlow", new Color(1f, 0.95f, 0.75f), 0.5f, 0f, null, null, new Color(1f, 0.8f, 0.4f) * 2.5f);
        mPole = Lit("Pole", new Color(0.2f, 0.2f, 0.22f), 0.5f, 0.6f);
        mWater = Lit("Water", new Color(0.3f, 0.75f, 0.95f), 0.9f, 0f, null, null, new Color(0.1f, 0.6f, 1f) * 0.8f);
        mParticle = Transparent("ParticleGlow", "Universal Render Pipeline/Particles/Unlit", Color.white, true, soft);
        mConfetti = Lit("Confetti", Color.white, 0.4f);
        mConfetti = SaveAsset(new Material(Shader.Find("Universal Render Pipeline/Particles/Lit")) { name = "Confetti" }, "Assets/Materials/Confetti.mat");

        Material sky = new Material(Shader.Find("Skybox/Procedural")) { name = "Sky" };
        sky.SetColor("_SkyTint", new Color(0.45f, 0.6f, 0.95f));
        sky.SetColor("_GroundColor", new Color(0.35f, 0.4f, 0.3f));
        sky.SetFloat("_AtmosphereThickness", 0.9f);
        sky.SetFloat("_Exposure", 1.2f);
        sky = SaveAsset(sky, "Assets/Materials/Sky.mat");
        RenderSettings.skybox = sky;

        Material outline = new Material(TMP_Settings.defaultFontAsset.material) { name = "TMP_Outline" };
        outline.EnableKeyword(ShaderUtilities.Keyword_Outline);
        outline.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.15f);
        outline.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.05f, 0.05f, 0.15f, 1f));
        outline.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        outline.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.6f));
        outline.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
        outline.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
        textOutline = SaveAsset(outline, "Assets/Materials/TMP_Outline.mat");

        uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    // ---------------- Effects ----------------

    static ParticleSystem Particles(GameObject go, Material mat, Gradient colors, bool loop, int burst, float rate,
        Vector2 life, Vector2 speed, Vector2 size, float gravity, ParticleSystemShapeType shape, float radius)
    {
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = loop ? 2f : 1f;
        main.loop = loop;
        main.playOnAwake = loop;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = new ParticleSystem.MinMaxGradient(colors) { mode = ParticleSystemGradientMode.RandomColor };
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;
        var em = ps.emission;
        em.rateOverTime = rate;
        em.SetBursts(burst > 0 ? new[] { new ParticleSystem.Burst(0f, (short)burst) } : new ParticleSystem.Burst[0]);
        var sh = ps.shape;
        sh.shapeType = shape;
        sh.radius = radius;
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
        return ps;
    }

    static Gradient Colors(params Color[] cs)
    {
        Gradient g = new Gradient();
        g.SetKeys(cs.Select((c, i) => new GradientColorKey(c, cs.Length == 1 ? 0f : i / (float)(cs.Length - 1))).ToArray(),
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    static ParticleSystem SaveFx(GameObject go)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"Assets/Prefabs/{go.name}.prefab");
        UnityEngine.Object.DestroyImmediate(go);
        return prefab.GetComponent<ParticleSystem>();
    }

    static void CreateEffects()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Color gold = new Color(1f, 0.85f, 0.3f);

        GameObject coin = new GameObject("FX_CoinBurst");
        Particles(coin, mParticle, Colors(gold, Color.white, new Color(1f, 0.95f, 0.6f)), false, 40, 0,
            new Vector2(0.4f, 0.8f), new Vector2(2f, 5f), new Vector2(0.1f, 0.3f), 0.4f, ParticleSystemShapeType.Sphere, 0.2f);
        fxCoin = SaveFx(coin);

        GameObject hit = new GameObject("FX_HitBurst");
        Particles(hit, mParticle, Colors(new Color(1f, 0.1f, 0.2f), new Color(1f, 0.55f, 0.1f), Color.white), false, 45, 0,
            new Vector2(0.3f, 0.7f), new Vector2(3f, 7f), new Vector2(0.12f, 0.35f), 1f, ParticleSystemShapeType.Sphere, 0.3f);
        fxHit = SaveFx(hit);

        GameObject vic = new GameObject("FX_Victory");
        ParticleSystem v = Particles(vic, mConfetti,
            Colors(new Color(1f, 0.3f, 0.3f), gold, new Color(0.4f, 0.95f, 0.6f), new Color(0.3f, 0.75f, 1f), new Color(0.9f, 0.4f, 1f)),
            false, 0, 0, new Vector2(2f, 3f), new Vector2(7f, 13f), new Vector2(0.1f, 0.22f), 1.2f, ParticleSystemShapeType.Cone, 0.5f);
        var vm = v.main;
        vm.duration = 1.6f;
        vm.startRotation3D = true;
        vm.startRotationX = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        vm.startRotationY = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        var vs = v.shape;
        vs.angle = 30f;
        vs.rotation = new Vector3(-90f, 0f, 0f);
        v.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 90), new ParticleSystem.Burst(0.5f, 70), new ParticleSystem.Burst(1f, 70) });
        var rot = v.rotationOverLifetime;
        rot.enabled = true;
        rot.separateAxes = true;
        rot.x = new ParticleSystem.MinMaxCurve(-6f, 6f);
        rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
        ParticleSystemRenderer vr = vic.GetComponent<ParticleSystemRenderer>();
        vr.renderMode = ParticleSystemRenderMode.Mesh;
        vr.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        fxVictory = SaveFx(vic);
    }

    // ---------------- Level ----------------

    static void Platform(Transform parent, string name, float x, float z, float w, float d, float h, Material mat)
    {
        GameObject p = Prim(PrimitiveType.Cube, name, parent, new Vector3(x, h / 2f, z), new Vector3(w, h, d), mat);
        p.isStatic = true;
    }

    static void Tree(Transform parent, float x, float z)
    {
        Transform t = Group("Tree", parent);
        t.localPosition = new Vector3(x, 0f, z);
        Prim(PrimitiveType.Cylinder, "Trunk", t, new Vector3(0f, 1f, 0f), new Vector3(0.6f, 1f, 0.6f), mTrunk);
        MeshObj("Leaves", t, cone, mLeaves, new Vector3(0f, 1.6f, 0f), Vector3.zero, new Vector3(3.2f, 2.6f, 3.2f));
        MeshObj("LeavesTop", t, cone, mLeaves, new Vector3(0f, 3f, 0f), Vector3.zero, new Vector3(2.2f, 1.9f, 2.2f));
    }

    static void Lamp(Transform parent, float x, float z)
    {
        Transform t = Group("Lamp", parent);
        t.localPosition = new Vector3(x, 0f, z);
        Prim(PrimitiveType.Cylinder, "Pole", t, new Vector3(0f, 1.5f, 0f), new Vector3(0.16f, 1.5f, 0.16f), mPole);
        Prim(PrimitiveType.Sphere, "Bulb", t, new Vector3(0f, 3.15f, 0f), Vector3.one * 0.55f, mLampGlow, false);
        Light l = new GameObject("Light").AddComponent<Light>();
        l.transform.SetParent(t, false);
        l.transform.localPosition = new Vector3(0f, 3f, 0f);
        l.type = LightType.Point;
        l.color = new Color(1f, 0.82f, 0.55f);
        l.range = 7f;
        l.intensity = 2.5f;
    }

    static void CoinAt(Transform parent, Vector3 pos)
    {
        GameObject root = new GameObject("Coin");
        root.transform.SetParent(parent, false);
        root.transform.position = pos;
        SphereCollider sc = root.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 0.8f;
        Transform visual = Group("Visual", root.transform);
        Prim(PrimitiveType.Cylinder, "Disc", visual, Vector3.zero, new Vector3(0.85f, 0.04f, 0.85f), mGold, false, new Vector3(90f, 0f, 0f));
        Prim(PrimitiveType.Cylinder, "Emblem", visual, new Vector3(0f, 0f, 0f), new Vector3(0.5f, 0.05f, 0.5f), mGold, false, new Vector3(90f, 0f, 0f));
        root.AddComponent<Coin>().visual = visual;
    }

    static GameObject CreateEnemyPrefab()
    {
        GameObject root = new GameObject("Enemy");
        EnemyController ec = root.AddComponent<EnemyController>();
        Transform body = Group("Body", root.transform);
        Prim(PrimitiveType.Sphere, "Core", body, Vector3.zero, Vector3.one * 1.1f, mEnemy, false);
        Transform spikes = Group("Spikes", body);
        for (int i = 0; i < 6; i++)
        {
            Transform holder = Group("Holder", spikes);
            holder.localEulerAngles = new Vector3(0f, i * 60f, 0f);
            MeshObj("Spike", holder, cone, mSpike, new Vector3(0f, 0f, 0.5f), new Vector3(90f, 0f, 0f), new Vector3(0.26f, 0.38f, 0.26f));
        }
        foreach (float x in new[] { -0.2f, 0.2f })
        {
            Prim(PrimitiveType.Sphere, "Eye", body, new Vector3(x, 0.15f, 0.45f), Vector3.one * 0.28f, mEyeWhite, false);
            Prim(PrimitiveType.Sphere, "Pupil", body, new Vector3(x, 0.15f, 0.57f), Vector3.one * 0.14f, mPupil, false);
        }
        GameObject glow = Prim(PrimitiveType.Sphere, "Glow", body, Vector3.zero, Vector3.one * 1.7f, mEnemyGlow, false);
        glow.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        ec.body = body;
        ec.spikes = spikes;
        ec.glow = glow.GetComponent<Renderer>();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Enemy.prefab");
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    static void Portal(Transform parent)
    {
        Transform root = Group("Portal", parent);
        root.position = new Vector3(0f, 0f, 21f);
        BoxCollider trigger = root.gameObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(3.4f, 4.6f, 1.4f);
        trigger.center = new Vector3(0f, 2.4f, 0f);

        Prim(PrimitiveType.Cylinder, "Base", root, new Vector3(0f, 0.1f, 0f), new Vector3(5.2f, 0.1f, 5.2f), mPortalBase);
        MeshObj("GroundRing", root, torusFlat, mCyan, new Vector3(0f, 0.22f, 0f), new Vector3(90f, 0f, 0f), Vector3.one);

        Transform rings = Group("Rings", root);
        rings.localPosition = new Vector3(0f, 2.6f, 0f);
        GameObject outer = MeshObj("OuterRing", rings, torusOuter, mCyan, Vector3.zero, Vector3.zero, Vector3.one);
        GameObject inner = MeshObj("InnerRing", rings, torusInner, mMagenta, Vector3.zero, Vector3.zero, Vector3.one);
        GameObject disc = Prim(PrimitiveType.Cylinder, "Disc", rings, Vector3.zero, new Vector3(3.6f, 0.01f, 3.6f), mPortalDisc, false, new Vector3(90f, 0f, 0f));
        disc.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

        Light light = new GameObject("PortalLight").AddComponent<Light>();
        light.transform.SetParent(rings, false);
        light.transform.localPosition = new Vector3(0f, 0f, -0.5f);
        light.type = LightType.Point;
        light.color = new Color(0f, 0.9f, 1f);
        light.range = 12f;
        light.intensity = 3f;

        GameObject swirlGo = new GameObject("Swirl");
        swirlGo.transform.SetParent(rings, false);
        ParticleSystem swirl = Particles(swirlGo, mParticle, Colors(new Color(0f, 0.9f, 1f), new Color(0.9f, 0.3f, 1f), Color.white),
            true, 0, 35, new Vector2(1.4f, 2.2f), Vector2.zero, new Vector2(0.08f, 0.2f), 0f, ParticleSystemShapeType.Circle, 2f);
        var sm = swirl.main;
        sm.simulationSpace = ParticleSystemSimulationSpace.Local;
        var vel = swirl.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.orbitalZ = new ParticleSystem.MinMaxCurve(2.2f);
        vel.radial = new ParticleSystem.MinMaxCurve(-0.9f);
        vel.x = vel.y = vel.z = new ParticleSystem.MinMaxCurve(0f);

        foreach (float x in new[] { -3.2f, 3.2f })
        {
            Prim(PrimitiveType.Cylinder, "Pillar", root, new Vector3(x, 2.5f, 0f), new Vector3(0.9f, 2.5f, 0.9f), mStone);
            Prim(PrimitiveType.Sphere, "PillarOrb", root, new Vector3(x, 5.25f, 0f), Vector3.one * 0.8f, mCyan, false);
        }

        GameObject label = new GameObject("FinishLabel");
        label.transform.SetParent(root, false);
        label.transform.localPosition = new Vector3(0f, 6.1f, 0f);
        TextMeshPro tmp = label.AddComponent<TextMeshPro>();
        tmp.text = "FINISH";
        tmp.fontSize = 10;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.fontSharedMaterial = textOutline;
        tmp.rectTransform.sizeDelta = new Vector2(10f, 2f);

        Portal p = root.gameObject.AddComponent<Portal>();
        p.outerRing = outer.transform;
        p.innerRing = inner.transform;
        p.disc = disc.transform;
        p.portalLight = light;
    }

    // ---------------- UI ----------------

    static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static RectTransform Stretch(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    static Image Panel(RectTransform rt, Color color)
    {
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = uiSprite;
        img.type = Image.Type.Sliced;
        img.color = color;
        return img;
    }

    static TMP_Text Label(Transform parent, string name, string text, float size, Color color, Vector2 anchor, Vector2 pivot,
        Vector2 pos, Vector2 box, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        RectTransform rt = Rect(name, parent, anchor, pivot, pos, box);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = FontStyles.Bold;
        t.raycastTarget = false;
        t.fontSharedMaterial = textOutline;
        return t;
    }

    static void ButtonAt(Transform parent, string label, Vector2 pos, Color color, UnityAction action)
    {
        RectTransform rt = Rect(label + "Button", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(380f, 100f));
        Image img = Panel(rt, color);
        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        cb.pressedColor = new Color(0.75f, 0.75f, 0.75f);
        cb.colorMultiplier = 1.2f;
        b.colors = cb;
        UnityEventTools.AddPersistentListener(b.onClick, action);
        Label(rt, "Label", label, 44, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, rt.sizeDelta);
    }

    static GameObject Screen(Transform canvas, string name, Vector2 cardSize, Color border, out RectTransform card)
    {
        RectTransform dim = Stretch(name, canvas);
        dim.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.1f, 0.72f);
        card = Rect("Card", dim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, cardSize);
        Panel(card, border);
        RectTransform inner = Stretch("Inner", card);
        inner.offsetMin = new Vector2(6f, 6f);
        inner.offsetMax = new Vector2(-6f, -6f);
        Panel(inner, new Color(0.06f, 0.08f, 0.2f, 0.97f));
        return dim.gameObject;
    }

    static UIManager BuildUI(GameManager gm)
    {
        GameObject cgo = new GameObject("Canvas");
        Canvas canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();
        UIManager ui = cgo.AddComponent<UIManager>();
        Transform c = cgo.transform;

        RectTransform flash = Stretch("DamageFlash", c);
        ui.damageFlash = flash.gameObject.AddComponent<Image>();
        ui.damageFlash.raycastTarget = false;
        ui.damageFlash.color = Color.clear;

        RectTransform hud = Stretch("HUD", c);
        ui.hud = hud.gameObject;
        RectTransform left = Rect("Left", hud, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -28f), new Vector2(380f, 190f));
        Panel(left, new Color(0.04f, 0.06f, 0.18f, 0.55f)).raycastTarget = false;
        Color gold = new Color(1f, 0.84f, 0.29f);
        ui.scoreText = Label(left, "Score", "Score: 0", 52, gold, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -14f), new Vector2(340f, 64f), TextAlignmentOptions.Left);
        ui.healthText = Label(left, "Health", "Health: 3", 52, new Color(1f, 0.55f, 0.55f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -76f), new Vector2(340f, 64f), TextAlignmentOptions.Left);
        ui.coinsText = Label(left, "Coins", "Coins: 0 / 14", 30, new Color(1f, 1f, 1f, 0.85f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -138f), new Vector2(340f, 40f), TextAlignmentOptions.Left);

        RectTransform right = Rect("Right", hud, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(300f, 120f));
        Panel(right, new Color(0.04f, 0.06f, 0.18f, 0.55f)).raycastTarget = false;
        ui.timeText = Label(right, "Time", "Time: 60", 52, Color.white, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -14f), new Vector2(260f, 64f), TextAlignmentOptions.Right);
        ui.speedText = Label(right, "Speed", "Speed: 6", 28, new Color(1f, 1f, 1f, 0.8f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -76f), new Vector2(260f, 36f), TextAlignmentOptions.Right);

        Label(hud, "Crosshair", "+", 34, new Color(1f, 1f, 1f, 0.9f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));
        ui.toastText = Label(hud, "Toast", "", 46, gold, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1400f, 70f));
        ui.lockHint = Label(hud, "LockHint", "Click the game to look around with the mouse", 28, Color.white, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1000f, 50f));
        Label(hud, "MuteHint", "M: mute", 22, new Color(1f, 1f, 1f, 0.6f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 20f), new Vector2(200f, 36f), TextAlignmentOptions.Right);

        Color green = new Color(0.2f, 0.78f, 0.45f);
        Color blue = new Color(0.25f, 0.5f, 0.95f);

        ui.startScreen = Screen(c, "StartScreen", new Vector2(980f, 760f), gold, out RectTransform start);
        RectTransform title = (RectTransform)Label(start, "Title", "COIN RUSH <color=#33e5ff>3D</color>", 112, gold, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(940f, 140f)).transform;
        Label(start, "Tagline", "Collect the coins, avoid enemies, and reach the portal!", 34, Color.white, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(940f, 50f));
        Label(start, "Rules", "Collect coins.\nAvoid enemies.\nReach the portal before time runs out.", 36, new Color(0.85f, 0.9f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -265f), new Vector2(940f, 160f));
        Label(start, "Controls", "<color=#ffd54a>WASD / Arrows</color> Move   <color=#ffd54a>Mouse</color> Look   <color=#ffd54a>Space</color> Jump   <color=#ffd54a>Shift</color> Sprint   <color=#ffd54a>+ / -</color> Speed",
            24, Color.white, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -455f), new Vector2(940f, 40f));
        ButtonAt(start, "START GAME", new Vector2(0f, -250f), green, ui.OnStartClicked);

        ui.winScreen = Screen(c, "WinScreen", new Vector2(820f, 600f), gold, out RectTransform win);
        Label(win, "Title", "YOU WIN!", 110, gold, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(780f, 140f));
        ui.winScore = Label(win, "FinalScore", "Final Score: 0", 46, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(760f, 60f));
        ui.winCoins = Label(win, "Coins", "Coins Collected: 0", 46, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -5f), new Vector2(760f, 60f));
        ui.winTime = Label(win, "TimeRemaining", "Time Remaining: 0s", 46, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(760f, 60f));
        ButtonAt(win, "PLAY AGAIN", new Vector2(0f, -200f), green, ui.OnRestartClicked);

        Color red = new Color(1f, 0.35f, 0.38f);
        ui.gameOverScreen = Screen(c, "GameOverScreen", new Vector2(820f, 540f), red, out RectTransform lose);
        ui.gameOverTitle = Label(lose, "Title", "GAME OVER", 110, red, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(780f, 140f));
        ui.gameOverScore = Label(lose, "Score", "Score: 0", 46, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(760f, 60f));
        ui.gameOverCoins = Label(lose, "Coins", "Coins: 0", 46, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -25f), new Vector2(760f, 60f));
        ButtonAt(lose, "RESTART", new Vector2(0f, -170f), blue, ui.OnRestartClicked);

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        Type module = null;
#if ENABLE_INPUT_SYSTEM
        module = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
#endif
        if (module != null) es.AddComponent(module);
        else es.AddComponent<StandaloneInputModule>();

        gm.ui = ui;
        return ui;
    }

    // ---------------- Scene ----------------

    static void BuildScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Sky.mat");
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.6f, 0.7f, 0.85f);
        RenderSettings.ambientEquatorColor = new Color(0.45f, 0.5f, 0.5f);
        RenderSettings.ambientGroundColor = new Color(0.25f, 0.28f, 0.22f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.72f, 0.84f, 0.95f);
        RenderSettings.fogStartDistance = 30f;
        RenderSettings.fogEndDistance = 85f;

        Light sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        sun.color = new Color(1f, 0.95f, 0.85f);
        sun.intensity = 1.3f;
        sun.shadows = LightShadows.Soft;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        Bloom bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(1.1f);
        bloom.threshold.Override(1f);
        bloom.scatter.Override(0.65f);
        Tonemapping tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.Neutral);
        Vignette vig = profile.Add<Vignette>(true);
        vig.intensity.Override(0.22f);
        profile = SaveAsset(profile, "Assets/Settings/CoinRush3DVolume.asset");
        foreach (var comp in new VolumeComponent[] { bloom, tone, vig })
        {
            comp.name = comp.GetType().Name;
            AssetDatabase.AddObjectToAsset(comp, profile);
        }
        Volume volume = new GameObject("Global Volume").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;

        Transform env = Group("Environment");
        GameObject ground = Prim(PrimitiveType.Plane, "Ground", env, Vector3.zero, new Vector3(6f, 1f, 6f), mGrass);
        ground.isStatic = true;
        Material wallMat = new Material(mStone) { name = "Wall" };
        wallMat.SetTextureScale("_BaseMap", new Vector2(25f, 1.5f));
        wallMat = SaveAsset(wallMat, "Assets/Materials/Wall.mat");
        Material wallMatSide = new Material(wallMat) { name = "WallSide" };
        wallMatSide = SaveAsset(wallMatSide, "Assets/Materials/WallSide.mat");
        Platform(env, "Wall_N", 0f, 25f, 50f, 1f, 3f, wallMat);
        Platform(env, "Wall_S", 0f, -25f, 50f, 1f, 3f, wallMat);
        Platform(env, "Wall_W", -25f, 0f, 1f, 50f, 3f, wallMatSide);
        Platform(env, "Wall_E", 25f, 0f, 1f, 50f, 3f, wallMatSide);

        Transform plaza = Group("Plaza", env);
        Platform(plaza, "Floor", 0f, 6f, 10f, 10f, 0.3f, mTiles);
        Prim(PrimitiveType.Cylinder, "Fountain", plaza, new Vector3(0f, 0.6f, 6f), new Vector3(2f, 0.3f, 2f), mTiles);
        Prim(PrimitiveType.Cylinder, "Water", plaza, new Vector3(0f, 0.92f, 6f), new Vector3(1.6f, 0.03f, 1.6f), mWater, false);

        Transform plats = Group("Platforms", env);
        Material platStone = new Material(mStone) { name = "PlatformStone" };
        platStone.SetTextureScale("_BaseMap", new Vector2(2f, 2f));
        platStone = SaveAsset(platStone, "Assets/Materials/PlatformStone.mat");
        Platform(plats, "Step_W1", -13f, -6f, 5f, 5f, 0.6f, platStone);
        Platform(plats, "Step_W2", -15f, 2f, 5f, 5f, 1.2f, platStone);
        Platform(plats, "Step_W3", -13f, 11f, 5f, 5f, 1.8f, platStone);
        Platform(plats, "Step_E1", 13f, -8f, 5f, 5f, 0.6f, mWood);
        Platform(plats, "Step_E2", 15f, 0f, 4f, 4f, 1.2f, mWood);

        Transform crates = Group("Crates", env);
        foreach (Vector3 p in new[] { new Vector3(5f, 0.6f, -10f), new Vector3(6.3f, 0.6f, -10f), new Vector3(5.6f, 1.8f, -10f),
                     new Vector3(-6f, 0.6f, -2f), new Vector3(9f, 0.6f, 15f), new Vector3(-9f, 0.6f, 16f), new Vector3(-9f, 0.6f, 17.3f) })
            Prim(PrimitiveType.Cube, "Crate", crates, p, Vector3.one * 1.2f, mWood).isStatic = true;

        Transform trees = Group("Trees", env);
        foreach (Vector2 t in new[] { new Vector2(-20f, -18f), new Vector2(20f, -18f), new Vector2(-20f, 19f), new Vector2(20f, 18f),
                     new Vector2(-9f, -17f), new Vector2(10f, -16f), new Vector2(-21f, -7f), new Vector2(21f, 7f) })
            Tree(trees, t.x, t.y);

        Transform lamps = Group("Lamps", env);
        foreach (Vector2 l in new[] { new Vector2(-6f, 1f), new Vector2(6f, 1f), new Vector2(-6f, 11f), new Vector2(6f, 11f) })
            Lamp(lamps, l.x, l.y);

        Transform rocks = Group("Rocks", env);
        foreach (Vector4 r in new[] { new Vector4(-17f, 12f, 1.4f, 0.8f), new Vector4(17f, -13f, 1.1f, 0.7f), new Vector4(-3f, -18f, 0.8f, 0.6f),
                     new Vector4(19f, 12f, 1.3f, 0.9f), new Vector4(-18f, 7f, 1.1f, 0.7f) })
            Prim(PrimitiveType.Sphere, "Rock", rocks, new Vector3(r.x, 0.2f, r.y), new Vector3(r.z, r.w, r.z * 0.9f), mRock);

        Transform coins = Group("Coins");
        foreach (Vector3 p in new[]
                 {
                     new Vector3(0f, 1.2f, -14f), new Vector3(-6f, 1.2f, -10f), new Vector3(6f, 1.2f, -13f), new Vector3(-13f, 1.8f, -6f),
                     new Vector3(-15f, 2.4f, 2f), new Vector3(-13f, 3f, 11f), new Vector3(13f, 1.8f, -8f), new Vector3(15f, 2.4f, 0f),
                     new Vector3(0f, 2.1f, 6f), new Vector3(-4f, 1.5f, 6f), new Vector3(4f, 1.5f, 6f), new Vector3(10f, 1.2f, 14f),
                     new Vector3(-18f, 1.2f, -14f), new Vector3(18f, 1.2f, 12f)
                 })
            CoinAt(coins, p);

        GameObject enemyPrefab = CreateEnemyPrefab();
        Transform enemies = Group("Enemies");
        var paths = new (Vector3[] pts, float speed)[]
        {
            (new[] { new Vector3(-8f, 0f, -12f), new Vector3(8f, 0f, -12f) }, 3f),
            (new[] { new Vector3(-7f, 0f, 0.2f), new Vector3(7f, 0f, 0.2f), new Vector3(7f, 0f, 12f), new Vector3(-7f, 0f, 12f) }, 3.4f),
            (new[] { new Vector3(10f, 0f, -4f), new Vector3(19f, 0f, -4f), new Vector3(19f, 0f, 9f), new Vector3(10f, 0f, 9f) }, 3f),
            (new[] { new Vector3(-6f, 0f, 18.5f), new Vector3(6f, 0f, 18.5f) }, 4f)
        };
        foreach (var path in paths)
        {
            GameObject e = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, enemies);
            e.transform.position = new Vector3(path.pts[0].x, 0.9f, path.pts[0].z);
            EnemyController ec = e.GetComponent<EnemyController>();
            ec.waypoints = path.pts;
            ec.speed = path.speed;
        }

        Portal(env);

        GameObject playerGo = new GameObject("Player");
        playerGo.transform.position = new Vector3(0f, 0.05f, -20f);
        CharacterController cc = playerGo.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.stepOffset = 0.65f;
        cc.slopeLimit = 50f;
        Transform pivot = Group("CameraPivot", playerGo.transform);
        pivot.localPosition = new Vector3(0f, 1.6f, 0f);
        GameObject camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        camGo.transform.SetParent(pivot, false);
        Camera cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 70f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 200f;
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        camGo.AddComponent<AudioListener>();
        PlayerController pc = playerGo.AddComponent<PlayerController>();
        pc.cameraPivot = pivot;

        AudioManager audio = new GameObject("AudioManager").AddComponent<AudioManager>();
        audio.coin = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/coin.wav");
        audio.hit = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/hit.wav");
        audio.win = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/win.wav");
        audio.lose = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/lose.wav");
        audio.music = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/music.wav");

        GameManager gm = new GameObject("GameManager").AddComponent<GameManager>();
        gm.player = pc;
        gm.audioManager = audio;
        gm.coinsRoot = coins;
        gm.coinBurstPrefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>("Assets/Prefabs/FX_CoinBurst.prefab");
        gm.hitBurstPrefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>("Assets/Prefabs/FX_HitBurst.prefab");
        gm.victoryPrefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>("Assets/Prefabs/FX_Victory.prefab");
        BuildUI(gm);

        EditorSceneManager.SaveScene(scene, ScenePath);
    }
}
