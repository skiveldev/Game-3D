using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public static class TutorialSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string Output = "Assets/Scenes/SampleScene_Terrain";
    private const string Source = "Assets/Polytope Studio/Lowpoly_Environments/";

    [MenuItem("Tools/Tutorial/Complete SampleScene Grass Coverage")]
    private static void ExtendGrassCoverage()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || !scene.isLoaded || SceneManager.sceneCount != 1 ||
            EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty)
        {
            Debug.LogError("Open only a saved, clean SampleScene in Edit Mode before completing grass coverage.");
            return;
        }

        Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        if (terrains.Length != 1 || terrains[0].gameObject.scene != scene)
        {
            Debug.LogError("Expected exactly one terrain in the clean SampleScene.");
            return;
        }
        Terrain terrain = terrains[0];
        TerrainData data = terrain ? terrain.terrainData : null;
        if (!data || AssetDatabase.GetAssetPath(data) != Output + "/Terrain.asset" ||
            data.detailWidth != 256 || data.detailHeight != 256 ||
            data.detailPrototypes.Length != 1 ||
            data.detailPrototypes[0].renderMode != DetailRenderMode.GrassBillboard ||
            data.detailPrototypes[0].prototype != null ||
            AssetDatabase.GetAssetPath(data.detailPrototypes[0].prototypeTexture) !=
                Source + "Sources/Textures/PT_Grass_01.png" ||
            data.size != new Vector3(240, 32, 240) ||
            terrain.transform.position != new Vector3(-120, 0, -120))
        {
            Debug.LogError("Expected the original SampleScene terrain with one PT_Grass_01 texture billboard detail layer.");
            return;
        }

        TerrainLayer[] layers = data.terrainLayers;
        string textures = Source + "Sources/Textures/";
        if (layers.Length != 2 || !layers[0] || !layers[1] ||
            layers[0].name != "Grass" || layers[1].name != "Soil" ||
            AssetDatabase.GetAssetPath(layers[0]) != Output + "/Grass.terrainlayer" ||
            AssetDatabase.GetAssetPath(layers[1]) != Output + "/Soil.terrainlayer" ||
            !layers[0].diffuseTexture || !layers[1].diffuseTexture ||
            AssetDatabase.GetAssetPath(layers[0].diffuseTexture) != textures + "PT_Ground_Grass_Green_01.png" ||
            AssetDatabase.GetAssetPath(layers[1].diffuseTexture) != textures + "PT_Ground_Generic_03.png" ||
            data.alphamapWidth != 256 || data.alphamapHeight != 256 || data.alphamapLayers != 2)
        {
            Debug.LogError("Expected the original Grass/Soil terrain layers and ground textures; grass was not changed.");
            return;
        }

        const int resolution = 256;
        float[,,] weights = data.GetAlphamaps(0, 0, resolution, resolution);
        int[,] detail = data.GetDetailLayer(0, 0, resolution, resolution, 0);
        long before = 0;
        foreach (int amount in detail)
            before += amount;
        const int grassPerCell = 5;
        int greenCells = 0, greenZeroBefore = 0, greenZeroAfter = 0;
        int skippedBrown = 0, skippedSpawn = 0;
        int filled = 0;
        for (int z = 0; z < resolution; z++)
        for (int x = 0; x < resolution; x++)
        {
            // The builder puts green in alphamap channel 0 and soil in channel 1.
            bool green = weights[z, x, 0] >= weights[z, x, 1];
            if (green) greenCells++;
            if (detail[z, x] != 0)
                continue;
            if (green) greenZeroBefore++;
            if (!green)
            {
                skippedBrown++;
                continue;
            }
            // Cell centers are 240/256m apart; this leaves a roughly 2m spawn patch.
            float dx = (x + .5f - 128) * (240f / resolution);
            float dz = (z + .5f - 128) * (240f / resolution);
            if (dx * dx + dz * dz <= 1f)
            {
                skippedSpawn++;
                continue;
            }
            detail[z, x] = grassPerCell;
            filled++;
        }

        if (filled == 0)
        {
            Debug.Log($"Grass coverage unchanged: total before={before}, after={before}, green zero before={greenZeroBefore}, after={greenZeroBefore}, green cells={greenCells}, brown zero skipped={skippedBrown}, spawn zero skipped={skippedSpawn}, selected=0.");
            return;
        }
        data.SetDetailLayer(0, 0, 0, detail);
        terrain.detailObjectDensity = .9f;
        terrain.detailObjectDistance = 100f;
        EditorUtility.SetDirty(data);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            Debug.LogError("Grass data was saved, but SampleScene detail display settings could not be saved; inspect the scene.");
            return;
        }
        long after = 0;
        int[,] saved = data.GetDetailLayer(0, 0, resolution, resolution, 0);
        greenZeroAfter = 0;
        for (int z = 0; z < resolution; z++)
        for (int x = 0; x < resolution; x++)
        {
            after += saved[z, x];
            if (weights[z, x, 0] >= weights[z, x, 1] && saved[z, x] == 0)
                greenZeroAfter++;
        }
        Debug.Log($"Completed PT_Grass_01 grass coverage: total before={before}, after={after}, green zero before={greenZeroBefore}, after={greenZeroAfter}, green cells={greenCells}, brown zero skipped={skippedBrown}, spawn zero skipped={skippedSpawn}, selected={filled}, added={after - before}, maximum added={resolution * resolution * grassPerCell}. Inspect in Unity.");
    }

    private static void RestoreGrassTexture()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || !scene.isLoaded || SceneManager.sceneCount != 1 ||
            EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty)
        {
            Debug.LogError("Open only a saved, clean SampleScene in Edit Mode before restoring grass.");
            return;
        }

        Terrain terrain = Object.FindAnyObjectByType<Terrain>();
        TerrainData data = terrain ? terrain.terrainData : null;
        if (!data || AssetDatabase.GetAssetPath(data) != Output + "/Terrain.asset" ||
            data.detailPrototypes.Length != 1 || data.detailWidth != 256 || data.detailHeight != 256 ||
            data.size != new Vector3(240, 32, 240) ||
            terrain.transform.position != new Vector3(-120, 0, -120))
        {
            Debug.LogError("Expected the original SampleScene terrain with one 256x256 grass detail layer.");
            return;
        }

        DetailPrototype original = data.detailPrototypes[0];
        string meshPath = AssetDatabase.GetAssetPath(original.prototype);
        if (original.renderMode != DetailRenderMode.VertexLit ||
            (meshPath != Source + "Prefabs/Plants/PT_Grass_02.prefab" && original.prototype != null) ||
            original.prototypeTexture != null)
        {
            Debug.LogError("Expected the failed PT_Grass_02 mesh detail only; refusing to modify another prototype.");
            return;
        }

        Texture2D grass = Require<Texture2D>(Source + "Sources/Textures/PT_Grass_01.png");
        if (!grass)
        {
            Debug.LogError("PT_Grass_01.png is missing; terrain was not changed.");
            return;
        }
        int[,] saved = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, 0);
        long before = 0;
        foreach (int amount in saved)
            before += amount;
        if (before == 0)
        {
            Debug.LogWarning("Grass map has zero painted instances before restoration. No map will be invented; inspect the saved terrain or recover from a known-good backup.");
        }

        DetailPrototype restored = new DetailPrototype {
            prototypeTexture = grass, renderMode = DetailRenderMode.GrassBillboard,
            healthyColor = new Color(.38f, .65f, .27f), dryColor = new Color(.62f, .55f, .29f),
            minWidth = 1f, maxWidth = 2f, minHeight = .58f, maxHeight = 1.7f,
            noiseSpread = .18f
        };
        data.detailPrototypes = new[] { restored };
        int[,] current = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, 0);
        long after = 0;
        foreach (int amount in current)
            after += amount;
        if (after != before)
        {
            data.SetDetailLayer(0, 0, 0, saved);
            current = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, 0);
            after = 0;
            foreach (int amount in current)
                after += amount;
        }
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        if (after != before)
            Debug.LogError($"Grass detail count changed from {before} to {after} despite attempted restoration. Inspect Terrain.asset before further edits.");
        else
            Debug.Log($"Restored PT_Grass_01 texture billboard. Painted instances before={before}, after={after}; verify the map and rendering in Unity. Count equality alone does not prove cell-by-cell preservation.");
    }

    private static void FillGrassClearing()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || !scene.isLoaded || SceneManager.sceneCount != 1 ||
            EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty)
        {
            Debug.LogError("Open only a saved, clean SampleScene in Edit Mode before filling grass.");
            return;
        }

        Terrain terrain = Object.FindAnyObjectByType<Terrain>();
        TerrainData data = terrain ? terrain.terrainData : null;
        if (!data || AssetDatabase.GetAssetPath(data) != Output + "/Terrain.asset" ||
            data.detailPrototypes.Length != 1 ||
            AssetDatabase.GetAssetPath(data.detailPrototypes[0].prototypeTexture) !=
                Source + "Sources/Textures/PT_Grass_01.png" ||
            data.detailWidth != 256 || data.detailHeight != 256 ||
            data.size != new Vector3(240, 32, 240) ||
            terrain.transform.position != new Vector3(-120, 0, -120))
        {
            Debug.LogError("Expected the original SampleScene terrain with one 256x256 grass detail layer.");
            return;
        }

        int[,] detail = data.GetDetailLayer(0, 0, 256, 256, 0);
        int additions = 0;
        for (int z = 0; z < 256; z++)
        for (int x = 0; x < 256; x++)
        {
            float dx = (x - 128) / 128f, dz = (z - 128) / 128f;
            float radiusSquared = dx * dx + dz * dz;
            if (radiusSquared >= .035f || radiusSquared < .00028f)
                continue;
            if (detail[z, x] != 0)
            {
                Debug.LogError("Grass clearing already contains painted details; refusing to overwrite it.");
                return;
            }
            additions++;
        }

        if (additions == 0)
        {
            Debug.LogError("Expected an empty central grass clearing.");
            return;
        }

        for (int z = 0; z < 256; z++)
        for (int x = 0; x < 256; x++)
        {
            float dx = (x - 128) / 128f, dz = (z - 128) / 128f;
            float radiusSquared = dx * dx + dz * dz;
            if (radiusSquared < .00028f || radiusSquared >= .035f)
                continue;
            float noise = Mathf.PerlinNoise(x * .11f + 9, z * .11f + 17);
            detail[z, x] = noise > .43f ? (noise > .7f ? 4 : 2) : 0;
        }

        data.SetDetailLayer(0, 0, 0, detail);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log("Filled the central grass clearing while preserving the spawn patch and existing terrain details. Inspect in Unity.");
    }

    [MenuItem("Tools/Tutorial/Inspect SampleScene Grass")]
    private static void InspectGrass()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || !scene.isLoaded || SceneManager.sceneCount != 1)
        {
            Debug.LogError("Open only SampleScene to inspect its terrain grass.");
            return;
        }

        Terrain terrain = Object.FindAnyObjectByType<Terrain>();
        if (!terrain || !terrain.terrainData)
        {
            Debug.LogError("SampleScene has no terrain data to inspect.");
            return;
        }

        TerrainData data = terrain.terrainData;
        long count = 0;
        if (data.detailPrototypes.Length > 0)
        {
            int[,] details = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, 0);
            foreach (int amount in details)
                count += amount;
        }

        Debug.Log($"Terrain grass: prototypes={data.detailPrototypes.Length}, painted instances={count}, " +
            $"drawing={terrain.drawTreesAndFoliage}, distance={terrain.detailObjectDistance}, " +
            $"density={terrain.detailObjectDensity}.");
    }

    [MenuItem("Tools/Tutorial/Diagnose SampleScene Grass")]
    private static void DiagnoseGrass()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || !scene.isLoaded || SceneManager.sceneCount != 1 ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Open only Assets/Scenes/SampleScene.unity in Edit Mode to diagnose grass.");
            return;
        }

        Terrain terrain = Object.FindAnyObjectByType<Terrain>();
        TerrainData data = terrain ? terrain.terrainData : null;
        if (!data)
        {
            Debug.Log("Grass diagnostic: SampleScene has no terrain data.");
            return;
        }

        string texturePath = Source + "Sources/Textures/PT_Grass_01.png";
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        string alpha = "unknown";
        if (texture)
        {
            try
            {
                float min = 1f, max = 0f;
                int opaque = 0, transparent = 0;
                for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    float a = texture.GetPixel((x * (texture.width - 1)) / 7,
                        (y * (texture.height - 1)) / 7).a;
                    min = Mathf.Min(min, a);
                    max = Mathf.Max(max, a);
                    if (a >= .99f) opaque++;
                    if (a <= .01f) transparent++;
                }
                alpha = $"min={min:F2},max={max:F2},opaque={opaque}/64,transparent={transparent}/64," +
                    $"center={texture.GetPixel(texture.width / 2, texture.height / 2).a:F2}," +
                    $"corner={texture.GetPixel(0, 0).a:F2}";
            }
            catch (UnityException)
            {
                alpha = "unknown (pixel access unavailable)";
            }
        }

        DetailPrototype[] prototypes = data.detailPrototypes;
        string prototypeInfo = "none";
        long total = 0;
        string center = "n/a";
        if (prototypes.Length > 0)
        {
            DetailPrototype p = prototypes[0];
            prototypeInfo = $"texture={AssetDatabase.GetAssetPath(p.prototypeTexture)},mesh={AssetDatabase.GetAssetPath(p.prototype)}," +
                $"mode={p.renderMode},width={p.minWidth:F2}..{p.maxWidth:F2},height={p.minHeight:F2}..{p.maxHeight:F2}," +
                $"align={p.alignToGround:F2},noise={p.noiseSpread:F2}";
            if (data.detailWidth > 0 && data.detailHeight > 0)
            {
                int[,] layer = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, 0);
                foreach (int value in layer) total += value;
                center = layer[data.detailHeight / 2, data.detailWidth / 2].ToString();
            }
        }

        Camera camera = Camera.main;
        Debug.Log($"Grass diagnostic: terrainAsset={AssetDatabase.GetAssetPath(data)},size={data.size},position={terrain.transform.position}," +
            $"detailSize={data.detailWidth}x{data.detailHeight},prototypes={prototypes.Length},layer0Total={total},center={center}," +
            $"draw={terrain.drawTreesAndFoliage},distance={terrain.detailObjectDistance:F1},density={terrain.detailObjectDensity:F2}; " +
            $"layer0={prototypeInfo}; PT_Grass_01={texture?.width ?? 0}x{texture?.height ?? 0}," +
            $"alphaSource={(importer ? importer.alphaSource.ToString() : "unknown")}," +
            $"alphaIsTransparency={(importer ? importer.alphaIsTransparency.ToString() : "unknown")}," +
            $"isReadable={(importer ? importer.isReadable.ToString() : "unknown")},alpha={alpha}; " +
            $"shaders URP={Shader.Find("Universal Render Pipeline/Terrain/Details/Grass") != null}," +
            $"builtIn={Shader.Find("Nature/Terrain/Detail/Grass") != null}; " +
            $"mainCamera={(camera ? camera.transform.position.ToString() : "none")}");
    }

    [MenuItem("Tools/Tutorial/Build SampleScene Terrain")]
    private static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || !scene.isLoaded || SceneManager.sceneCount != 1 ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Open only Assets/Scenes/SampleScene.unity in Edit Mode before building terrain.");
            return;
        }

        if (scene.isDirty || (System.IO.Directory.Exists(Output) &&
            System.IO.Directory.GetFileSystemEntries(Output).Length != 0) ||
            AssetDatabase.LoadAssetAtPath<TerrainData>(Output + "/Terrain.asset") != null ||
            Object.FindAnyObjectByType<Terrain>() != null)
        {
            Debug.LogError("Save the clean scene first; existing terrain or output assets prevent rebuilding.");
            return;
        }

        string textures = Source + "Sources/Textures/";
        string prefabs = Source + "Prefabs/";
        Texture2D green = Require<Texture2D>(textures + "PT_Ground_Grass_Green_01.png");
        Texture2D brown = Require<Texture2D>(textures + "PT_Ground_Generic_03.png");
        Texture2D grassDetail = Require<Texture2D>(textures + "PT_Grass_01.png");
        GameObject pine = Require<GameObject>(prefabs + "Trees/PT_Pine_Tree_03_green.prefab");
        GameObject fruit = Require<GameObject>(prefabs + "Trees/PT_Fruit_Tree_01_apples.prefab");
        GameObject shrub = Require<GameObject>(prefabs + "Shrubs/PT_Generic_Shrub_01_green.prefab");
        GameObject rock = Require<GameObject>(prefabs + "Rocks/PT_Generic_Rock_01.prefab");
        GameObject flower = Require<GameObject>(prefabs + "Flowers/PT_Poppy_02.prefab");
        if (!green || !brown || !grassDetail || !pine || !fruit || !shrub || !rock || !flower)
        {
            Debug.LogError("Missing imported environment assets; no terrain was created.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(Output))
            AssetDatabase.CreateFolder("Assets/Scenes", "SampleScene_Terrain");
        TerrainLayer grass = new TerrainLayer { diffuseTexture = green, tileSize = new Vector2(14, 14) };
        TerrainLayer soil = new TerrainLayer { diffuseTexture = brown, tileSize = new Vector2(18, 18) };
        AssetDatabase.CreateAsset(grass, Output + "/Grass.terrainlayer");
        AssetDatabase.CreateAsset(soil, Output + "/Soil.terrainlayer");

        TerrainData data = new TerrainData { heightmapResolution = 257, alphamapResolution = 256,
            baseMapResolution = 512, size = new Vector3(240, 32, 240) };
        AssetDatabase.CreateAsset(data, Output + "/Terrain.asset");
        float[,] heights = new float[257, 257];
        for (int z = 0; z < 257; z++)
        for (int x = 0; x < 257; x++)
        {
            float nx = x / 256f, nz = z / 256f;
            float edge = Mathf.SmoothStep(0, 1, Mathf.Clamp01(Mathf.Max(Mathf.Abs(nx - .5f), Mathf.Abs(nz - .5f)) * 2));
            heights[z, x] = Mathf.Clamp01(.13f + .22f * edge +
                .09f * Mathf.PerlinNoise(nx * 5 + 12, nz * 5 + 7) +
                .035f * Mathf.PerlinNoise(nx * 15 + 21, nz * 15 + 31));
        }
        data.SetHeights(0, 0, heights);
        data.terrainLayers = new[] { grass, soil };
        float[,,] weights = new float[256, 256, 2];
        for (int z = 0; z < 256; z++)
        for (int x = 0; x < 256; x++)
        {
            float n = Mathf.PerlinNoise(x / 30f + 41, z / 30f + 13);
            weights[z, x, 1] = Mathf.SmoothStep(0, .42f, Mathf.Clamp01((n - .52f) * 2.3f));
            weights[z, x, 0] = 1 - weights[z, x, 1];
        }
        data.SetAlphamaps(0, 0, weights);

        data.SetDetailResolution(256, 16);
        data.detailPrototypes = new[] { new DetailPrototype {
            prototypeTexture = grassDetail, renderMode = DetailRenderMode.GrassBillboard,
            healthyColor = new Color(.38f, .65f, .27f), dryColor = new Color(.62f, .55f, .29f),
            minWidth = .35f, maxWidth = .8f, minHeight = .45f, maxHeight = 1.1f,
            noiseSpread = .18f
        } };
        int[,] detail = new int[256, 256];
        for (int z = 0; z < 256; z++)
        for (int x = 0; x < 256; x++)
        {
            float dx = (x - 128) / 128f, dz = (z - 128) / 128f;
            detail[z, x] = dx * dx + dz * dz < .035f ? 0 :
                Mathf.PerlinNoise(x * .11f + 9, z * .11f + 17) > .43f ? 2 : 0;
        }
        data.SetDetailLayer(0, 0, 0, detail);

        GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
        terrainObject.name = "Tutorial Terrain";
        terrainObject.transform.position = new Vector3(-120, 0, -120);
        Terrain terrain = terrainObject.GetComponent<Terrain>();
        terrain.drawInstanced = true;
        terrain.detailObjectDistance = 80f;
        terrain.detailObjectDensity = .65f;
        data.treePrototypes = new[] {
            new TreePrototype { prefab = pine },
            new TreePrototype { prefab = fruit }
        };
        var trees = new List<TreeInstance>();
        AddTrees(trees, 0, 36, 11);
        AddTrees(trees, 1, 18, 22);
        data.SetTreeInstances(trees.ToArray(), true);
        GameObject vegetation = new GameObject("Tutorial Vegetation");
        vegetation.transform.SetParent(terrainObject.transform, false);
        Place(shrub, terrain, vegetation.transform, 42, 33);
        Place(rock, terrain, vegetation.transform, 26, 44);
        Place(flower, terrain, vegetation.transform, 45, 55);

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            Debug.LogError("Terrain generated, but SampleScene could not be saved. Inspect before retrying.");
        else
            Debug.Log("SampleScene terrain generated. Inspect the scene in Unity before continuing.");
    }

    private static T Require<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

    private static void AddTrees(List<TreeInstance> trees, int prototypeIndex, int count, int seed)
    {
        var random = new System.Random(seed);
        for (int i = 0; i < count; i++)
        {
            float x = 12 + (float)random.NextDouble() * 216;
            float z = 12 + (float)random.NextDouble() * 216;
            if ((x - 120) * (x - 120) + (z - 120) * (z - 120) < 24 * 24)
                continue;
            float scale = .8f + (float)random.NextDouble() * .45f;
            trees.Add(new TreeInstance {
                prototypeIndex = prototypeIndex,
                position = new Vector3(x / 240f, 0, z / 240f),
                widthScale = scale, heightScale = scale,
                rotation = (float)random.NextDouble() * Mathf.PI * 2f,
                color = Color.white, lightmapColor = Color.white
            });
        }
    }

    private static void Place(GameObject prefab, Terrain terrain, Transform parent, int count, int seed)
    {
        var random = new System.Random(seed);
        for (int i = 0; i < count; i++)
        {
            float x = 12 + (float)random.NextDouble() * 216;
            float z = 12 + (float)random.NextDouble() * 216;
            if ((x - 120) * (x - 120) + (z - 120) * (z - 120) < 24 * 24)
                continue;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, terrain.gameObject.scene);
            instance.transform.SetParent(parent, true);
            Vector3 origin = terrain.transform.position;
            instance.transform.position = new Vector3(origin.x + x,
                origin.y + terrain.terrainData.GetInterpolatedHeight(x / 240f, z / 240f), origin.z + z);
            instance.transform.rotation = Quaternion.Euler(0, (float)random.NextDouble() * 360, 0);
            instance.transform.localScale *= .8f + (float)random.NextDouble() * .45f;
        }
    }
}
