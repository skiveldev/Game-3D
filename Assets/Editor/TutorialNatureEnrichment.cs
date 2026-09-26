using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class TutorialNatureEnrichment
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string TerrainPath = "Assets/Scenes/SampleScene_Terrain/Terrain.asset";
    private const string Prefabs = "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/";
    private const string Marker = "Tutorial Nature Enrichment";
    private const string SecondMarker = "Tutorial Nature Densification II";

    private static readonly string[] PropPaths = {
        "Mushrooms/PT_Caesars_Mushroom_01.prefab",
        "Trees/PT_Pine_Tree_03_logs.prefab",
        "Trees/PT_Pine_Tree_03_stump.prefab",
        "Rocks/PT_Generic_Rock_01.prefab",
        "Rocks/PT_River_Rock_Pile_02.prefab",
        "Rocks/PT_Menhir_Rock_02.prefab",
        "Shrubs/PT_Generic_Shrub_01_green.prefab",
        "Shrubs/PT_Generic_Shrub_01_dead.prefab",
        "Flowers/PT_Poppy_02.prefab"
    };

    private static readonly Vector2[] Groves = {
        new Vector2(-78, -76), new Vector2(72, -74),
        new Vector2(-78, 71), new Vector2(76, 76),
        new Vector2(-103, 4), new Vector2(102, -7),
        new Vector2(-29, -96), new Vector2(34, 96),
        new Vector2(-55, 34), new Vector2(58, -34)
    };

    [MenuItem("Tools/Tutorial/Enrich SampleScene Nature")]
    private static void Enrich()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || !scene.isLoaded || SceneManager.sceneCount != 1 ||
            EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty)
        {
            Debug.LogError("Open only the saved, clean SampleScene in Edit Mode before enriching nature.");
            return;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == Marker || root.transform.Find(Marker))
            {
                Debug.Log("Nature enrichment already exists; no changes made.");
                return;
            }

        Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        if (terrains.Length != 1 || terrains[0].gameObject.scene != scene ||
            terrains[0].transform.position != new Vector3(-120, 0, -120))
        {
            Debug.LogError("Expected exactly one SampleScene terrain at (-120, 0, -120); no changes made.");
            return;
        }
        Terrain terrain = terrains[0];
        TerrainData data = terrain.terrainData;
        if (!data || AssetDatabase.GetAssetPath(data) != TerrainPath ||
            data.size != new Vector3(240, 32, 240) || data.treePrototypes.Length != 2 ||
            AssetDatabase.GetAssetPath(data.treePrototypes[0].prefab) != Prefabs + "Trees/PT_Pine_Tree_03_green.prefab" ||
            AssetDatabase.GetAssetPath(data.treePrototypes[1].prefab) != Prefabs + "Trees/PT_Fruit_Tree_01_apples.prefab")
        {
            Debug.LogError("Unexpected terrain asset, size, or tree prototypes; no changes made.");
            return;
        }

        GameObject[] props = new GameObject[PropPaths.Length];
        for (int i = 0; i < props.Length; i++)
        {
            props[i] = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + PropPaths[i]);
            Renderer[] renderers = props[i] ? props[i].GetComponentsInChildren<Renderer>(true) : null;
            if (!props[i] || !PrefabUtility.IsPartOfPrefabAsset(props[i]) ||
                renderers == null || renderers.Length == 0)
            {
                Debug.LogError("Missing or non-renderable prefab: " + Prefabs + PropPaths[i]);
                return;
            }
            foreach (Renderer renderer in renderers)
                foreach (Material material in renderer.sharedMaterials)
                    if (!material || !material.shader || !material.shader.isSupported)
                    {
                        Debug.LogError("Missing or unsupported material in: " + PropPaths[i]);
                        return;
                    }
        }

        // Compute the entire placement plan before touching the scene or TerrainData.
        TreeInstance[] original = data.treeInstances;
        var trees = new List<TreeInstance>(original);
        var propPlan = new List<(int kind, Vector2 position, float yaw, float scale)>();
        var random = new System.Random(13057);
        int pine = 0, fruit = 0;
        for (int grove = 0; grove < Groves.Length; grove++)
        {
            int target = grove < 4 ? 34 : grove < 8 ? 25 : 18;
            for (int attempt = 0, accepted = 0; attempt < target * 35 && accepted < target; attempt++)
            {
                float angle = (float)(random.NextDouble() * Math.PI * 2);
                float radius = Mathf.Sqrt((float)random.NextDouble()) * (grove < 4 ? 30 : 23);
                Vector2 point = Groves[grove] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (!Clear(point, 11)) continue;
                bool spaced = true;
                foreach (TreeInstance tree in trees)
                {
                    Vector2 existing = new Vector2(tree.position.x * 240 - 120, tree.position.z * 240 - 120);
                    if ((point - existing).sqrMagnitude < 6.5f * 6.5f) { spaced = false; break; }
                }
                if (!spaced) continue;
                int kind = random.NextDouble() < .13 ? 1 : 0;
                float scale = .78f + (float)random.NextDouble() * .5f;
                trees.Add(new TreeInstance {
                    prototypeIndex = kind,
                    position = new Vector3((point.x + 120) / 240, 0, (point.y + 120) / 240),
                    widthScale = scale, heightScale = scale * (.93f + (float)random.NextDouble() * .14f),
                    rotation = (float)random.NextDouble() * Mathf.PI * 2,
                    color = Color.white, lightmapColor = Color.white
                });
                if (kind == 0) pine++; else fruit++;
                accepted++;
            }
        }
        // Sparse isolated trees break up the grove outlines without closing the crossing lanes.
        for (int attempt = 0, accepted = 0; attempt < 800 && accepted < 16; attempt++)
        {
            Vector2 point = new Vector2(-106 + (float)random.NextDouble() * 212,
                -106 + (float)random.NextDouble() * 212);
            if (!Clear(point, 12)) continue;
            bool spaced = true;
            foreach (TreeInstance tree in trees)
                if ((point - new Vector2(tree.position.x * 240 - 120, tree.position.z * 240 - 120)).sqrMagnitude < 11 * 11)
                { spaced = false; break; }
            if (!spaced) continue;
            int kind = random.NextDouble() < .22 ? 1 : 0;
            float scale = .8f + (float)random.NextDouble() * .4f;
            trees.Add(new TreeInstance {
                prototypeIndex = kind, position = new Vector3((point.x + 120) / 240, 0, (point.y + 120) / 240),
                widthScale = scale, heightScale = scale, rotation = (float)random.NextDouble() * Mathf.PI * 2,
                color = Color.white, lightmapColor = Color.white
            });
            if (kind == 0) pine++; else fruit++;
            accepted++;
        }
        int[] counts = { 64, 24, 30, 46, 28, 10, 72, 28, 95 };
        for (int kind = 0; kind < counts.Length; kind++)
        for (int attempt = 0, accepted = 0; attempt < counts[kind] * 40 && accepted < counts[kind]; attempt++)
        {
            Vector2 center = Groves[random.Next(Groves.Length)];
            Vector2 point = center + new Vector2((float)(random.NextDouble() * 2 - 1),
                (float)(random.NextDouble() * 2 - 1)) * 37;
            if (!Clear(point, 8)) continue;
            bool spaced = true;
            foreach (var placed in propPlan)
                if ((point - placed.position).sqrMagnitude < 2.8f * 2.8f) { spaced = false; break; }
            if (!spaced) continue;
            propPlan.Add((kind, point, (float)random.NextDouble() * 360,
                .8f + (float)random.NextDouble() * .38f));
            accepted++;
        }
        if (trees.Count == original.Length || propPlan.Count == 0)
        {
            Debug.LogError("No safe nature placement plan could be generated; no changes made.");
            return;
        }

        Debug.Log($"Nature plan: trees {original.Length} -> {trees.Count} (+{pine} pine, +{fruit} fruit); " +
            $"props 0 -> {propPlan.Count}; grass and terrain detail unchanged.");
        GameObject container = new GameObject(Marker);
        foreach (var item in propPlan)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(props[item.kind], scene);
            instance.transform.SetParent(container.transform, true);
            instance.transform.position = new Vector3(item.position.x,
                terrain.SampleHeight(new Vector3(item.position.x, 0, item.position.y)) + terrain.transform.position.y,
                item.position.y);
            instance.transform.rotation = Quaternion.Euler(0, item.yaw, 0);
            instance.transform.localScale *= item.scale;
            // These decorative props must not obstruct the FPS controller.
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
        }
        data.SetTreeInstances(trees.ToArray(), true);
        EditorUtility.SetDirty(data);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            Debug.LogError("Nature terrain data saved but scene save failed. Inspect the open scene before retrying.");
        else
            Debug.Log($"Nature enrichment saved: trees {original.Length} -> {data.treeInstanceCount}, " +
                $"props={container.transform.childCount}. Inspect all quadrants and Play Mode in Unity.");
    }

    private static bool Clear(Vector2 point, float margin)
    {
        if (Mathf.Abs(point.x) > 108 || Mathf.Abs(point.y) > 108 || point.sqrMagnitude < 27 * 27)
            return false;
        // Preserve central east-west/north-south routes and the spawn approach.
        return Mathf.Abs(point.x) > margin && Mathf.Abs(point.y) > margin;
    }

    [MenuItem("Tools/Tutorial/Densify SampleScene Nature Again")]
    private static void DensifyAgain()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || !scene.isLoaded || SceneManager.sceneCount != 1 ||
            EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty)
        {
            Debug.LogError("Open only the saved, clean SampleScene in Edit Mode.");
            return;
        }
        GameObject first = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == SecondMarker || root.transform.Find(SecondMarker))
            {
                Debug.Log("Second nature densification already exists; no changes made.");
                return;
            }
            if (root.name == Marker) first = root;
        }
        Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        if (!first || first.transform.childCount < 397 || terrains.Length != 1 ||
            terrains[0].gameObject.scene != scene ||
            terrains[0].transform.position != new Vector3(-120, 0, -120))
        {
            Debug.LogError("Expected the first enrichment and one SampleScene terrain; no changes made.");
            return;
        }
        Terrain terrain = terrains[0];
        TerrainData data = terrain.terrainData;
        if (!data || AssetDatabase.GetAssetPath(data) != TerrainPath ||
            data.size != new Vector3(240, 32, 240) || data.treePrototypes.Length != 2 ||
            AssetDatabase.GetAssetPath(data.treePrototypes[0].prefab) != Prefabs + "Trees/PT_Pine_Tree_03_green.prefab" ||
            AssetDatabase.GetAssetPath(data.treePrototypes[1].prefab) != Prefabs + "Trees/PT_Fruit_Tree_01_apples.prefab")
        {
            Debug.LogError("Unexpected terrain asset or tree prototypes; no changes made.");
            return;
        }
        GameObject[] prefabs = new GameObject[PropPaths.Length];
        for (int i = 0; i < prefabs.Length; i++)
        {
            prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + PropPaths[i]);
            Renderer[] renderers = prefabs[i] ? prefabs[i].GetComponentsInChildren<Renderer>(true) : null;
            if (!prefabs[i] || !PrefabUtility.IsPartOfPrefabAsset(prefabs[i]) ||
                renderers == null || renderers.Length == 0)
            {
                Debug.LogError("Missing or non-renderable prefab: " + PropPaths[i]);
                return;
            }
            foreach (Renderer renderer in renderers)
                foreach (Material material in renderer.sharedMaterials)
                    if (!material || !material.shader || !material.shader.isSupported)
                    {
                        Debug.LogError("Missing or unsupported material: " + PropPaths[i]);
                        return;
                    }
        }

        TreeInstance[] original = data.treeInstances;
        if (original.Length < 365)
        {
            Debug.LogError("First tree enrichment is missing; no changes made.");
            return;
        }
        var trees = new List<TreeInstance>(original);
        var treePoints = new List<Vector2>();
        foreach (TreeInstance tree in original)
            treePoints.Add(new Vector2(tree.position.x * 240 - 120, tree.position.z * 240 - 120));
        var propPoints = new List<Vector2>();
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == Marker || root.name == "Tutorial Vegetation")
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child != root.transform && PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))
                        propPoints.Add(new Vector2(child.position.x, child.position.z));
        var plan = new List<(int kind, Vector2 point, float yaw, float scale)>();
        var random = new System.Random(240913);
        int treeTarget = original.Length * 2;
        int propTarget = first.transform.childCount * 2;
        int pine = 0, fruit = 0;
        // Wider overlapping groves cover each quadrant; a uniform share softens their edges.
        for (int attempt = 0; attempt < treeTarget * 180 && trees.Count - original.Length < treeTarget; attempt++)
        {
            Vector2 point;
            if (random.NextDouble() < .78)
            {
                Vector2 center = Groves[random.Next(Groves.Length)];
                float angle = (float)(random.NextDouble() * Math.PI * 2);
                float radius = Mathf.Sqrt((float)random.NextDouble()) * 38;
                point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            else point = new Vector2((float)(random.NextDouble() * 212 - 106),
                (float)(random.NextDouble() * 212 - 106));
            if (!Clear(point, 10) || !FarEnough(point, treePoints, 4.2f) ||
                !FarEnough(point, propPoints, 2.2f)) continue;
            int kind = random.NextDouble() < .16 ? 1 : 0;
            float scale = .75f + (float)random.NextDouble() * .55f;
            trees.Add(new TreeInstance {
                prototypeIndex = kind,
                position = new Vector3((point.x + 120) / 240, 0, (point.y + 120) / 240),
                widthScale = scale, heightScale = scale * (.93f + (float)random.NextDouble() * .14f),
                rotation = (float)random.NextDouble() * Mathf.PI * 2,
                color = Color.white, lightmapColor = Color.white
            });
            treePoints.Add(point);
            if (kind == 0) pine++; else fruit++;
        }
        int[] weights = { 135, 46, 55, 92, 54, 17, 145, 55, 195 };
        int weightTotal = 0;
        foreach (int weight in weights) weightTotal += weight;
        int[] kinds = new int[weights.Length];
        for (int attempt = 0; attempt < propTarget * 160 && plan.Count < propTarget; attempt++)
        {
            int draw = random.Next(weightTotal);
            int kind = 0;
            while (draw >= weights[kind]) draw -= weights[kind++];
            Vector2 center = Groves[random.Next(Groves.Length)];
            float angle = (float)(random.NextDouble() * Math.PI * 2);
            float radius = Mathf.Sqrt((float)random.NextDouble()) * 42;
            Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            if (!Clear(point, 8) || !FarEnough(point, propPoints, 1.7f) ||
                !FarEnough(point, treePoints, 1.9f)) continue;
            plan.Add((kind, point, (float)random.NextDouble() * 360,
                .75f + (float)random.NextDouble() * .5f));
            propPoints.Add(point);
            kinds[kind]++;
        }
        if (trees.Count < Mathf.CeilToInt(original.Length * 2.7f) ||
            first.transform.childCount + plan.Count < Mathf.CeilToInt(first.transform.childCount * 2.7f))
        {
            Debug.LogError($"Placement below 2.7x threshold: trees {original.Length}->{trees.Count}, " +
                $"enriched props {first.transform.childCount}->{first.transform.childCount + plan.Count}; no changes made.");
            return;
        }
        GameObject container = new GameObject(SecondMarker);
        foreach (var item in plan)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[item.kind], scene);
            instance.transform.SetParent(container.transform, true);
            instance.transform.position = new Vector3(item.point.x,
                terrain.SampleHeight(new Vector3(item.point.x, 0, item.point.y)) + terrain.transform.position.y,
                item.point.y);
            instance.transform.rotation = Quaternion.Euler(0, item.yaw, 0);
            instance.transform.localScale *= item.scale;
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
        }
        data.SetTreeInstances(trees.ToArray(), true);
        EditorUtility.SetDirty(data);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            Debug.LogError("Terrain saved but scene save failed; inspect the open scene before retrying.");
            return;
        }
        string byType = "";
        for (int i = 0; i < kinds.Length; i++) byType += $" {PropPaths[i]}={kinds[i]};";
        Debug.Log($"Second nature pass saved: trees {original.Length}->{data.treeInstanceCount} " +
            $"(+{pine} pine, +{fruit} fruit); enriched props {first.transform.childCount}->" +
            $"{first.transform.childCount + container.transform.childCount}; added props by type:{byType} " +
            "Terrain details, heights and layers unchanged. Inspect all quadrants and Play Mode.");
    }

    private static bool FarEnough(Vector2 point, List<Vector2> others, float distance)
    {
        float minimum = distance * distance;
        foreach (Vector2 other in others)
            if ((point - other).sqrMagnitude < minimum) return false;
        return true;
    }
}
