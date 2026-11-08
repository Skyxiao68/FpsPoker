using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static partial class CombatVisualIntegration
{
    static void PrepareShotActors(FpsCharacterController player, FpsCharacterController target)
    {
        foreach (FpsCharacterController fighter in new[] { player, target })
        {
            Health health = fighter.GetComponent<Health>();
            health.SetMaxHealth(1000);
            health.ResetHealth();
            var ai = fighter.GetComponent<FPSAIEngine>();
            if (ai != null) ai.enabled = false;
            var input = fighter.GetComponent<FpsPlayerInputHandler>();
            if (input != null) input.enabled = false;
            fighter.SetFireInput(false);
            fighter.SetLookInput(Vector2.zero);
            fighter.SetMoveInput(Vector2.zero);
            fighter.SetSprint(false);
            fighter.Stop();
            var capsule = fighter.GetComponent<CharacterController>();
            capsule.enabled = false;
            fighter.transform.SetPositionAndRotation(new Vector3(30f, 0f, fighter == player ? 0f : 5f),
                fighter == player ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f));
            capsule.enabled = true;
            fighter.enabled = true;
            var driver = fighter.GetComponentInChildren<CharacterAnimationDriver>();
            driver.enabled = false;
            EvaluateRifleAimPose(driver.GetComponent<Animator>());
            if (fighter == player)
                foreach (Renderer renderer in driver.GetComponentsInChildren<Renderer>()) renderer.forceRenderingOff = true;
        }
    }

    static void ValidateShotFeatures(FpsCharacterController player, FpsCharacterController target)
    {
        Health playerHealth = player.GetComponent<Health>();
        Health targetHealth = target.GetComponent<Health>();
        CombatHitbox[] regions = target.GetComponentsInChildren<CombatHitbox>();
        CombatHitbox head = regions.Single(region => region.IsHeadshot);
        CombatHitbox torso = regions.Single(region => region.GetComponent<BoxCollider>() != null);
        var headSphere = head.GetComponent<SphereCollider>();
        var torsoBox = torso.GetComponent<BoxCollider>();
        Vector3 headPoint = headSphere.transform.TransformPoint(headSphere.center);
        Vector3 torsoPoint = torsoBox.transform.TransformPoint(torsoBox.center);
        Check(regions.Length == 10 && regions.All(region => region.Owner == targetHealth &&
            region.GetComponent<Collider>().isTrigger), "animated character has one owned head and nine owned body hitboxes");

        Weapon weapon = player.EquippedWeapon;
        WeaponSettings originalSettings = weapon.Settings;
        WeaponSettings settings = UnityEngine.Object.Instantiate(originalSettings);
        Check(Mathf.Approximately(settings.headshotMultiplier, 1.5f), "equipped weapon defaults to a 1.5x headshot multiplier");
        settings.damage = 10;
        settings.fireRate = 0f;
        settings.magazineSize = 30;
        settings.range = 100f;
        settings.recoilRandomness = 0f;
        settings.verticalRecoilPattern = Array.Empty<float>();
        settings.horizontalRecoilPattern = Array.Empty<float>();
        settings.impactDecal = null;
        settings.headshotMultiplier = 1.5f;
        weapon.SetSettings(settings);
        weapon.SetCombatLocked(false);
        var confirmations = new List<bool>();
        Action<bool> observeHit = isHead => confirmations.Add(isHead);
        weapon.OnHitConfirmed += observeHit;
        foreach (FPS_HUD existing in player.GetComponents<FPS_HUD>()) existing.enabled = false;

        Camera camera = player.CameraTransform.GetComponentInChildren<Camera>();
        RenderTexture previousTarget = camera.targetTexture;
        var renderTarget = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = renderTarget;
        var canvasObject = new GameObject("Shot Validation HUD", typeof(RectTransform), typeof(Canvas));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = Mathf.Max(.5f, camera.nearClipPlane + .1f);
        var textObject = new GameObject("Health Validation", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(canvas.transform, false);
        TextMeshProUGUI healthText = textObject.GetComponent<TextMeshProUGUI>();
        healthText.font = TMP_Settings.defaultFontAsset;
        healthText.color = Color.white;
        healthText.alignment = TextAlignmentOptions.Left;
        RectTransform healthRect = healthText.rectTransform;
        healthRect.anchorMin = healthRect.anchorMax = healthRect.pivot = new Vector2(.5f, .5f);
        healthRect.anchoredPosition = new Vector2(721.2f, -437.28f);
        healthRect.sizeDelta = new Vector2(200f, 50f);
        var hud = player.gameObject.AddComponent<FPS_HUD>();
        hud.enabled = false;
        hud.health = playerHealth;
        hud.healthText = healthText;
        hud.enabled = true;
        Canvas.ForceUpdateCanvases();
        Transform marker = canvas.transform.Find("Hit Marker (FPS)");
        TextMeshProUGUI ammo = canvas.transform.Find("Ammo (FPS)").GetComponent<TextMeshProUGUI>();

        Action<Vector3> fireAt = point =>
        {
            weapon.SetSettings(settings);
            weapon.ResetRecoil();
            player.ResetRecoil();
            player.CameraTransform.LookAt(point);
            Check(weapon.CanFire, "test shot passes real weapon timing and ammunition guards");
            weapon.Fire();
        };

        GameObject selfObject = null;
        GameObject wall = null;
        try
        {
            int before = targetHealth.CurrentHealth;
            fireAt(torsoPoint);
            Check(before - targetHealth.CurrentHealth == 10 && confirmations.Count == 1 && !confirmations[0],
                "body raycast deals base damage and confirms exactly one body hit");
            CheckHitMarkerColor(marker, Color.white, "body shot shows a white hitmarker");
            CaptureShotHud(camera, renderTarget, Work + "hitmarker-body.png");

            before = targetHealth.CurrentHealth;
            fireAt(headPoint);
            Check(before - targetHealth.CurrentHealth == 15 && confirmations.Count == 2 && confirmations[1],
                "head raycast applies default 1.5x damage and confirms exactly one headshot");
            CheckHitMarkerColor(marker, Color.red, "headshot shows a red hitmarker");
            CaptureShotHud(camera, renderTarget, Work + "hitmarker-head.png");

            settings.headshotMultiplier = 2f;
            before = targetHealth.CurrentHealth;
            fireAt(headPoint);
            Check(before - targetHealth.CurrentHealth == 20 && confirmations.Count == 3 && confirmations[2],
                "headshot multiplier remains configurable through weapon settings");

            selfObject = new GameObject("Owned Hitbox Validation", typeof(SphereCollider), typeof(CombatHitbox));
            selfObject.transform.SetParent(player.transform, false);
            selfObject.transform.position = Vector3.Lerp(player.CameraTransform.position, torsoPoint, .2f);
            selfObject.GetComponent<SphereCollider>().radius = .2f;
            selfObject.GetComponent<CombatHitbox>().Configure(playerHealth, true);
            before = targetHealth.CurrentHealth;
            fireAt(torsoPoint);
            Check(playerHealth.CurrentHealth == 1000 && before - targetHealth.CurrentHealth == 10 &&
                confirmations.Count == 4 && !confirmations[3], "owned hitboxes are ignored while an enemy behind them takes body damage");
            CheckHitMarkerColor(marker, Color.white, "body hit returns a red hitmarker to white");
            selfObject.SetActive(false);

            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Shot Blocking Validation";
            wall.transform.position = Vector3.Lerp(player.CameraTransform.position, torsoPoint, .5f);
            wall.transform.localScale = new Vector3(1f, 2f, .3f);
            marker.gameObject.SetActive(false);
            before = targetHealth.CurrentHealth;
            fireAt(torsoPoint);
            Check(targetHealth.CurrentHealth == before && confirmations.Count == 4 && !marker.gameObject.activeSelf,
                "solid wall blocks enemy damage and produces no hitmarker");
            wall.SetActive(false);

            settings.damage = 0;
            fireAt(headPoint);
            Check(targetHealth.CurrentHealth == before && confirmations.Count == 4 && !marker.gameObject.activeSelf,
                "zero damage produces no headshot confirmation or hitmarker");
            settings.damage = 10;
            targetHealth.TakeDamage(int.MaxValue);
            fireAt(headPoint);
            Check(targetHealth.CurrentHealth == 0 && confirmations.Count == 4 && !marker.gameObject.activeSelf,
                "an already dead target produces no damage confirmation or hitmarker");

            Canvas.ForceUpdateCanvases();
            Check(healthText.fontSize == 42f && ammo.fontSize == 34f, "health and ammo labels use larger font sizes");
            CheckHudLabelFits(healthText, canvas, "larger health label fits the 1920x1080 HUD");
            ammo.text = "Ammo 30/30  Reloading";
            CheckHudLabelFits(ammo, canvas, "larger ammo label fits the 1920x1080 HUD");
            Bounds healthBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas.transform, healthRect);
            Bounds ammoBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas.transform, ammo.rectTransform);
            Check(ammoBounds.max.y < healthBounds.min.y, "larger health and ammo labels do not overlap");
        }
        finally
        {
            weapon.OnHitConfirmed -= observeHit;
            weapon.SetSettings(originalSettings);
            hud.enabled = false;
            UnityEngine.Object.Destroy(hud);
            UnityEngine.Object.Destroy(canvasObject);
            if (selfObject != null) UnityEngine.Object.Destroy(selfObject);
            if (wall != null) UnityEngine.Object.Destroy(wall);
            camera.targetTexture = previousTarget;
            renderTarget.Release();
            UnityEngine.Object.Destroy(renderTarget);
            UnityEngine.Object.Destroy(settings);
        }
    }

    static void CheckHitMarkerColor(Transform marker, Color expected, string description)
    {
        Image[] arms = marker.GetComponentsInChildren<Image>(true);
        Check(marker.gameObject.activeSelf && arms.Length == 4 && arms.All(arm => arm.color == expected), description);
    }

    static void CheckHudLabelFits(TextMeshProUGUI label, Canvas canvas, string description)
    {
        label.ForceMeshUpdate();
        var corners = new Vector3[4];
        label.rectTransform.GetWorldCorners(corners);
        Rect canvasRect = ((RectTransform)canvas.transform).rect;
        bool contained = corners.All(corner => canvasRect.Contains(canvas.transform.InverseTransformPoint(corner)));
        Check(contained && label.preferredWidth <= label.rectTransform.rect.width &&
            label.preferredHeight <= label.rectTransform.rect.height, description);
    }

    static void CaptureShotHud(Camera camera, RenderTexture renderTarget, string path)
    {
        Canvas.ForceUpdateCanvases();
        RenderTexture previous = RenderTexture.active;
        var texture = new Texture2D(renderTarget.width, renderTarget.height, TextureFormat.RGB24, false);
        try
        {
            camera.Render();
            RenderTexture.active = renderTarget;
            texture.ReadPixels(new Rect(0, 0, renderTarget.width, renderTarget.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            UnityEngine.Object.Destroy(texture);
        }
    }
}
