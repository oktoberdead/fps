using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Only installs editable target objects in a saved rig sandbox scene.
public static class TwoHandRigSetup
{
    [MenuItem("Tools/Weapon Feel V2/Add two-hand pose to open sandbox")]
    public static void SetupOpenSandbox()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !(scene.path.Replace('\\', '/').Contains("/Experiments/WeaponFeelV2/") ||
             scene.path.Replace('\\', '/').Contains("/Experiments/exp/")) ||
            !scene.name.StartsWith("PunkM_RigSandbox", StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("Weapon Feel V2",
                "Open a generated PunkM_RigSandbox scene first. The production SampleScene is never modified.", "OK");
            return;
        }
        GameObject character = scene.GetRootGameObjects().FirstOrDefault(root =>
            root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length >= 2);
        if (character == null)
        {
            Debug.LogError("[Weapon Feel V2] Cannot find the skinned PunkM character in this sandbox.");
            return;
        }
        if (character.GetComponent<TwoHandPosePreview>() != null)
        {
            Selection.activeGameObject = character;
            Debug.Log("[Weapon Feel V2] Two-hand target rig is already installed.", character);
            return;
        }

        Transform[] bones = character.GetComponentsInChildren<Transform>(true);
        Transform FindBone(string name) { return bones.FirstOrDefault(bone => bone.name == name); }
        string[] required = { "Chest", "Shoulder.L", "UpperArm.L", "LowerArm.L", "Wrist.L",
                              "Shoulder.R", "UpperArm.R", "LowerArm.R", "Wrist.R" };
        string[] missing = required.Where(name => FindBone(name) == null).ToArray();
        if (missing.Length != 0)
        {
            EditorUtility.DisplayDialog("Weapon Feel V2", "Missing bones: " + string.Join(", ", missing) +
                ". On the FBX Rig tab disable Optimize Game Objects, then Apply and retry.", "OK");
            return;
        }

        Transform chest = FindBone("Chest");
        Transform rightUpper = FindBone("UpperArm.R");
        Transform leftUpper = FindBone("UpperArm.L");
        float reach = Vector3.Distance(rightUpper.position, FindBone("LowerArm.R").position) +
                      Vector3.Distance(FindBone("LowerArm.R").position, FindBone("Wrist.R").position);
        float shoulderWidth = Vector3.Distance(rightUpper.position, leftUpper.position);
        reach = Mathf.Max(0.1f, reach);
        shoulderWidth = Mathf.Max(0.1f, shoulderWidth);

        GameObject targets = new GameObject("Two-hand pose targets (move these, not FBX bones)");
        Undo.RegisterCreatedObjectUndo(targets, "Create two-hand pose targets");
        SceneManager.MoveGameObjectToScene(targets, scene);
        Transform rightGrip = Target("Right wrist / pistol grip", targets.transform,
            chest.position + chest.forward * (reach * 0.65f) + chest.right * (shoulderWidth * 0.2f) -
            chest.up * (shoulderWidth * 0.25f), FindBone("Wrist.R").rotation);
        Transform leftGrip = Target("Left wrist / support", targets.transform,
            rightGrip.position - chest.right * (shoulderWidth * 0.17f) + chest.forward * (shoulderWidth * 0.08f),
            FindBone("Wrist.L").rotation);
        Transform rightElbow = Target("Right elbow hint", targets.transform,
            rightUpper.position + chest.right * (shoulderWidth * 0.45f) - chest.up * (shoulderWidth * 0.55f) +
            chest.forward * (shoulderWidth * 0.15f), Quaternion.identity);
        Transform leftElbow = Target("Left elbow hint", targets.transform,
            leftUpper.position - chest.right * (shoulderWidth * 0.45f) - chest.up * (shoulderWidth * 0.55f) +
            chest.forward * (shoulderWidth * 0.15f), Quaternion.identity);

        TwoHandPosePreview rig = Undo.AddComponent<TwoHandPosePreview>(character);
        rig.right.upperArm = rightUpper;
        rig.right.lowerArm = FindBone("LowerArm.R");
        rig.right.wrist = FindBone("Wrist.R");
        rig.right.target = rightGrip;
        rig.right.elbowHint = rightElbow;
        rig.left.upperArm = leftUpper;
        rig.left.lowerArm = FindBone("LowerArm.L");
        rig.left.wrist = FindBone("Wrist.L");
        rig.left.target = leftGrip;
        rig.left.elbowHint = leftElbow;
        rig.CaptureRestPose();
        EditorUtility.SetDirty(rig);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = character;
        Debug.Log("[Weapon Feel V2] Two-hand preview ready. In the TwoHandPosePreview Inspector click Preview pose. " +
                  "Move the separate target objects and preview again; no production scene or FBX asset changed.", character);
    }

    private static Transform Target(string name, Transform parent, Vector3 position, Quaternion rotation)
    {
        GameObject target = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(target, "Add hand pose target");
        target.transform.SetParent(parent);
        target.transform.SetPositionAndRotation(position, rotation);
        return target.transform;
    }
}
