using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class CombatVisualIntegration
{
    static void ValidateRuntimeRifleVisuals(FpsCharacterController player, FpsCharacterController target)
    {
        var report = new StringBuilder();
        Directory.CreateDirectory(Work);
        try
        {
            var driver = target.GetComponentInChildren<CharacterAnimationDriver>();
            Animator animator = driver.GetComponent<Animator>();
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform rifle = hand.Find("World Rifle");
            float forward = Vector3.Dot(rifle.forward, target.transform.forward);
            float up = Vector3.Dot(rifle.up, target.transform.up);
            report.AppendLine("Enemy barrel=" + forward + "; upright=" + up + "; localRotation=" +
                rifle.localEulerAngles + "; handRotation=" + hand.rotation.eulerAngles + "; layerWeight=" +
                animator.GetLayerWeight(1) + "; aimForward=" + target.CameraTransform.forward);
            Check(driver.enabled && forward > .8f && up > .8f,
                "live enemy aim keeps the hand-attached rifle forward and upright");
            ValidateLiveRifleRenderer(rifle, report, "enemy");

            Transform support = rifle.Find("Support Hand Grip");
            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            float supportDistance = Vector3.Distance(support.position, leftHand.position);
            report.AppendLine("Enemy support hand distance=" + supportDistance);
            Check(supportDistance < .2f, "live enemy support hand follows the rifle grip");

            var viewmodel = player.GetComponentInChildren<FirstPersonRifleVisual>();
            Transform firstPersonRifle = viewmodel.transform.Find("First Person Rifle");
            ValidateLiveRifleRenderer(firstPersonRifle, report, "first-person");
            Camera camera = player.CameraTransform.GetComponentInChildren<Camera>();
            Renderer renderer = firstPersonRifle.GetComponentInChildren<MeshRenderer>();
            Vector3 viewport = camera.WorldToViewportPoint(renderer.bounds.center);
            report.AppendLine("First-person rifle viewport=" + viewport);
            Check(GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), renderer.bounds),
                "live first-person rifle intersects the gameplay camera view");
        }
        finally
        {
            File.WriteAllText(Work + "runtime-rifle-alignment.txt", report.ToString());
        }
    }

    static void ValidateLiveRifleRenderer(Transform rifle, StringBuilder report, string name)
    {
        Renderer renderer = rifle.GetComponentInChildren<MeshRenderer>(true);
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        bool visible = renderer.enabled && renderer.gameObject.activeInHierarchy &&
            !renderer.forceRenderingOff && renderer.shadowCastingMode != ShadowCastingMode.ShadowsOnly &&
            filter.sharedMesh != null;
        report.AppendLine(name + " renderer: enabled=" + renderer.enabled + "; active=" +
            renderer.gameObject.activeInHierarchy + "; forceRenderingOff=" + renderer.forceRenderingOff +
            "; shadows=" + renderer.shadowCastingMode + "; mesh=" + filter.sharedMesh +
            "; bounds=" + renderer.bounds);
        Check(visible, "live " + name + " rifle has an enabled visible mesh renderer");
    }
}
