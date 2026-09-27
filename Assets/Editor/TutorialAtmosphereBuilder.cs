using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class TutorialAtmosphereBuilder
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ProfilePath = "Assets/Scenes/SampleScene_Terrain/TutorialAtmosphere.asset";
    private const string SourceProfilePath = "Assets/Settings/SampleSceneProfile.asset";
    private const string SkyPath = "Assets/Polytope Studio/Lowpoly_Environments/Sources/Materials/PT_Skybox_mat.mat";
    private const string OrePath = "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Rocks/PT_Ore_Rock_01.prefab";

    [MenuItem("Tools/Tutorial/Soften SampleScene Fog")]
    private static void SoftenFog()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || !scene.isLoaded || SceneManager.sceneCount != 1 ||
            EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty ||
            !RenderSettings.fog || !GameObject.Find("Tutorial Ore"))
        {
            Debug.LogError("Open and save only the completed SampleScene in Edit Mode before adjusting fog.");
            return;
        }

        RenderSettings.fogDensity = .003f;
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            Debug.LogError("Fog changed, but SampleScene could not be saved; inspect the scene.");
    }

    [MenuItem("Tools/Tutorial/Add SampleScene Atmosphere")]
    private static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || !scene.isLoaded || SceneManager.sceneCount != 1 ||
            EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty ||
            AssetDatabase.LoadAssetAtPath<Object>(ProfilePath) != null ||
            System.IO.File.Exists(ProfilePath) ||
            !AssetDatabase.IsValidFolder("Assets/Scenes/SampleScene_Terrain"))
        {
            Debug.LogError("Open only the clean SampleScene in Edit Mode; atmosphere output must not already exist.");
            return;
        }

        Terrain terrain = Object.FindFirstObjectByType<Terrain>();
        GameObject volumeObject = GameObject.Find("Tutorial Atmosphere");
        GameObject oreObject = GameObject.Find("Tutorial Ore");
        GameObject sunObject = GameObject.Find("Directional Light");
        GameObject existingVolumeObject = GameObject.Find("Global Volume");
        Volume volume = existingVolumeObject ? existingVolumeObject.GetComponent<Volume>() : null;
        VolumeProfile sourceProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(SourceProfilePath);
        Light sun = sunObject ? sunObject.GetComponent<Light>() : null;
        Material sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
        GameObject ore = AssetDatabase.LoadAssetAtPath<GameObject>(OrePath);
        if (!terrain || !terrain.terrainData || volumeObject || oreObject || !sun || sun.type != LightType.Directional ||
            !volume || !volume.isGlobal || volume.gameObject.scene != scene || !sourceProfile ||
            volume.sharedProfile != sourceProfile ||
            !sky || !sky.shader || !sky.shader.name.StartsWith("Skybox/") || !ore ||
            terrain.terrainData.treePrototypes.Length < 2 || terrain.terrainData.detailPrototypes.Length == 0)
        {
            Debug.LogError("Expected terrain, original Global Volume/profile, directional light, imported skybox and ore prefab are required; no changes made.");
            return;
        }

        if (!AssetDatabase.CopyAsset(SourceProfilePath, ProfilePath))
        {
            Debug.LogError("Could not copy the existing volume profile; no scene changes made.");
            return;
        }
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (!profile)
        {
            Debug.LogError("Copied volume profile could not be loaded; no scene changes made.");
            return;
        }
        profile.name = "Tutorial Atmosphere";
        Bloom bloom;
        if (!profile.TryGet(out bloom)) bloom = profile.Add<Bloom>(true);
        bloom.active = true;
        bloom.intensity.Override(.22f);
        bloom.threshold.Override(1.1f);
        ColorAdjustments color;
        if (!profile.TryGet(out color)) color = profile.Add<ColorAdjustments>(true);
        color.active = true;
        color.postExposure.Override(.12f);
        color.contrast.Override(8f);
        ChannelMixer mixer;
        if (!profile.TryGet(out mixer)) mixer = profile.Add<ChannelMixer>(true);
        mixer.active = true;
        mixer.redOutRedIn.Override(102f);
        mixer.greenOutGreenIn.Override(100f);
        mixer.blueOutBlueIn.Override(98f);
        LiftGammaGain lift;
        if (!profile.TryGet(out lift)) lift = profile.Add<LiftGammaGain>(true);
        lift.active = true;
        lift.gain.Override(new Vector4(1.02f, 1f, .98f, 0f));
        foreach (VolumeComponent component in profile.components)
        {
            if (!component) continue;
            if (!AssetDatabase.Contains(component))
                AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(component);
        }
        EditorUtility.SetDirty(profile);

        terrain.detailObjectDensity = .78f;
        terrain.detailObjectDistance = 90f;
        TerrainData data = terrain.terrainData;
        int width = data.detailWidth, height = data.detailHeight;
        int[,] detail = data.GetDetailLayer(0, 0, width, height, 0);
        for (int z = 0; z < height; z++)
        for (int x = 0; x < width; x++)
            if (detail[z, x] > 0 && Mathf.PerlinNoise(x * .17f + 31, z * .17f + 47) > .62f)
                detail[z, x] = Mathf.Min(3, detail[z, x] + 1);
        data.SetDetailLayer(0, 0, 0, detail);

        var trees = new System.Collections.Generic.List<TreeInstance>(data.treeInstances);
        var random = new System.Random(157);
        for (int i = 0; i < 90; i++)
        {
            float x = .06f + (float)random.NextDouble() * .88f;
            float z = .06f + (float)random.NextDouble() * .88f;
            if ((x - .5f) * (x - .5f) + (z - .5f) * (z - .5f) < .015f ||
                Mathf.PerlinNoise(x * 7f + 3f, z * 7f + 8f) < .38f)
                continue;
            float scale = .8f + (float)random.NextDouble() * .4f;
            trees.Add(new TreeInstance { prototypeIndex = i % 4 == 0 ? 1 : 0,
                position = new Vector3(x, 0, z), widthScale = scale, heightScale = scale,
                rotation = (float)random.NextDouble() * Mathf.PI * 2,
                color = Color.white, lightmapColor = Color.white });
        }
        data.SetTreeInstances(trees.ToArray(), true);

        RenderSettings.skybox = sky;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = .015f;
        RenderSettings.fogColor = new Color(.72f, .76f, .73f);
        RenderSettings.sun = sun;
        sun.color = new Color(1f, .91f, .78f);
        sun.intensity = 1.25f;
        sun.transform.rotation = Quaternion.Euler(45, -35, 0);

        volume.sharedProfile = profile;
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(ore, scene);
        instance.name = "Tutorial Ore";
        Vector3 center = terrain.transform.position + new Vector3(data.size.x * .5f + 15, 0, data.size.z * .5f + 12);
        center.y = terrain.SampleHeight(center) + terrain.transform.position.y;
        instance.transform.position = center;
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            Debug.LogError("Atmosphere assets created, but SampleScene could not be saved; inspect before retrying.");
    }
}
