using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using AvoiderPlugin;

public static class CreateAvoiderShowcase
{
    [MenuItem("Tools/Avoider/Create Showcase Scene")]
    public static void Create()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        int layer = LayerMask.NameToLayer("Cover");
        if (layer < 0)
        {
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = settings.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                { layer = i; layers.GetArrayElementAtIndex(i).stringValue = "Cover"; settings.ApplyModifiedProperties(); break; }
        }
        if (layer < 0) { Debug.LogError("Create an available Cover layer first."); return; }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        string folder = AssetDatabase.GenerateUniqueAssetPath("Assets/AvoiderShowcase");
        AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
        Material floor = MaterialAsset(folder, "Floor", new Color(0.22f, 0.32f, 0.4f));
        Material cover = MaterialAsset(folder, "Cover", new Color(0.55f, 0.3f, 0.12f));
        Material red = MaterialAsset(folder, "Avoider", new Color(0.9f, 0.15f, 0.12f));
        Material yellow = MaterialAsset(folder, "Player", Color.yellow);
        var arena = new GameObject("Arena (baked NavMesh)");
        Box("Floor", new Vector3(0, -0.25f, 0), new Vector3(26, 0.5f, 26), floor, arena.transform, 0);
        Box("Cover A", new Vector3(-2, 1.5f, 0), new Vector3(1, 3, 7), cover, arena.transform, layer);
        Box("Cover B", new Vector3(0, 1.5f, -3), new Vector3(5, 3, 1), cover, arena.transform, layer);
        Box("Cover C", new Vector3(6, 1.5f, 4), new Vector3(1, 3, 5), cover, arena.transform, layer);
        Box("North", new Vector3(0, 1.5f, 13), new Vector3(27, 3, 1), cover, arena.transform, layer);
        Box("South", new Vector3(0, 1.5f, -13), new Vector3(27, 3, 1), cover, arena.transform, layer);
        Box("East", new Vector3(13, 1.5f, 0), new Vector3(1, 3, 27), cover, arena.transform, layer);
        Box("West", new Vector3(-13, 1.5f, 0), new Vector3(1, 3, 27), cover, arena.transform, layer);
        var surface = arena.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        if (surface.navMeshData != null)
            AssetDatabase.CreateAsset(surface.navMeshData, folder + "/ArenaNavMesh.asset");

        var player = new GameObject("Player - WASD");
        player.transform.position = new Vector3(5, 0.05f, -6);
        var controller = player.AddComponent<CharacterController>();
        controller.height = 2; controller.radius = 0.45f; controller.center = Vector3.up;
        player.AddComponent<ShowcasePlayer>();
        Visual(player.transform, yellow);
        var enemy = new GameObject("Avoider - select to inspect");
        enemy.transform.position = new Vector3(2, 0, -1);
        var agent = enemy.AddComponent<NavMeshAgent>();
        agent.height = 2; agent.radius = 0.5f; agent.stoppingDistance = 0.05f;
        agent.acceleration = 20;
        Visual(enemy.transform, red);
        var avoider = enemy.AddComponent<Avoider>();
        avoider.Avoidee = player.transform; avoider.CoverMask = 1 << layer;
        // Both showcase characters have their pivot at ground level.
        var serialized = new SerializedObject(avoider);
        serialized.FindProperty("avoideeEyeOffset").floatValue = 1.5f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();
        camera.transform.position = new Vector3(0, 25, -20);
        camera.transform.rotation = Quaternion.Euler(52, 0, 0);
        camera.orthographic = true; camera.orthographicSize = 18;
        camera.backgroundColor = new Color(0.08f, 0.1f, 0.15f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1.4f;
        light.transform.rotation = Quaternion.Euler(50, -30, 0);
        RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.5f);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), folder + "/Showcase.unity");
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = enemy;
        Debug.Log("Showcase created. Press Play; move yellow player with WASD. Enable Scene/Game Gizmos to see candidate lines.");
    }
    private static Material MaterialAsset(string folder, string name, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var material = new Material(shader) { color = color };
        AssetDatabase.CreateAsset(material, folder + "/" + name + ".mat");
        return material;
    }
    private static void Box(string name, Vector3 position, Vector3 scale, Material material, Transform parent, int layer)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name; box.layer = layer; box.transform.SetParent(parent);
        box.transform.position = position; box.transform.localScale = scale;
        box.GetComponent<Renderer>().sharedMaterial = material;
    }
    private static void Visual(Transform parent, Material material)
    {
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Visual"; body.transform.SetParent(parent, false);
        body.transform.localPosition = Vector3.up;
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.GetComponent<Renderer>().sharedMaterial = material;
        var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nose.name = "Facing marker"; nose.transform.SetParent(parent, false);
        nose.transform.localPosition = new Vector3(0, 1.5f, 0.6f);
        nose.transform.localScale = new Vector3(0.15f, 0.15f, 0.5f);
        Object.DestroyImmediate(nose.GetComponent<Collider>());
        nose.GetComponent<Renderer>().sharedMaterial = material;
    }
}
