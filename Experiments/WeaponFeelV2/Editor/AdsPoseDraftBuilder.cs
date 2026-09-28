using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Build two REVIEWABLE drafts from the user's authored far-ADS scene. We keep
// its FOV, eye position, mesh, finger grip and gun orientation. Rather than
// guessing six joint rotations per arm, we move the gun/hand goals together,
// solve both arms, then bake the result back into editable ManualJoints values.
public static class AdsPoseDraftBuilder
{
    private const string MenuPath = "Tools/Weapon Feel V2/Create near ADS + hip drafts from farthestADS";

    [MenuItem(MenuPath)]
    private static void Create()
    {
        Scene source = SceneManager.GetActiveScene();
        string path = source.path.Replace('\\', '/');
        if (!source.IsValid() || !path.EndsWith("/farthestADS/farthestADS.unity", StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("Weapon Feel V2",
                "Open farthestADS/farthestADS.unity first. This command never edits SampleScene.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        int folder = path.LastIndexOf("/farthestADS/", StringComparison.OrdinalIgnoreCase);
        string destinationFolder = path.Substring(0, folder) + "/Experiments/WeaponFeelV2";
        if (!AssetDatabase.IsValidFolder(destinationFolder))
        {
            EditorUtility.DisplayDialog("Weapon Feel V2", "Cannot find " + destinationFolder, "OK");
            return;
        }
        string near = AssetDatabase.GenerateUniqueAssetPath(destinationFolder +
            "/PunkM_RigSandbox_NearADS_Draft.unity");
        string hip = AssetDatabase.GenerateUniqueAssetPath(destinationFolder +
            "/PunkM_RigSandbox_Hip_Draft.unity");

        // Save BOTH copies before opening either one. saveAsCopy preserves the
        // active authored scene, its hand targets, and all user-made camera edits.
        if (!EditorSceneManager.SaveScene(source, near, true) ||
            !EditorSceneManager.SaveScene(source, hip, true))
        {
            Debug.LogError("[Weapon Feel V2] Failed to copy farthestADS; original is unchanged.");
            return;
        }

        // Parent gun pivot is (0, -0.091, 0.469) relative to the user's FPS
        // camera. Offsets are deliberately conservative and only starter poses.
        // HIP is a visible low-ready draft; true waist level may require a
        // separate FPS viewmodel to remain on screen at the chosen 88-degree FOV.
        BuildDraft(near, new Vector3(0.03f, -0.18f, 0.35f), 0.12f, 0.10f);
        BuildDraft(hip, new Vector3(0.05f, -0.32f, 0.38f), 0.13f, 0.15f);
        Debug.Log("[Weapon Feel V2] Created near ADS draft: " + near + " and low/hip draft: " + hip +
            ". Original farthestADS remains untouched. Open each draft and inspect FPS and both sides; " +
            "the guns/IK goals are not gameplay animations yet.");
    }

    private static void BuildDraft(string path, Vector3 gunLocalPosition, float elbowOut, float elbowDown)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        TwoHandPosePreview rig = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<TwoHandPosePreview>(true)).FirstOrDefault();
        if (rig == null || rig.controlMode != TwoHandPosePreview.JointMode.ManualJoints ||
            rig.right.wrist == null || rig.left.wrist == null ||
            rig.right.target == null || rig.left.target == null ||
            rig.right.elbowHint == null || rig.left.elbowHint == null)
        {
            Debug.LogError("[Weapon Feel V2] Far ADS pose rig is incomplete in " + path +
                ". The copied scene remains available for inspection.");
            return;
        }
        Transform gun = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == "WeaponAnchor reference (from SampleScene)");
        if (gun == null || gun.parent == null || gun.parent.GetComponent<Camera>() == null)
        {
            Debug.LogError("[Weapon Feel V2] Cannot find the camera-relative weapon pivot in " + path);
            return;
        }

        // Preview the authored far pose first. Its saved wrist targets were last
        // used in IK mode and need not coincide with actual MANUAL wrist positions.
        rig.PreviewPose();
        rig.right.target.SetPositionAndRotation(rig.right.wrist.position, rig.right.wrist.rotation);
        rig.left.target.SetPositionAndRotation(rig.left.wrist.position, rig.left.wrist.rotation);
        gun.localPosition = gunLocalPosition;

        // Wrist goals now follow the gun by the exact same displacement, keeping
        // the contact shape from the far pose. Bend elbow hints down/outwards.
        MoveHint(rig.right, rig.transform, elbowOut, elbowDown);
        MoveHint(rig.left, rig.transform, -elbowOut, elbowDown);
        rig.controlMode = TwoHandPosePreview.JointMode.WristIK;
        rig.poseWeight = 1f;
        rig.PreviewPose();
        float rightMiss = Vector3.Distance(rig.right.wrist.position, rig.right.target.position);
        float leftMiss = Vector3.Distance(rig.left.wrist.position, rig.left.target.position);
        if (rightMiss > 0.015f || leftMiss > 0.015f)
            Debug.LogWarning("[Weapon Feel V2] Draft wrist goals may be out of reach: R " + rightMiss.ToString("F3") +
                " m, L " + leftMiss.ToString("F3") + " m. Check the draft pose in Scene View.");

        // Re-express solved joint rotations as editable shoulder/forearm/wrist
        // angles. Grip values and the authored FOV/camera stay unchanged.
        rig.ConvertCurrentPoseToManual();
        EditorUtility.SetDirty(rig);
        foreach (Transform bone in rig.DrivenBones)
        {
            if (bone == null) continue;
            EditorUtility.SetDirty(bone);
            PrefabUtility.RecordPrefabInstancePropertyModifications(bone);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            Debug.LogError("[Weapon Feel V2] Unable to save draft " + path);
    }

    private static void MoveHint(TwoHandPosePreview.Arm arm, Transform character, float outwards, float down)
    {
        Vector3 midpoint = (arm.upperArm.position + arm.target.position) * 0.5f;
        arm.elbowHint.position = midpoint + character.right * outwards -
                                 character.up * down - character.forward * 0.025f;
    }
}
