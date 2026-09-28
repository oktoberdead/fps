using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Copies the ENTIRE user-updated SampleScene, including ColtRoot, its two FBX
// meshes, slide/hammer/trigger, sounds, ammo and tuned TacticalGun settings.
// Never rebuild the final pistol from m1911-3 alone or alter SampleScene.
public static class WeaponComboBuilder
{
    [MenuItem("Tools/Weapon Feel V2/Create integrated gun + arms scene")]
    public static void Create()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string modelPath = FindAsset("/Models/PunkM.fbx");
        string samplePath = FindAsset("/Scenes/SampleScene.unity");
        string farPath = FindAsset("/tests/PunkM_RigSandbox_LongADS.unity");
        string shortPath = FindAsset("/tests/PunkM_RigSandbox_ShortADS.unity");
        string hipPath = FindAsset("/tests/PunkM_RigSandbox_Hip_Draft.unity");
        if (modelPath == null || samplePath == null || farPath == null || shortPath == null || hipPath == null)
        {
            EditorUtility.DisplayDialog("Weapon combo", "Missing PunkM FBX, updated SampleScene or a saved tests pose. " +
                "Copy Models, Scenes, Experiments and tests into the same Assets root.", "OK");
            return;
        }
        string root = samplePath.Substring(0, samplePath.Length - "/Scenes/SampleScene.unity".Length);
        string outputFolder = root + "/Experiments/WeaponCombo";
        if (!AssetDatabase.IsValidFolder(outputFolder))
        {
            EditorUtility.DisplayDialog("Weapon combo", "Missing folder: " + outputFolder, "OK");
            return;
        }

        // Verify the actual scene assembly BEFORE making the copy.
        Scene source = EditorSceneManager.OpenScene(samplePath, OpenSceneMode.Single);
        TacticalGun sourceGun = Find<TacticalGun>(source);
        if (sourceGun == null || sourceGun.slide == null || sourceGun.trigger == null ||
            sourceGun.hammer == null || sourceGun.firePoint == null ||
            sourceGun.transform.Find("m1911-2") == null ||
            !HasMeshFrom(sourceGun, "/Models/colt1911/m1911-3.fbx") ||
            !HasMeshFrom(sourceGun, "/Models/colt1911/m1911-3-bodyandtrigger.fbx"))
        {
            EditorUtility.DisplayDialog("Weapon combo", "SampleScene does not contain the expected complete " +
                "ColtRoot assembly (frame/trigger FBX + slide FBX and TacticalGun parts). Nothing changed.", "OK");
            return;
        }
        string result = AssetDatabase.GenerateUniqueAssetPath(outputFolder + "/WeaponCombo.unity");
        if (!EditorSceneManager.SaveScene(source, result, true))
        {
            Debug.LogError("[Weapon combo] Could not copy SampleScene: " + result);
            return;
        }
        Scene scene = EditorSceneManager.OpenScene(result, OpenSceneMode.Single);
        TacticalGun gun = Find<TacticalGun>(scene);
        SimpleFPSController fps = Find<SimpleFPSController>(scene);
        Camera camera = fps != null ? fps.playerCamera : null;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (gun == null || fps == null || camera == null || prefab == null)
        {
            Debug.LogError("[Weapon combo] Missing gun/controller/camera/model in copy: " + result);
            return;
        }

        WeaponHandCombo.Pose far = ReadPose(farPath, out Vector3 eyeOffset,
            out Vector3 frameOffset, out Quaternion frameRotation);
        WeaponHandCombo.Pose near = ReadPose(shortPath, out _, out _, out _);
        WeaponHandCombo.Pose hip = ReadPose(hipPath, out _, out _, out _);

