using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class ForestRelicBuilder
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string RootPath = "Assets/Paulo";
    private const string RelicPath = RootPath + "/Prefabs/ForestRelic.prefab";
    private const string FinalEffectPath = RootPath + "/VFX/CompletionBurst.prefab";

    private struct RelicDefinition
    {
        public readonly string name;
        public readonly PrimitiveType shape;
        public readonly Color color;
        public readonly Vector2 position;

        public RelicDefinition(string name, PrimitiveType shape, string color, float x, float z)
        {
            this.name = name;
            this.shape = shape;
            this.color = ColorUtility.TryParseHtmlString(color, out Color parsed) ? parsed : Color.white;
            position = new Vector2(x, z);
        }
    }

    private static readonly RelicDefinition[] Relics =
    {
        new RelicDefinition("Turquoise Sphere", PrimitiveType.Sphere, "#34E7D5", 3, 6),
        new RelicDefinition("Violet Crystal", PrimitiveType.Cube, "#A475E8", 24, 18),
        new RelicDefinition("Scarlet Cube", PrimitiveType.Cube, "#EE5353", -20, 30),
        new RelicDefinition("Golden Rune", PrimitiveType.Cube, "#FFD05C", -54, 60),
        new RelicDefinition("Emerald Gem", PrimitiveType.Capsule, "#60E585", 62, 55),
        new RelicDefinition("Azure Totem", PrimitiveType.Cylinder, "#568FEF", 90, -15),
        new RelicDefinition("Ivory Orb", PrimitiveType.Sphere, "#EFF7FF", -75, -38),
        new RelicDefinition("Amber Fragment", PrimitiveType.Cube, "#FC903C", 35, -72),
        new RelicDefinition("Rose Disk", PrimitiveType.Cylinder, "#FA83C4", -28, -88),
        new RelicDefinition("Cyan Ancient Stone", PrimitiveType.Capsule, "#59D8F6", 82, 92)
    };

    [MenuItem("Tools/Paulo/Build Forest Relic Collection")]
    public static void BuildCollection()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Terrain terrain = Object.FindAnyObjectByType<Terrain>();
        GameObject player = GameObject.FindWithTag("Player");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RelicPath);
        if (!scene.isLoaded || scene.isDirty || SceneManager.sceneCount != 1 ||
            EditorApplication.isPlayingOrWillChangePlaymode || !terrain || !terrain.terrainData ||
            !player || !prefab || !player.GetComponent<FirstPersonController>() ||
            GameObject.Find("Relic Collection") || GameObject.Find("Relic HUD") ||
            AssetDatabase.LoadAssetAtPath<GameObject>(FinalEffectPath))
            throw new InvalidOperationException("Expected a clean SampleScene with the original relic and player; collection already exists or prerequisites are missing.");

        var original = Object.FindObjectsByType<ForestRelicTrigger>();
        if (original.Length != 1 ||
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(original[0].gameObject) != RelicPath)
            throw new InvalidOperationException("Expected exactly one existing forest relic prefab instance.");

        Vector3[] sites = ChooseSites(terrain, player.transform.position);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader)
            throw new InvalidOperationException("URP Lit shader unavailable; scene unchanged.");

        EnsureFolder(RootPath, "VFX");
        var managerObject = new GameObject("Relic Collection");
        var manager = managerObject.AddComponent<RelicManager>();
        var completionSource = Object.Instantiate(prefab.GetComponentInChildren<ParticleSystem>().gameObject);
        completionSource.name = "Completion Burst";
        ConfigureEffect(completionSource.GetComponent<ParticleSystem>(), 10, Relics[9].color);
        GameObject completionPrefab = PrefabUtility.SaveAsPrefabAsset(completionSource, FinalEffectPath);
        Object.DestroyImmediate(completionSource);
        if (!completionPrefab)
            throw new InvalidOperationException("Could not save completion effect.");
        var completion = (GameObject)PrefabUtility.InstantiatePrefab(completionPrefab, scene);
        completion.transform.SetParent(managerObject.transform);
        SetReference(manager, "completionEffect", completion.GetComponent<ParticleSystem>());

        for (int i = 0; i < Relics.Length; i++)
        {
            GameObject relic = i == 0 ? original[0].gameObject : (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            relic.name = Relics[i].name;
            relic.transform.position = sites[i];
            PrefabUtility.RecordPrefabInstancePropertyModifications(relic);
            PrefabUtility.RecordPrefabInstancePropertyModifications(relic.transform);
            var trigger = relic.GetComponent<ForestRelicTrigger>();
            SetReference(trigger, "manager", manager);

            Renderer body = relic.transform.Find("Relic Crystal").GetComponent<Renderer>();
            Material material = CreateMaterial(shader, Relics[i], i);
            body.sharedMaterial = material;
            PrefabUtility.RecordPrefabInstancePropertyModifications(body);
            ConfigureForm(body.transform, Relics[i], material, i);
            ParticleSystem effect = relic.GetComponentInChildren<ParticleSystem>();
            ConfigureEffect(effect, i, Relics[i].color);
            PrefabUtility.RecordPrefabInstancePropertyModifications(effect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(effect.GetComponent<ParticleSystemRenderer>());
        }

        CreateHUD(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException("Could not save SampleScene after placing relics.");
        Debug.Log("Created ten varied forest relics, shared manager, HUD and completion effect.");
    }

    public static void VerifyCollection()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RelicManager manager = Object.FindAnyObjectByType<RelicManager>();
        RelicUI ui = Object.FindAnyObjectByType<RelicUI>();
        Terrain terrain = Object.FindAnyObjectByType<Terrain>();
        var relics = Object.FindObjectsByType<ForestRelicTrigger>();
        if (!manager || !ui || !terrain || relics.Length != 10 ||
            manager.CollectedCount != 0 || manager.TotalRelics != 10 || manager.IsComplete)
            throw new InvalidOperationException("Scene does not start with exactly ten uncollected relics and one HUD/manager.");

        var managerData = new SerializedObject(manager);
        var completion = managerData.FindProperty("completionEffect").objectReferenceValue as ParticleSystem;
        var uiData = new SerializedObject(ui);
        if (!completion || completion.main.playOnAwake || completion.emission.burstCount != 1 ||
            uiData.FindProperty("manager").objectReferenceValue != manager ||
            !uiData.FindProperty("counter").objectReferenceValue ||
            !uiData.FindProperty("message").objectReferenceValue)
            throw new InvalidOperationException("Manager/HUD/completion effect references are missing.");

        var names = new HashSet<string>();
        var materials = new HashSet<Material>();
        var effects = new HashSet<int>();
        foreach (ForestRelicTrigger relic in relics)
        {
            var serialized = new SerializedObject(relic);
            var particles = serialized.FindProperty("particles").objectReferenceValue as ParticleSystem;
            var renderer = serialized.FindProperty("relicRenderer").objectReferenceValue as Renderer;
            var collider = relic.GetComponent<SphereCollider>();
            Vector3 position = relic.transform.position;
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (!relic.gameObject.activeInHierarchy || !names.Add(relic.name) ||
                serialized.FindProperty("manager").objectReferenceValue != manager ||
                !collider || !collider.isTrigger || !particles || particles.main.playOnAwake ||
                !renderer || !renderer.sharedMaterial ||
                !renderer.GetComponent<MeshFilter>().sharedMesh ||
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(relic.gameObject) != RelicPath ||
                position.x <= origin.x || position.x >= origin.x + size.x ||
                position.z <= origin.z || position.z >= origin.z + size.z ||
                Mathf.Abs(position.y - terrain.SampleHeight(position) - origin.y) > .15f)
                throw new InvalidOperationException("Invalid or ungrounded relic: " + relic.name);

            Debug.Log("Relic placement: " + relic.name + " at " + position);
            materials.Add(renderer.sharedMaterial);
            effects.Add((int)(particles.main.startSpeed.constant * 100f + particles.emission.GetBurst(0).count.constant));
            foreach (ForestRelicTrigger other in relics)
                if (other != relic && Vector3.Distance(position, other.transform.position) < 9f)
                    throw new InvalidOperationException("Relics overlap: " + relic.name + " and " + other.name);
        }
        if (names.Count != 10 || materials.Count != 10 || effects.Count < 8)
            throw new InvalidOperationException("Relics lack distinct identities or effect settings.");

        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (!component)
                    throw new InvalidOperationException("Missing script under " + root.name);

        var probe = new GameObject("Collection verification (temporary)");
        try
        {
            var testManager = probe.AddComponent<RelicManager>();
            var effect = Object.Instantiate(completion, probe.transform);
            var testData = new SerializedObject(testManager);
            testData.FindProperty("completionEffect").objectReferenceValue = effect;
            testData.ApplyModifiedPropertiesWithoutUndo();
            int updates = 0, completed = 0;
            testManager.ProgressChanged += (count, total) => { if (count != ++updates || total != 10) throw new InvalidOperationException("Incorrect progress event."); };
            testManager.CollectionCompleted += () => completed++;
            foreach (ForestRelicTrigger relic in relics)
                if (!testManager.TryCollect(relic) || testManager.TryCollect(relic))
                    throw new InvalidOperationException("A relic was not collected exactly once.");
            if (updates != 10 || completed != 1 || !testManager.IsComplete || testManager.CollectedCount != 10)
                throw new InvalidOperationException("Completion did not fire once at 10/10.");
        }
        finally
        {
            Object.DestroyImmediate(probe);
        }

        Debug.Log("Verified ten grounded unique relics, HUD/manager links, no missing scripts, once-only collection and one completion at 10/10.");
    }

    private static Vector3[] ChooseSites(Terrain terrain, Vector3 playerPosition)
    {
        var sites = new Vector3[Relics.Length];
        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position;
        Vector2[] offsets = { Vector2.zero, new Vector2(4, 0), new Vector2(-4, 0), new Vector2(0, 4),
            new Vector2(0, -4), new Vector2(5, 5), new Vector2(-5, 5), new Vector2(5, -5), new Vector2(-5, -5) };
        Physics.SyncTransforms();
        for (int i = 0; i < Relics.Length; i++)
        {
            bool found = false;
            foreach (Vector2 offset in offsets)
            {
                Vector2 point = Relics[i].position + offset;
                float nx = (point.x - origin.x) / data.size.x;
                float nz = (point.y - origin.z) / data.size.z;
                if (nx < .05f || nx > .95f || nz < .05f || nz > .95f ||
                    data.GetSteepness(nx, nz) > 28f)
                    continue;

                var ground = new Vector3(point.x, 0f, point.y);
                ground.y = terrain.SampleHeight(ground) + origin.y;
                if (Vector3.Distance(new Vector3(ground.x, 0f, ground.z),
                    new Vector3(playerPosition.x, 0f, playerPosition.z)) < 3f)
                    continue;

                bool blocked = false;
                foreach (Collider collider in Physics.OverlapSphere(ground + Vector3.up, .75f))
                    if (collider && collider != terrain.GetComponent<TerrainCollider>() &&
                        !collider.isTrigger && !collider.transform.IsChildOf(GameObject.FindWithTag("Player").transform))
                        blocked = true;
                for (int j = 0; j < i; j++)
                    if (Vector3.Distance(sites[j], ground) < 9f)
                        blocked = true;
                if (blocked)
                    continue;

                sites[i] = ground;
                found = true;
                break;
            }
            if (!found)
                throw new InvalidOperationException("No clear, walkable terrain site for " + Relics[i].name);
        }
        return sites;
    }

    private static Material CreateMaterial(Shader shader, RelicDefinition relic, int index)
    {
        string path = RootPath + "/Materials/Relic" + (index + 1).ToString("00") + ".mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path))
            throw new InvalidOperationException("Relic material already exists: " + path);
        var material = new Material(shader) { name = relic.name };
        material.SetColor("_BaseColor", relic.color);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", relic.color * 1.8f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void ConfigureForm(Transform body, RelicDefinition relic, Material material, int index)
    {
        GameObject source = GameObject.CreatePrimitive(relic.shape);
        body.GetComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(source);
        PrefabUtility.RecordPrefabInstancePropertyModifications(body.GetComponent<MeshFilter>());
        Vector3[] sizes = { new Vector3(.75f, .75f, .75f), new Vector3(.52f, 1.1f, .52f),
            new Vector3(.85f, .85f, .85f), new Vector3(.95f, 1.15f, .22f), new Vector3(.55f, .8f, .55f),
            new Vector3(.65f, .65f, .65f), new Vector3(.8f, .8f, .8f), new Vector3(.5f, .9f, .5f),
            new Vector3(1.05f, .1f, 1.05f), new Vector3(.78f, .62f, .6f) };
        body.localScale = sizes[index];
        body.localRotation = index == 1 || index == 7 ? Quaternion.Euler(0f, 30f, 38f) : Quaternion.identity;
        PrefabUtility.RecordPrefabInstancePropertyModifications(body);

        if (index == 0 || index == 2)
            return;

        int pieces = index == 3 || index == 6 ? 3 : 2;
        for (int part = 0; part < pieces; part++)
        {
            PrimitiveType type = index == 4 || index == 5 || index == 8 ? PrimitiveType.Sphere : PrimitiveType.Cube;
            GameObject accent = GameObject.CreatePrimitive(type);
            accent.name = "Relic Detail " + (part + 1);
            Object.DestroyImmediate(accent.GetComponent<Collider>());
            accent.transform.SetParent(body, false);
            accent.transform.localPosition = new Vector3((part - (pieces - 1) * .5f) * .55f,
                index == 8 ? 1.2f : part * .48f - .25f, index == 3 ? -.6f : 0f);
            accent.transform.localScale = index == 6 ? new Vector3(.08f, .1f, 1.4f) : Vector3.one * .22f;
            accent.GetComponent<Renderer>().sharedMaterial = material;
        }
    }

    private static void ConfigureEffect(ParticleSystem particles, int style, Color color)
    {
        var main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = .3f;
        main.startLifetime = style == 10 ? 2.3f : 1.4f + (style % 3) * .25f;
        main.startSpeed = style == 10 ? 5f : 1.1f + (style % 4) * .7f;
        main.startSize = style == 10 ? .32f : .13f + (style % 3) * .05f;
        main.startColor = color;
        main.startRotation = style % 2 == 0 ? 0f : 45f * Mathf.Deg2Rad;
        main.gravityModifier = style == 4 || style == 8 ? -.25f : style == 9 ? .28f : 0f;
        main.maxParticles = style == 10 ? 180 : 60;

        var emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(style == 10 ? 150 : 18 + style * 3)) });
        var shape = particles.shape;
        shape.shapeType = style == 10 ? ParticleSystemShapeType.Sphere :
            style % 3 == 0 ? ParticleSystemShapeType.Sphere :
            style % 3 == 1 ? ParticleSystemShapeType.Cone : ParticleSystemShapeType.Box;
        shape.radius = style == 10 ? .9f : .15f + (style % 4) * .1f;
        var velocity = particles.velocityOverLifetime;
        velocity.enabled = style == 1 || style == 4 || style == 8;
        velocity.y = new ParticleSystem.MinMaxCurve(style == 8 ? -1.8f : 1.3f);
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private static void CreateHUD(RelicManager manager)
    {
        var hud = new GameObject("Relic HUD");
        hud.SetActive(false);
        var canvas = hud.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = hud.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        hud.AddComponent<GraphicRaycaster>();
        var ui = hud.AddComponent<RelicUI>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (!font)
            throw new InvalidOperationException("Unity built-in UI font is unavailable.");
        Text counter = CreateText(hud.transform, "Relic Counter", font, 34, TextAnchor.UpperLeft,
            new Vector2(24f, -24f), new Vector2(560f, 70f), new Vector2(0f, 1f));
        Text message = CreateText(hud.transform, "Relic Message", font, 42, TextAnchor.MiddleCenter,
            new Vector2(0f, -100f), new Vector2(920f, 100f), new Vector2(.5f, 1f));
        var serialized = new SerializedObject(ui);
        serialized.FindProperty("manager").objectReferenceValue = manager;
        serialized.FindProperty("counter").objectReferenceValue = counter;
        serialized.FindProperty("message").objectReferenceValue = message;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        hud.SetActive(true);
    }

    private static Text CreateText(Transform parent, string name, Font font, int size,
        TextAnchor alignment, Vector2 position, Vector2 dimensions, Vector2 anchor)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        gameObject.transform.SetParent(parent, false);
        var rect = (RectTransform)gameObject.transform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        var text = gameObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static void SetReference(Object target, string property, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(property).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        if (PrefabUtility.IsPartOfPrefabInstance(target))
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }
}
