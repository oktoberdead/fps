using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Unity calls the editor tick even when a wrist target is selected instead of
// the rig. Only changed targets in generated sandbox scenes trigger a new solve.
[InitializeOnLoad]
[CustomEditor(typeof(TwoHandPosePreview))]
public class TwoHandPosePreviewEditor : Editor
{
    // Use the object reference as the key: newer Unity versions obsolete GetInstanceID().
    private static readonly Dictionary<TwoHandPosePreview, PoseState> Previous =
        new Dictionary<TwoHandPosePreview, PoseState>();
    private static readonly string[] Labels = { "Right hand / pistol grip", "Left hand / support",
                                                 "Right elbow bend", "Left elbow bend" };
    private static readonly Color[] Colors = { new Color(0.2f, 0.85f, 1f), new Color(1f, 0.62f, 0.2f),
                                                new Color(0.18f, 0.55f, 0.8f), new Color(0.78f, 0.4f, 0.15f) };
    private int activeHandle;
    private bool rotateActiveHand;

    private struct PoseState
    {
        private Vector3 rightHand, leftHand, rightElbow, leftElbow;
        private Quaternion rightRotation, leftRotation;
        private float weight;

        public PoseState(TwoHandPosePreview rig)
        {
            rightHand = rig.right.target.position;
            leftHand = rig.left.target.position;
            rightElbow = rig.right.elbowHint.position;
            leftElbow = rig.left.elbowHint.position;
            rightRotation = rig.right.target.rotation;
            leftRotation = rig.left.target.rotation;
            weight = rig.poseWeight;
        }

        public bool SameAs(PoseState other)
        {
            return rightHand.Equals(other.rightHand) && leftHand.Equals(other.leftHand) &&
                   rightElbow.Equals(other.rightElbow) && leftElbow.Equals(other.leftElbow) &&
                   rightRotation.Equals(other.rightRotation) && leftRotation.Equals(other.leftRotation) &&
                   weight.Equals(other.weight);
        }
    }

    static TwoHandPosePreviewEditor()
    {
        EditorApplication.update += UpdateLivePreviews;
    }

