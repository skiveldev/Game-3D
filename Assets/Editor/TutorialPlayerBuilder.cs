using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class TutorialPlayerBuilder
{
    [MenuItem("Tools/Tutorial/Add SampleScene Player")]
    private static void Build()
    {
        const string scenePath = "Assets/Scenes/SampleScene.unity";
        const string prefabPath = "Assets/ModularFirstPersonController/FirstPersonController/FirstPersonController.prefab";
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != scenePath || !scene.isLoaded || SceneManager.sceneCount != 1 ||
            EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty)
        {
            Debug.LogError("Open only the clean SampleScene in Edit Mode before adding the player.");
            return;
        }
        Terrain terrain = Object.FindFirstObjectByType<Terrain>();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        GameObject original = GameObject.Find("Main Camera");
        Camera originalCamera = original ? original.GetComponent<Camera>() : null;
        Camera prefabCamera = prefab ? prefab.GetComponentInChildren<Camera>(true) : null;
        if (!terrain || !terrain.terrainData || !prefabCamera || !originalCamera ||
            !original.activeSelf || GameObject.Find("FirstPersonController") ||
            prefab.GetComponent<FirstPersonController>() == null ||
            Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None).Length != 0)
        {
            Debug.LogError("Expected terrain, original camera and unique imported FPS prefab are required; no changes made.");
            return;
        }

        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Vector3 origin = terrain.transform.position;
        Vector3 spawn = origin + new Vector3(terrain.terrainData.size.x * .5f,
            0, terrain.terrainData.size.z * .5f);
        spawn.y = terrain.SampleHeight(spawn) + origin.y + 2f;
        player.transform.position = spawn;
        Camera camera = player.GetComponentInChildren<Camera>(true);
        UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = true;
        original.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, scenePath))
            Debug.LogError("Player added, but SampleScene could not be saved; inspect before retrying.");
    }
}