        GameObject model = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (model == null) { Debug.LogError("[Weapon combo] Could not instantiate PunkM."); return; }
        model.name = "PunkM weapon-hand rig (combo copy only)";
        model.transform.SetParent(fps.transform, false);
        // Test scenes express the eye relative to character origin. SampleScene's
        // player and camera are already positioned; align the FBX to that camera.
        model.transform.localPosition = camera.transform.localPosition - eyeOffset;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;
        foreach (Animator animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        Transform[] bones = model.GetComponentsInChildren<Transform>(true);
        Transform Bone(string name) { return bones.FirstOrDefault(t => t.name == name); }
        string[] names = { "Chest", "Head", "Punk_Head", "UpperArm.R", "LowerArm.R", "Wrist.R",
                           "UpperArm.L", "LowerArm.L", "Wrist.L" };
        if (names.Any(name => Bone(name) == null))
        {
            Debug.LogError("[Weapon combo] Imported PunkM is missing exposed arm/head bones. " +
                "Disable Optimize Game Objects on Models/PunkM.fbx and regenerate this copy.");
            return;
        }
        Transform head = Bone("Punk_Head");
        head.gameObject.SetActive(true);
        head.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        camera.cullingMask &= ~(1 << head.gameObject.layer);
        // Preserve the user's authored view framing for this experiment only.
        fps.baseFOV = 88f;
        fps.walkFOV = 88f;
        fps.sprintFOV = 88f;
        camera.fieldOfView = 88f;

        // Align the copied assembled gun's PARENT to the far test stance first.
        // ColtRoot and all its user-tuned mechanical settings remain intact.
        Transform weaponAnchor = gun.transform.parent;
        weaponAnchor.localPosition = far.weaponPosition;
        weaponAnchor.localRotation = far.weaponRotation;

        // The calibration frame follows ColtRoot AFTER TacticalGun animates it;
        // moving it in Scene View adjusts BOTH hands relative to the real meshes.
        Transform frame = new GameObject("Grip calibration - move this to fit assembled pistol").transform;
        frame.SetParent(gun.transform, false);
        frame.SetPositionAndRotation(camera.transform.TransformPoint(frameOffset),
            camera.transform.rotation * frameRotation);

        Transform targetRoot = new GameObject("Weapon-hand IK goals (runtime driven)").transform;
        SceneManager.MoveGameObjectToScene(targetRoot.gameObject, scene);
        targetRoot.SetParent(fps.transform, false);
        TwoHandPosePreview rig = model.AddComponent<TwoHandPosePreview>();
        rig.right.upperArm = Bone("UpperArm.R");
        rig.right.lowerArm = Bone("LowerArm.R");
        rig.right.wrist = Bone("Wrist.R");
        rig.right.target = NewTarget("Right grip goal", targetRoot);
        rig.right.elbowHint = NewTarget("Right elbow bend", targetRoot);
        rig.left.upperArm = Bone("UpperArm.L");
        rig.left.lowerArm = Bone("LowerArm.L");
        rig.left.wrist = Bone("Wrist.L");
        rig.left.target = NewTarget("Left support goal", targetRoot);
        rig.left.elbowHint = NewTarget("Left elbow bend", targetRoot);
        rig.controlMode = TwoHandPosePreview.JointMode.WristIK;
        rig.livePreviewInEditMode = false;
        rig.CaptureRestPose();

        WeaponHandCombo combo = fps.gameObject.AddComponent<WeaponHandCombo>();
        combo.controller = fps;
        combo.gun = gun;
        combo.viewCamera = camera;
        combo.arms = rig;
        combo.weaponAnchor = weaponAnchor;
        combo.gunGripFrame = frame;
        combo.chest = Bone("Chest");
        combo.hip = hip;
        combo.shortADS = near;
        combo.longADS = far;
        combo.adsDistance = 1f;
        // Initial editor view = long ADS; no Play Mode required to see a pose.
        SetPreview(combo, far);
        EditorUtility.SetDirty(combo);
        EditorUtility.SetDirty(rig);
        EditorUtility.SetDirty(fps);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            Debug.LogError("[Weapon combo] Failed to save " + result);
        else
        {
            Selection.activeGameObject = frame.gameObject;
            Debug.Log("[Weapon combo] " + result + " — copied actual SampleScene ColtRoot intact. " +
                "Grip calibration is approximate: move ONE frame to fit both hands to the assembled gun. " +
                "Play: RMB ADS, 1 near ADS, 2 far ADS, release RMB for low ready; all existing weapon controls remain.");
        }
    }

