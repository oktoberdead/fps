using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Creates a new comparison scene from the user's CURRENT sandbox; never rebuilds the
// character or overwrites its hand targets in the source scene or SampleScene.
public static class FpsPoseComparisonBuilder
{
    [MenuItem("Tools/Weapon Feel V2/Make FPS + exterior comparison copy")]
    private static void Create()
    {
        Scene source = SceneManager.GetActiveScene();
        if (!IsSandbox(source))
        {
            EditorUtility.DisplayDialog("Weapon Feel V2",
                "Open the saved Experiments/exp/PunkM_RigSandbox scene first. SampleScene is not supported.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string originalPath = source.path;
        string copyPath = AssetDatabase.GenerateUniqueAssetPath(
            originalPath.Substring(0, originalPath.Length - ".unity".Length) + "_FPSComparison.unity");
        if (!EditorSceneManager.SaveScene(source, copyPath, true))
        {
            Debug.LogError("[Weapon Feel V2] Could not create a comparison copy: " + copyPath);
            return;
        }
        Scene scene = EditorSceneManager.OpenScene(copyPath, OpenSceneMode.Single);
        TwoHandPosePreview rig = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<TwoHandPosePreview>(true)).FirstOrDefault();
        Camera fps = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
            .FirstOrDefault(camera => camera.name == "Rig preview camera");
        if (rig == null || fps == null || rig.right.target == null || rig.left.target == null ||
            rig.right.elbowHint == null || rig.left.elbowHint == null)
        {
            Debug.LogError("[Weapon Feel V2] Missing pose rig or original preview camera in " + copyPath +
                ". Original scene is unchanged; inspect or discard this copy.");
            return;
        }

        Transform character = rig.transform;
        Transform[] bones = character.GetComponentsInChildren<Transform>(true);
        Transform headBone = bones.FirstOrDefault(bone => bone.name == "Head");
        Transform headMesh = bones.FirstOrDefault(bone => bone.name == "Punk_Head");
        if (headBone == null || headMesh == null)
        {
            Debug.LogError("[Weapon Feel V2] Could not find Head and Punk_Head in the copied character.");
            return;
        }

        // Layer 2 is built into Unity, so this isolated experiment does not need
        // any TagManager edits. It excludes only the head mesh in the FPS camera;
        // the exterior camera and Scene View still draw it. No head colliders here.
        int headLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (headLayer < 0)
        {
            Debug.LogError("[Weapon Feel V2] Built-in Ignore Raycast layer is unavailable.");
            return;
        }
        headMesh.gameObject.SetActive(true);
        SetLayerRecursively(headMesh, headLayer);

        fps.name = "FPS comparison camera (enable for Game view)";
        fps.tag = "MainCamera";
        fps.enabled = true;
        fps.fieldOfView = 60f; // Same baseFOV as SimpleFPSController in SampleScene.
        fps.nearClipPlane = 0.03f;
        fps.cullingMask &= ~(1 << headLayer);

        Transform eye = new GameObject("Eye anchor (tune height / offset here)").transform;
        eye.SetParent(character, true);
        eye.SetPositionAndRotation(headBone.position + character.up * 0.105f + character.forward * 0.045f,
            Quaternion.LookRotation(character.forward, character.up));
        fps.transform.SetParent(eye, true);
        fps.transform.localPosition = Vector3.zero;
        fps.transform.localRotation = Quaternion.identity;

        // Duplicate the working camera (including its URP camera settings), rather
        // than creating a bare camera that may not be configured for this pipeline.
        GameObject exteriorObject = UnityEngine.Object.Instantiate(fps.gameObject);
        exteriorObject.name = "Exterior comparison camera (enable for Game view)";
        exteriorObject.transform.SetParent(null, true);
        SceneManager.MoveGameObjectToScene(exteriorObject, scene);
        exteriorObject.tag = "Untagged";
        Camera exterior = exteriorObject.GetComponent<Camera>();
        exterior.enabled = false; // Toggle one camera at a time in the Inspector.
        exterior.cullingMask = -1;
        exterior.nearClipPlane = 0.05f;
        exterior.fieldOfView = 45f;
        exterior.transform.position = character.position + character.right * 2.15f +
                                      character.up * 1.45f + character.forward * 2.65f;
        exterior.transform.LookAt(character.position + character.up * 1.12f);

        // Values are from SampleScene WeaponAnchor. The gun is visual only: no
        // firing scripts, cartridges or ejected cases are added to this scene.
        Transform weaponAnchor = new GameObject("WeaponAnchor reference (from SampleScene)").transform;
        weaponAnchor.SetParent(fps.transform, false);
        weaponAnchor.localPosition = new Vector3(0.245f, -0.226f, 0.489f);
        weaponAnchor.localRotation = Quaternion.Euler(0f, 91.769f, 0f);
        AddVisualPistol(scene, weaponAnchor);

        // ONLY this new copy receives a starter two-hand pose. Restore the stored
        // T pose before reading finger directions; the source scene may have saved
        // solved rotations already. Source targets are never changed.
        rig.RestoreRestPose();
        PlaceArm(rig.right, fps.transform, new Vector3(0.14f, -0.26f, 0.28f),
            character.right, character.forward, bones, "Middle1.R");
        PlaceArm(rig.left, fps.transform, new Vector3(0.045f, -0.27f, 0.27f),
            -character.right, character.forward, bones, "Middle1.L");
        // Both grip goals follow the gun pivot; elbow hints stay on the body.
        rig.right.target.SetParent(weaponAnchor, true);
        rig.left.target.SetParent(weaponAnchor, true);
        rig.livePreviewInEditMode = true;
        rig.PreviewPose();
        EditorUtility.SetDirty(rig);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            Debug.LogError("[Weapon Feel V2] Could not save comparison copy: " + copyPath);
            return;
        }
        Selection.activeGameObject = weaponAnchor.gameObject;
        Debug.Log("[Weapon Feel V2] Comparison copy saved: " + copyPath +
            ". Source scene and SampleScene are unchanged. Game view: disable FPS camera and enable exterior " +
            "camera to compare. The wrist goals follow the gun pivot, but are only approximate grips: " +
            "visually adjust their positions and rotations against the M1911 before using this pose in gameplay.", weaponAnchor);
    }