    private static bool IsSandbox(TwoHandPosePreview rig)
    {
        return rig != null && rig.gameObject.scene.IsValid() &&
               !PrefabUtility.IsPartOfPrefabAsset(rig.gameObject) &&
               rig.gameObject.scene.path.Replace('\\', '/').Contains("/Experiments/WeaponFeelV2/") &&
               rig.gameObject.scene.name.StartsWith("PunkM_RigSandbox", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasTargets(TwoHandPosePreview rig)
    {
        return rig.right != null && rig.left != null && rig.right.target != null && rig.left.target != null &&
               rig.right.elbowHint != null && rig.left.elbowHint != null;
    }

    private static void UpdateLivePreviews()
    {
        if (Application.isPlaying) return; // Runtime LateUpdate drives the pose instead.
        foreach (TwoHandPosePreview rig in Resources.FindObjectsOfTypeAll<TwoHandPosePreview>())
        {
            if (!IsSandbox(rig) || !rig.enabled || !rig.livePreviewInEditMode || !rig.HasRestPose || !HasTargets(rig))
            {
                if (rig != null) Previous.Remove(rig);
                continue;
            }

            PoseState current = new PoseState(rig);
            PoseState before;
            if (Previous.TryGetValue(rig, out before) && current.SameAs(before)) continue;
            ApplyPose(rig);
            Previous[rig] = current;
        }
    }

    private static void ApplyPose(TwoHandPosePreview rig)
    {
        rig.PreviewPose();
        foreach (Transform bone in rig.DrivenBones)
        {
            if (bone == null) continue;
            EditorUtility.SetDirty(bone);
            PrefabUtility.RecordPrefabInstancePropertyModifications(bone);
        }
        EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
        SceneView.RepaintAll();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        TwoHandPosePreview rig = (TwoHandPosePreview)target;
        EditorGUILayout.HelpBox("Select a wrist or elbow below. Colored handles appear in Scene View; drag the " +
            "axis arrows to move the target. Hand targets also have rotation rings. " +
            "With Live Preview enabled, the arms follow changes immediately, even when a target is selected.", MessageType.Info);

        if (!IsSandbox(rig))
        {
            EditorGUILayout.HelpBox("Pose controls work only in a generated PunkM_RigSandbox scene, " +
                "never on the FBX asset or SampleScene.", MessageType.Warning);
            return;
        }
        if (!rig.HasRestPose || !HasTargets(rig))
        {
            EditorGUILayout.HelpBox("Rest pose or targets missing. Add the rig through the Tools menu again.", MessageType.Warning);
            return;
        }

        activeHandle = EditorGUILayout.Popup("Edit target", activeHandle,
            new[] { "Right hand", "Left hand", "Right elbow", "Left elbow" });
        Transform[] editable = { rig.right.target, rig.left.target, rig.right.elbowHint, rig.left.elbowHint };
        Transform selected = editable[activeHandle];
        EditorGUI.BeginChangeCheck();
        Vector3 localPosition = EditorGUILayout.Vector3Field("Position (local)", selected.localPosition);
        Vector3 localAngles = activeHandle < 2
            ? EditorGUILayout.Vector3Field("Rotation (local)", selected.localEulerAngles) : Vector3.zero;
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(selected, "Edit " + Labels[activeHandle]);
            selected.localPosition = localPosition;
            if (activeHandle < 2) selected.localEulerAngles = localAngles;
            if (rig.livePreviewInEditMode) ApplyPose(rig);
        }
        if (activeHandle < 2)
            rotateActiveHand = GUILayout.Toolbar(rotateActiveHand ? 1 : 0,
                new[] { "Move hand", "Rotate hand" }) == 1;
        WarnIfOutOfReach(rig.right, "Right hand");
        WarnIfOutOfReach(rig.left, "Left hand");
        if (GUILayout.Button("Preview pose now")) Run(rig, rig.PreviewPose, "Preview two-hand pose");
        if (GUILayout.Button("Restore captured rest pose"))
        {
            Undo.RecordObject(rig, "Disable live pose preview");
            rig.livePreviewInEditMode = false;
            Run(rig, rig.RestoreRestPose, "Restore rig rest pose");
        }
        if (GUILayout.Button("Capture current pose as rest (use carefully)"))
        {
            if (EditorUtility.DisplayDialog("Capture new rest pose?",
                "Disable Live Preview and restore the imported rest pose first. " +
                "Do not capture the already solved IK pose as the new rest.", "Capture", "Cancel"))
                Run(rig, rig.CaptureRestPose, "Capture rig rest pose");
        }
    }

    private void OnSceneGUI()
    {
        TwoHandPosePreview rig = (TwoHandPosePreview)target;
        if (!IsSandbox(rig) || !HasTargets(rig)) return;
        Transform[] handles = { rig.right.target, rig.left.target, rig.right.elbowHint, rig.left.elbowHint };
        UnityEngine.Rendering.CompareFunction previousZTest = Handles.zTest;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always; // visible through the character mesh
        for (int index = 0; index < handles.Length; index++)
        {
            Transform marker = handles[index];
            Handles.color = Colors[index];
            float size = HandleUtility.GetHandleSize(marker.position) * 0.13f;
            if (index != activeHandle)
            {
                if (Handles.Button(marker.position, Quaternion.identity, size, size * 1.2f,
                    Handles.SphereHandleCap))
                {
                    activeHandle = index;
                    Repaint();
                }
            }
            else
            {
                Handles.SphereHandleCap(0, marker.position, Quaternion.identity, size * 0.75f, EventType.Repaint);
                if (index < 2 && rotateActiveHand)
                {
                    EditorGUI.BeginChangeCheck();
                    Quaternion nextRotation = Handles.RotationHandle(marker.rotation, marker.position);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(marker, "Rotate " + Labels[index]);
                        marker.rotation = nextRotation;
                        if (rig.livePreviewInEditMode) ApplyPose(rig);
                    }
                }
                else
                {
                    EditorGUI.BeginChangeCheck();
                    Vector3 nextPosition = Handles.PositionHandle(marker.position, marker.rotation);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(marker, "Move " + Labels[index]);
                        marker.position = nextPosition;
                        if (rig.livePreviewInEditMode) ApplyPose(rig);
                    }
                }
            }
            Handles.Label(marker.position + Vector3.up * size, Labels[index]);
        }
        Handles.color = Color.white;
        Handles.zTest = previousZTest;
    }

    private static void WarnIfOutOfReach(TwoHandPosePreview.Arm arm, string label)
    {
        if (arm.upperArm == null || arm.lowerArm == null || arm.wrist == null || arm.target == null) return;
        float maxReach = Vector3.Distance(arm.upperArm.position, arm.lowerArm.position) +
                         Vector3.Distance(arm.lowerArm.position, arm.wrist.position);
        if (Vector3.Distance(arm.upperArm.position, arm.target.position) > maxReach + 0.005f)
            EditorGUILayout.HelpBox(label + " target is beyond arm reach. The elbow solver clamps it; " +
                "move the target closer to the shoulder.", MessageType.Warning);
    }

    private static void Run(TwoHandPosePreview rig, Action operation, string undoName)
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
