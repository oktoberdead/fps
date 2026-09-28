using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Unity calls the editor tick even when a wrist target is selected instead of
// the rig. Only changed targets in saved sandbox scenes trigger a new solve.
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
    private int activeArm;
    private int activeJoint;

    private struct PoseState
    {
        private Vector3 rightHand, leftHand, rightElbow, leftElbow;
        private Quaternion rightRotation, leftRotation;
        private Vector3 rightShoulder, rightForearm, rightWrist;
        private Vector3 leftShoulder, leftForearm, leftWrist;
        private float weight, rightGrip, leftGrip, rightThumb, leftThumb;
        private float rightCurl, leftCurl, rightThumbCurl, leftThumbCurl;
        private float rightSpread, leftSpread, rightSpreadAngle, leftSpreadAngle;
        private bool rightInvert, leftInvert, rightThumbInvert, leftThumbInvert;
        private TwoHandPosePreview.JointMode mode;

        public PoseState(TwoHandPosePreview rig)
        {
            rightHand = rig.right.target.position;
            leftHand = rig.left.target.position;
            rightElbow = rig.right.elbowHint.position;
            leftElbow = rig.left.elbowHint.position;
            rightRotation = rig.right.target.rotation;
            leftRotation = rig.left.target.rotation;
            rightShoulder = rig.right.shoulderAngles;
            rightForearm = rig.right.forearmAngles;
            rightWrist = rig.right.wristAngles;
            leftShoulder = rig.left.shoulderAngles;
            leftForearm = rig.left.forearmAngles;
            leftWrist = rig.left.wristAngles;
            rightGrip = rig.right.fingerGrip;
            leftGrip = rig.left.fingerGrip;
            rightThumb = rig.right.thumbGrip;
            leftThumb = rig.left.thumbGrip;
            rightCurl = rig.right.fingerCurlDegrees;
            leftCurl = rig.left.fingerCurlDegrees;
            rightThumbCurl = rig.right.thumbCurlDegrees;
            leftThumbCurl = rig.left.thumbCurlDegrees;
            rightSpread = rig.right.thumbSplay;
            leftSpread = rig.left.thumbSplay;
            rightSpreadAngle = rig.right.thumbSplayDegrees;
            leftSpreadAngle = rig.left.thumbSplayDegrees;
            rightInvert = rig.right.invertFingerCurl;
            leftInvert = rig.left.invertFingerCurl;
            rightThumbInvert = rig.right.invertThumbCurl;
            leftThumbInvert = rig.left.invertThumbCurl;
            weight = rig.poseWeight;
            mode = rig.controlMode;
        }

        public bool SameAs(PoseState other)
        {
            return rightHand.Equals(other.rightHand) && leftHand.Equals(other.leftHand) &&
                   rightElbow.Equals(other.rightElbow) && leftElbow.Equals(other.leftElbow) &&
                   rightRotation.Equals(other.rightRotation) && leftRotation.Equals(other.leftRotation) &&
                   rightShoulder.Equals(other.rightShoulder) && rightForearm.Equals(other.rightForearm) &&
                   rightWrist.Equals(other.rightWrist) && leftShoulder.Equals(other.leftShoulder) &&
                   leftForearm.Equals(other.leftForearm) && leftWrist.Equals(other.leftWrist) &&
                   rightGrip.Equals(other.rightGrip) && leftGrip.Equals(other.leftGrip) &&
                   rightThumb.Equals(other.rightThumb) && leftThumb.Equals(other.leftThumb) &&
                   rightCurl.Equals(other.rightCurl) && leftCurl.Equals(other.leftCurl) &&
                   rightThumbCurl.Equals(other.rightThumbCurl) && leftThumbCurl.Equals(other.leftThumbCurl) &&
                   rightSpread.Equals(other.rightSpread) && leftSpread.Equals(other.leftSpread) &&
                   rightSpreadAngle.Equals(other.rightSpreadAngle) && leftSpreadAngle.Equals(other.leftSpreadAngle) &&
                   rightInvert == other.rightInvert && leftInvert == other.leftInvert &&
                   rightThumbInvert == other.rightThumbInvert && leftThumbInvert == other.leftThumbInvert &&
                   weight.Equals(other.weight) && mode == other.mode;
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
               (rig.gameObject.scene.path.Replace('\\', '/').Contains("/Experiments/WeaponFeelV2/") ||
                rig.gameObject.scene.path.Replace('\\', '/').Contains("/Experiments/exp/")) &&
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
        EditorUtility.SetDirty(rig); // Also persists lazy finger-bone bindings on older scenes.
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
        if (!IsSandbox(rig))
        {
            EditorGUILayout.HelpBox("Pose controls work only in a saved PunkM_RigSandbox scene, " +
                "never on the FBX asset or SampleScene.", MessageType.Warning);
            return;
        }
        if (!rig.HasRestPose || !HasTargets(rig))
        {
            EditorGUILayout.HelpBox("Rest pose or targets missing. Add the rig through the Tools menu again.", MessageType.Warning);
            return;
        }

        EditorGUILayout.Space();
        EditorGUI.BeginChangeCheck();
        TwoHandPosePreview.JointMode nextMode = (TwoHandPosePreview.JointMode)
            EditorGUILayout.EnumPopup("Arm control", rig.controlMode);
        if (EditorGUI.EndChangeCheck())
        {
            if (nextMode == TwoHandPosePreview.JointMode.ManualJoints)
                Run(rig, rig.ConvertCurrentPoseToManual, "Keep current pose, edit each joint");
            else
                Run(rig, () => { rig.controlMode = nextMode; rig.PreviewPose(); }, "Switch to wrist IK");
        }

        if (rig.controlMode == TwoHandPosePreview.JointMode.ManualJoints)
        {
            EditorGUILayout.HelpBox("Independent upper arm, forearm and wrist rotations, relative to imported " +
                "rest pose. The shoulder/elbow positions stay attached to the skeleton. Wrist IK targets are " +
                "ignored in this mode. Select the character to use the rotation rings in Scene View.", MessageType.Info);
            activeArm = GUILayout.Toolbar(activeArm, new[] { "Right arm", "Left arm" });
            activeJoint = GUILayout.Toolbar(activeJoint, new[] { "Shoulder", "Forearm", "Wrist" });
            TwoHandPosePreview.Arm arm = activeArm == 0 ? rig.right : rig.left;
            EditorGUI.BeginChangeCheck();
            Vector3 shoulder = EditorGUILayout.Vector3Field("Shoulder offset (degrees)", arm.shoulderAngles);
            Vector3 forearm = EditorGUILayout.Vector3Field("Forearm offset (degrees)", arm.forearmAngles);
            Vector3 wrist = EditorGUILayout.Vector3Field("Wrist offset (degrees)", arm.wristAngles);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(rig, "Rotate arm joints");
                arm.shoulderAngles = shoulder;
                arm.forearmAngles = forearm;
                arm.wristAngles = wrist;
                EditorUtility.SetDirty(rig);
                if (rig.livePreviewInEditMode) ApplyPose(rig);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Wrist target + elbow hint IK. To edit the three joints separately, " +
                "switch Arm control to ManualJoints; the current pose is retained.", MessageType.Info);
            DrawIKControls(rig);
            WarnIfOutOfReach(rig.right, "Right hand");
            WarnIfOutOfReach(rig.left, "Left hand");
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Coarse grip (knuckles, not palm bones)", EditorStyles.boldLabel);
        DrawGrip(rig, rig.right, "Right palm");
        DrawGrip(rig, rig.left, "Left palm");

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
                "Do not capture an already solved pose or curled fingers as the new rest.", "Capture", "Cancel"))
                Run(rig, rig.CaptureRestPose, "Capture rig rest pose");
        }
    }

    private void DrawIKControls(TwoHandPosePreview rig)
    {
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
    }

    private static void DrawGrip(TwoHandPosePreview rig, TwoHandPosePreview.Arm arm, string label)
    {
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
        if ((arm.index2 == null || arm.index3 == null || arm.thumb2 == null || arm.thumb3 == null) &&
            (arm.fingerGrip > 0f || arm.thumbGrip > 0f || arm.thumbSplay != 0f))
            EditorGUILayout.HelpBox("Finger2/Finger3 bones not found under the wrist. Disable Optimize Game " +
                "Objects on the PunkM FBX and check Index2 / Thumb2 bone names.", MessageType.Warning);
        EditorGUI.BeginChangeCheck();
        float fingers = EditorGUILayout.Slider("Four fingers", arm.fingerGrip, 0f, 1f);
        float thumb = EditorGUILayout.Slider("Thumb bend", arm.thumbGrip, 0f, 1f);
        float spread = EditorGUILayout.Slider("Thumb spread / oppose", arm.thumbSplay, -1f, 1f);
        float fingerAngle = EditorGUILayout.Slider("Finger bend at 1.0", arm.fingerCurlDegrees, 10f, 110f);
        float thumbAngle = EditorGUILayout.Slider("Thumb bend at 1.0", arm.thumbCurlDegrees, 10f, 70f);
        float spreadAngle = EditorGUILayout.Slider("Thumb spread at 1.0", arm.thumbSplayDegrees, 10f, 70f);
        bool invert = EditorGUILayout.Toggle("Reverse finger bend", arm.invertFingerCurl);
        bool invertThumb = EditorGUILayout.Toggle("Reverse thumb bend", arm.invertThumbCurl);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(rig, "Edit " + label + " grip");
            arm.fingerGrip = fingers;
            arm.thumbGrip = thumb;
            arm.thumbSplay = spread;
            arm.fingerCurlDegrees = fingerAngle;
            arm.thumbCurlDegrees = thumbAngle;
            arm.thumbSplayDegrees = spreadAngle;
            arm.invertFingerCurl = invert;
            arm.invertThumbCurl = invertThumb;
            EditorUtility.SetDirty(rig);
            if (rig.livePreviewInEditMode) ApplyPose(rig);
        }
    }

    private void OnSceneGUI()
    {
        TwoHandPosePreview rig = (TwoHandPosePreview)target;
        if (!IsSandbox(rig) || !HasTargets(rig)) return;
        if (rig.controlMode == TwoHandPosePreview.JointMode.ManualJoints)
        {
            DrawManualHandles(rig);
            return;
        }
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

    private void DrawManualHandles(TwoHandPosePreview rig)
    {
        UnityEngine.Rendering.CompareFunction previousZTest = Handles.zTest;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        string[] joints = { "Shoulder", "Forearm", "Wrist" };
        for (int side = 0; side < 2; side++)
        {
            TwoHandPosePreview.Arm arm = side == 0 ? rig.right : rig.left;
            Transform[] bones = { arm.upperArm, arm.lowerArm, arm.wrist };
            Color color = side == 0 ? Colors[0] : Colors[1];
            for (int joint = 0; joint < bones.Length; joint++)
            {
                Transform bone = bones[joint];
                if (bone == null) continue;
                Handles.color = color;
                float size = HandleUtility.GetHandleSize(bone.position) * 0.11f;
                if (side == activeArm && joint == activeJoint)
                {
                    Handles.SphereHandleCap(0, bone.position, Quaternion.identity, size, EventType.Repaint);
                    EditorGUI.BeginChangeCheck();
                    Quaternion rotation = Handles.RotationHandle(bone.rotation, bone.position);
                    if (EditorGUI.EndChangeCheck())
                    {
                        // The angle is stored on the rig, never on the imported FBX.
                        Undo.RecordObject(rig, "Rotate " + joints[joint]);
                        Quaternion rest = joint == 0 ? arm.upperRest : joint == 1 ? arm.lowerRest : arm.wristRest;
                        Quaternion local = Quaternion.Inverse(bone.parent.rotation) * rotation;
                        Vector3 euler = (Quaternion.Inverse(rest) * local).eulerAngles;
                        Vector3 signed = new Vector3(Mathf.DeltaAngle(0f, euler.x),
                            Mathf.DeltaAngle(0f, euler.y), Mathf.DeltaAngle(0f, euler.z));
                        if (joint == 0) arm.shoulderAngles = signed;
                        else if (joint == 1) arm.forearmAngles = signed;
                        else arm.wristAngles = signed;
                        EditorUtility.SetDirty(rig);
                        if (rig.livePreviewInEditMode) ApplyPose(rig);
                    }
                }
                else if (Handles.Button(bone.position, Quaternion.identity, size, size * 1.2f,
                    Handles.SphereHandleCap))
                {
                    activeArm = side;
                    activeJoint = joint;
                    Repaint();
                }
                Handles.Label(bone.position + Vector3.up * size, (side == 0 ? "R " : "L ") + joints[joint]);
            }
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