    private static void AddVisualPistol(Scene scene, Transform anchor)
    {
        string[] candidates = AssetDatabase.FindAssets("m1911-3")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith("/Models/colt1911/m1911-3.fbx", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (candidates.Length != 1)
        {
            Debug.LogWarning("[Weapon Feel V2] Could not locate the main m1911-3 FBX; weapon anchor is empty.");
            return;
        }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(candidates[0]);
        if (prefab == null) return;
        GameObject pistol = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (pistol == null) return;
        pistol.name = "m1911-3 (visual reference only)";
        pistol.transform.SetParent(anchor, false);
        pistol.transform.localPosition = Vector3.zero;
        pistol.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        // In SampleScene the Blender-exported gun meshes use a 0.023 scale.
        pistol.transform.localScale = Vector3.one * 0.023f;
        foreach (Animator animator in pistol.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
    }

    private static void PlaceArm(TwoHandPosePreview.Arm arm, Transform fps, Vector3 wristOffset,
        Vector3 outward, Vector3 forward, Transform[] bones, string middleFingerName)
    {
        // Compute the wrist orientation from the imported skeleton before solving
        // IK: turn its middle finger towards the gun rather than leaving fingers up.
        Transform finger = bones.FirstOrDefault(bone => bone.name == middleFingerName);
        if (finger != null && (finger.position - arm.wrist.position).sqrMagnitude > 0.000001f)
            arm.target.rotation = Quaternion.FromToRotation(finger.position - arm.wrist.position, forward) * arm.wrist.rotation;
        arm.target.position = fps.TransformPoint(wristOffset);
        Vector3 middle = (arm.upperArm.position + arm.target.position) * 0.5f;
        arm.elbowHint.position = middle + outward * 0.09f - fps.up * 0.10f - forward * 0.06f;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root) SetLayerRecursively(child, layer);
    }

    private static bool IsSandbox(Scene scene)
    {
        return scene.IsValid() && scene.path.Replace('\\', '/').Contains("/Experiments/exp/") &&
               scene.name.Equals("PunkM_RigSandbox", StringComparison.OrdinalIgnoreCase);
    }
}
