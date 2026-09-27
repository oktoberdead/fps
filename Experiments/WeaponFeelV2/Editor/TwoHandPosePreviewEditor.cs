using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(TwoHandPosePreview))]
public class TwoHandPosePreviewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        TwoHandPosePreview rig = (TwoHandPosePreview)target;
        EditorGUILayout.HelpBox("Sandbox only. Move the separate wrist targets to place both hands; " +
            "elbow hints control the bend. Preview again after each change. " +
            "Captured bone rotations are preserved so repeated previews do not accumulate errors.", MessageType.Info);

        if (PrefabUtility.IsPartOfPrefabAsset(rig.gameObject))
        {
            EditorGUILayout.HelpBox("Instantiate the FBX in a rig sandbox first; never edit the model asset.", MessageType.Warning);
            return;
        }
        string path = rig.gameObject.scene.path.Replace('\\', '/');
        if (!path.Contains("/Experiments/WeaponFeelV2/"))
        {
            EditorGUILayout.HelpBox("Pose buttons are restricted to a WeaponFeelV2 test scene.", MessageType.Warning);
            return;
        }

        if (!rig.HasRestPose)
            EditorGUILayout.HelpBox("No rest rotations captured. Restore the imported rest pose and capture it first.", MessageType.Warning);

        if (GUILayout.Button("Preview pose (recalculate IK)")) Run(rig, rig.PreviewPose, "Preview two-hand pose");
        if (GUILayout.Button("Restore captured rest pose")) Run(rig, rig.RestoreRestPose, "Restore rig rest pose");
        if (GUILayout.Button("Capture current pose as rest (use carefully)"))
        {
            if (EditorUtility.DisplayDialog("Capture new rest pose?",
                "This stores the CURRENT bone rotations. First restore the original model pose; do not capture an IK preview as rest.",
                "Capture", "Cancel"))
                Run(rig, rig.CaptureRestPose, "Capture rig rest pose");
        }
    }

    private static void Run(TwoHandPosePreview rig, System.Action operation, string undoName)
    {
        Undo.RecordObject(rig, undoName);
        foreach (Transform bone in rig.DrivenBones)
            if (bone != null) Undo.RecordObject(bone, undoName);
        operation();
        EditorUtility.SetDirty(rig);
        EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
        SceneView.RepaintAll();
    }
}
