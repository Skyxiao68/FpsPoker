using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static partial class CombatVisualIntegration
{
    [MenuItem("Tools/Combat Visuals/Render Previews")]
    static partial void RenderPreviews()
    {
        Directory.CreateDirectory(Work);
        Scene sourceScene = SceneManager.GetActiveScene();
        bool sourceWasClean = !sourceScene.isDirty;
        Scene scene = EditorSceneManager.NewPreviewScene();
        GameObject instance = null;
        try
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefabs/FPS/Player(FPS).prefab");
            instance = UnityEngine.Object.Instantiate(source);
            SceneManager.MoveGameObjectToScene(instance, scene);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var camera in instance.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            var c = instance.GetComponent<FpsCharacterController>();
            var body = instance.GetComponentInChildren<CharacterAnimationDriver>();
            var vm = instance.GetComponentInChildren<FirstPersonRifleVisual>();
            vm.gameObject.SetActive(false);
            body.ConfigureLocalPlayer(false);
            foreach (Renderer r in body.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
            var animator = body.GetComponent<Animator>();
            var lightObject = new GameObject("Preview Light", typeof(Light));
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            var light = lightObject.GetComponent<Light>(); light.type = LightType.Directional; light.intensity = 2; lightObject.transform.rotation = Quaternion.Euler(35,-30,0);
            var cameraObject = new GameObject("Preview Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var view = cameraObject.GetComponent<Camera>(); view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(.14f,.18f,.23f); view.nearClipPlane = .01f;
            view.scene = scene;
            animator.Rebind();
            Clip("Combat/Rifle/HumanM@Rifle_Aim01").SampleAnimation(animator.gameObject, 0);
            view.transform.position = new Vector3(2.6f, 1.1f, 3.6f); view.transform.LookAt(new Vector3(0, .15f, .25f)); view.fieldOfView = 38;
            Capture(view, Work + "character-aim.png");
            Clip("Combat/HumanM@Death01").SampleAnimation(animator.gameObject, 1.5f);
            Capture(view, Work + "character-death.png");
            Clip("Combat/Rifle/HumanM@Rifle_Aim01").SampleAnimation(animator.gameObject, 0);
            foreach (var r in animator.GetComponentsInChildren<Renderer>()) r.forceRenderingOff = true;
            var playerCamera = c.CameraTransform.GetComponentInChildren<Camera>(true);
            vm.gameObject.SetActive(true);
            vm.GetComponent<WradRifleGripPose>().ApplyPose();
            view.transform.SetPositionAndRotation(playerCamera.transform.position, playerCamera.transform.rotation); view.fieldOfView = playerCamera.fieldOfView;
            Capture(view, Work + "first-person.png");
            // Side view makes grip fit and backward-facing geometry obvious.
            Vector3 centre = playerCamera.transform.position + new Vector3(.1f,-.2f,.5f);
            view.transform.position = centre + new Vector3(1.5f,.3f,.3f); view.transform.LookAt(centre); view.fieldOfView = 50;
            Capture(view, Work + "hands-side.png");
        }
        finally
        {
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            EditorSceneManager.ClosePreviewScene(scene);
            if (sourceWasClean && sourceScene.isLoaded && sourceScene.isDirty) EditorSceneManager.SaveScene(sourceScene);
        }
        File.WriteAllText(Work + "preview.txt", "Rendered character aim/death and first-person grips.");
    }
    static void Capture(Camera camera, string path)
    {
        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        var texture = new Texture2D(1280,720,TextureFormat.RGB24,false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0,0,1280,720),0,0); texture.Apply(); File.WriteAllBytes(path,texture.EncodeToPNG());
        }
        finally { camera.targetTexture = null; RenderTexture.active = previous; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture); }
    }
}

