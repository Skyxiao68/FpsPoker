using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class CombatVisualIntegration
{
    const string Village = "Assets/Assets/Asset Packs/PolyRonin/Wild West Village/Prefabs/";

    // The prototype arena is one connected ProBuilder mesh, including all its cover.
    // Retain that collision mesh and give its exact surface a separate visual skin.
    // Prefab decoration rests on solid cover or wall tops, never in walking lanes.
    public static void InstallEnvironment(Scene scene)
    {
        GameObject previous = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Western Environment Visuals");
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous);
        var plane = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Plane");
        if (plane == null || plane.GetComponent<MeshFilter>() == null || plane.GetComponent<MeshCollider>() == null)
            throw new InvalidOperationException("FPS arena collision mesh was not found; environment dressing was not installed.");
        var original = plane.GetComponent<MeshRenderer>();
        var source = plane.GetComponent<MeshFilter>().sharedMesh;
        var visualRoot = new GameObject("Western Environment Visuals");
        SceneManager.MoveGameObjectToScene(visualRoot, scene);
        var report = new StringBuilder("Western arena dressing installed. Original Plane MeshCollider and NavMesh geometry retained.\n");

        string materialFolder = Output + "/Materials/Environment";
        Directory.CreateDirectory(materialFolder);
        AssetDatabase.Refresh();
        Material earth = EnvironmentMaterial(materialFolder, "Packed Earth", new Color(.54f, .37f, .20f));
        Material cliff = EnvironmentMaterial(materialFolder, "Desert Sandstone", new Color(.58f, .33f, .19f));
        Material cover = EnvironmentMaterial(materialFolder, "Cover Sandstone", new Color(.43f, .25f, .15f));
        Material timber = EnvironmentMaterial(materialFolder, "Gate Timber", new Color(.25f, .13f, .065f));

        var skin = new GameObject("Arena Surface - Collision Aligned");
        skin.transform.SetParent(visualRoot.transform, false);
        skin.transform.SetPositionAndRotation(plane.transform.position, plane.transform.rotation);
        skin.transform.localScale = plane.transform.lossyScale;
        Mesh mesh = UnityEngine.Object.Instantiate(source);
        mesh.name = "Western Arena Surface";
        var groups = new[] { new System.Collections.Generic.List<int>(), new System.Collections.Generic.List<int>(), new System.Collections.Generic.List<int>() };
        var vertices = mesh.vertices;
        var triangles = mesh.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 a = plane.transform.TransformPoint(vertices[triangles[i]]);
            Vector3 b = plane.transform.TransformPoint(vertices[triangles[i+1]]);
            Vector3 c = plane.transform.TransformPoint(vertices[triangles[i+2]]);
            Vector3 center = (a + b + c) / 3;
            Vector3 normal = Vector3.Cross(b-a, c-a).normalized;
            int group = normal.y > .8f && center.y < -.95f ? 0 : center.y > 5 || center.x < -21 || center.x > 19 || center.z < -22 || center.z > 22 ? 1 : 2;
            groups[group].Add(triangles[i]); groups[group].Add(triangles[i+1]); groups[group].Add(triangles[i+2]);
        }
        mesh.subMeshCount = groups.Length;
        for (int i = 0; i < groups.Length; i++) mesh.SetTriangles(groups[i], i);
        string meshPath = Output + "/WesternArenaSurface.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (existing == null) { AssetDatabase.CreateAsset(mesh, meshPath); existing = mesh; }
        else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); }
        skin.AddComponent<MeshFilter>().sharedMesh = existing;
        skin.AddComponent<MeshRenderer>().sharedMaterials = new[] { earth, cliff, cover };
        original.enabled = false; // Exact same triangles are rendered above, with the original collider untouched.
        var barrier = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "barrier");
        if (barrier != null && barrier.GetComponent<MeshRenderer>() != null) barrier.GetComponent<MeshRenderer>().sharedMaterial = timber;

        // Small scenery occupies only the roofs of already solid, non-walkable cover.
        EnvironmentProp(visualRoot.transform, "Barrel", new Vector3(-18.0f, .627f, -2.4f), 0, 1.15f);
        EnvironmentProp(visualRoot.transform, "Barrel", new Vector3(-18.0f, .627f, -.9f), 15, 1.15f);
        EnvironmentProp(visualRoot.transform, "Box", new Vector3(-18.0f, .627f, 1.0f), 0, 1.35f);
        EnvironmentProp(visualRoot.transform, "Crate1", new Vector3(-18.0f, .627f, 2.7f), 90, 1.6f);
        EnvironmentProp(visualRoot.transform, "Box", new Vector3(15.55f, .627f, -2.3f), 0, 1.3f);
        EnvironmentProp(visualRoot.transform, "Barrel", new Vector3(15.55f, .627f, 0), 0, 1.2f);
        EnvironmentProp(visualRoot.transform, "Barrel", new Vector3(15.55f, .627f, 2.0f), 0, 1.2f);
        foreach (float z in new[] { -17.72f, 17.72f })
        {
            EnvironmentProp(visualRoot.transform, "Barrel", new Vector3(-3.4f, .627f, z), 0, 1.15f);
            EnvironmentProp(visualRoot.transform, "Box", new Vector3(-1.7f, .627f, z), 0, 1.4f);
            EnvironmentProp(visualRoot.transform, "Crate1", new Vector3(.5f, .627f, z), 0, 1.6f);
        }
        foreach (float x in new[] { -6.0f, 3.65f })
        {
            EnvironmentProp(visualRoot.transform, "Barrel", new Vector3(x, 1.657f, -1.2f), 0, 1);
            EnvironmentProp(visualRoot.transform, "Box", new Vector3(x, 1.657f, .8f), 0, 1.1f);
        }

        // Four cliff outposts sit inside the existing tall solid cover footprints.
        EnvironmentProp(visualRoot.transform, "Building2", new Vector3(-14.98f, 12.026f, -14.63f), 0, .83f);
        EnvironmentProp(visualRoot.transform, "Building5", new Vector3(-14.98f, 12.026f, 14.56f), 180, .83f);
        EnvironmentProp(visualRoot.transform, "Building4", new Vector3(12.62f, 12.026f, -14.63f), 0, .76f);
        EnvironmentProp(visualRoot.transform, "Building3", new Vector3(12.62f, 12.026f, 14.56f), 180, .76f);
        foreach (float x in new[] { -17.9f, 15.7f })
            foreach (float z in new[] { -17.5f, 17.5f })
                EnvironmentProp(visualRoot.transform, "Cactus1", new Vector3(x, 12.026f, z), 35, 1.5f);

        // Break up the retaining-wall skyline with genuine rock and cactus meshes.
        for (int i = 0; i < 6; i++)
        {
            float z = -20 + i * 8;
            EnvironmentProp(visualRoot.transform, "Rock" + (i % 3 + 1), new Vector3(-23.85f, 11.91f, z), i*63, 3.0f);
            EnvironmentProp(visualRoot.transform, "Rock" + ((i+1) % 3 + 1), new Vector3(21.45f, 11.91f, z), i*87, 3.0f);
        }
        for (int i = 0; i < 5; i++)
        {
            float x = -17 + i * 8;
            EnvironmentProp(visualRoot.transform, "Rock" + (i % 3 + 1), new Vector3(x, 11.91f, -24.0f), i*71, 3.0f);
            EnvironmentProp(visualRoot.transform, "Rock" + ((i+1) % 3 + 1), new Vector3(x, 11.91f, 23.95f), i*53, 3.0f);
        }
        foreach (var r in visualRoot.GetComponentsInChildren<Renderer>()) r.gameObject.isStatic = true;
        report.AppendLine("Source pack: PolyRonin Wild West Village. Props=" + (visualRoot.transform.childCount - 1));
        report.AppendLine("Covers, walkable floor, perimeter and central gate retain original collision silhouettes; no props placed in walking lanes.");
        report.AppendLine("Surface skin has " + triangles.Length / 3 + " original triangles. Rendering materials are earth and two sandstone tones.");
        report.AppendLine("Limit: this is a safe environment dressing pass. Full bespoke buildings/interiors require redesigning the prototype collision layout and rebaking navigation.");
        File.WriteAllText(Work + "environment-installed.txt", report.ToString());
        CaptureEnvironmentPreview(scene);
    }

    static void CaptureEnvironmentPreview(Scene scene)
    {
        var cameraObject = new GameObject("Temporary Environment Preview Camera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var camera = cameraObject.GetComponent<Camera>();
        camera.scene = scene;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.59f, .72f, .81f);
        camera.farClipPlane = 300;
        camera.fieldOfView = 60;
        var gate = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "barrier");
        var gateRenderer = gate == null ? null : gate.GetComponent<MeshRenderer>();
        bool oldGateVisibility = gateRenderer != null && gateRenderer.forceRenderingOff;
        try
        {
            // The temporary pre-round gate is hidden only for these static inspection images.
            if (gateRenderer != null) gateRenderer.forceRenderingOff = true;
            camera.transform.position = new Vector3(0, 38, -47);
            camera.transform.LookAt(new Vector3(-1.18f, 4, 0));
            Capture(camera, Work + "environment-overview.png");
            camera.transform.position = new Vector3(-1.18f, .65f, -20);
            camera.transform.LookAt(new Vector3(-1.18f, 3.5f, 12));
            Capture(camera, Work + "environment-player-height.png");
        }
        finally
        {
            if (gateRenderer != null) gateRenderer.forceRenderingOff = oldGateVisibility;
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    static Material EnvironmentMaterial(string folder, string name, Color color)
    {
        string path = folder + "/" + name.Replace(" ", "") + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = name;
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", .05f);
        material.SetFloat("_Metallic", 0);
        EditorUtility.SetDirty(material);
        return material;
    }

    static GameObject EnvironmentProp(Transform parent, string name, Vector3 bottomCenter, float yaw, float scale)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Village + name + ".prefab");
        if (source == null) throw new InvalidOperationException("Missing Western environment prefab: " + name);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
        instance.name = name + " - Western Scenery";
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0,yaw,0));
        instance.transform.localScale = Vector3.one * scale;
        Bounds bounds = EnvironmentBounds(instance);
        instance.transform.position += bottomCenter - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        // Shipped village prefabs have no colliders. Keep dressing from altering gameplay.
        foreach (var collider in instance.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
        return instance;
    }

    static Bounds EnvironmentBounds(GameObject instance)
    {
        var renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("Missing environment mesh: " + instance.name);
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }
}