    private static WeaponHandCombo.Pose ReadPose(string path, out Vector3 eye,
        out Vector3 frame, out Quaternion frameRotation)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            TwoHandPosePreview rig = Find<TwoHandPosePreview>(scene);
            Camera camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault(c => c.name.StartsWith("FPS comparison camera", StringComparison.Ordinal));
            Transform anchor = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "WeaponAnchor reference (from SampleScene)");
            if (rig == null || camera == null || anchor == null)
                throw new InvalidOperationException("Incomplete pose scene: " + path);
            eye = rig.transform.InverseTransformPoint(camera.transform.position);
            frame = camera.transform.InverseTransformPoint(anchor.position);
            frameRotation = Quaternion.Inverse(camera.transform.rotation) * anchor.rotation;
            WeaponHandCombo.Pose pose = new WeaponHandCombo.Pose();
            pose.weaponPosition = frame;
            pose.weaponRotation = frameRotation;
            pose.rightGrip = anchor.InverseTransformPoint(rig.right.target.position);
            pose.leftGrip = anchor.InverseTransformPoint(rig.left.target.position);
            pose.rightRotation = Quaternion.Inverse(anchor.rotation) * rig.right.target.rotation;
            pose.leftRotation = Quaternion.Inverse(anchor.rotation) * rig.left.target.rotation;
            pose.rightElbow = rig.transform.InverseTransformPoint(rig.right.elbowHint.position);
            pose.leftElbow = rig.transform.InverseTransformPoint(rig.left.elbowHint.position);
            pose.rightFingerCurl = rig.right.fingerGrip;
            pose.leftFingerCurl = rig.left.fingerGrip;
            pose.rightThumbCurl = rig.right.thumbGrip;
            pose.leftThumbCurl = rig.left.thumbGrip;
            pose.rightThumbSpread = rig.right.thumbSplay;
            pose.leftThumbSpread = rig.left.thumbSplay;
            pose.rightFingerDegrees = rig.right.fingerCurlDegrees;
            pose.leftFingerDegrees = rig.left.fingerCurlDegrees;
            pose.rightThumbDegrees = rig.right.thumbCurlDegrees;
            pose.leftThumbDegrees = rig.left.thumbCurlDegrees;
            pose.rightSpreadDegrees = rig.right.thumbSplayDegrees;
            pose.leftSpreadDegrees = rig.left.thumbSplayDegrees;
            pose.rightInvertFinger = rig.right.invertFingerCurl;
            pose.leftInvertFinger = rig.left.invertFingerCurl;
            pose.rightInvertThumb = rig.right.invertThumbCurl;
            pose.leftInvertThumb = rig.left.invertThumbCurl;
            return pose;
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    private static void SetPreview(WeaponHandCombo combo, WeaponHandCombo.Pose pose)
    {
        TwoHandPosePreview rig = combo.arms;
        Transform frame = combo.gunGripFrame;
        rig.right.target.SetPositionAndRotation(frame.TransformPoint(pose.rightGrip), frame.rotation * pose.rightRotation);
        rig.left.target.SetPositionAndRotation(frame.TransformPoint(pose.leftGrip), frame.rotation * pose.leftRotation);
        rig.right.elbowHint.position = rig.transform.TransformPoint(pose.rightElbow);
        rig.left.elbowHint.position = rig.transform.TransformPoint(pose.leftElbow);
        rig.right.fingerGrip = pose.rightFingerCurl;
        rig.left.fingerGrip = pose.leftFingerCurl;
        rig.right.thumbGrip = pose.rightThumbCurl;
        rig.left.thumbGrip = pose.leftThumbCurl;
        rig.right.thumbSplay = pose.rightThumbSpread;
        rig.left.thumbSplay = pose.leftThumbSpread;
        rig.right.fingerCurlDegrees = pose.rightFingerDegrees;
        rig.left.fingerCurlDegrees = pose.leftFingerDegrees;
        rig.right.thumbCurlDegrees = pose.rightThumbDegrees;
        rig.left.thumbCurlDegrees = pose.leftThumbDegrees;
        rig.right.thumbSplayDegrees = pose.rightSpreadDegrees;
        rig.left.thumbSplayDegrees = pose.leftSpreadDegrees;
        rig.right.invertFingerCurl = pose.rightInvertFinger;
        rig.left.invertFingerCurl = pose.leftInvertFinger;
        rig.right.invertThumbCurl = pose.rightInvertThumb;
        rig.left.invertThumbCurl = pose.leftInvertThumb;
        rig.PreviewPose();
        foreach (Transform bone in rig.DrivenBones)
            if (bone != null) PrefabUtility.RecordPrefabInstancePropertyModifications(bone);
    }

    private static Transform NewTarget(string name, Transform parent)
    {
        Transform result = new GameObject(name).transform;
        result.SetParent(parent, false);
        return result;
    }

    private static T Find<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault();
    }

    private static bool HasMeshFrom(TacticalGun gun, string assetSuffix)
    {
        return gun.GetComponentsInChildren<MeshFilter>(true).Any(mesh =>
            mesh.sharedMesh != null && AssetDatabase.GetAssetPath(mesh.sharedMesh)
                .EndsWith(assetSuffix, StringComparison.OrdinalIgnoreCase));
    }

    private static string FindAsset(string suffix)
    {
        string[] assets = AssetDatabase.FindAssets(System.IO.Path.GetFileNameWithoutExtension(suffix))
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)).ToArray();
        return assets.Length == 1 ? assets[0] : null;
    }
}
