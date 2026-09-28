using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Only the isolated combo copy gets live hand/weapon alignment tools.
[InitializeOnLoad]
[CustomEditor(typeof(WeaponHandCombo))]
public class WeaponHandComboEditor : Editor
{
    private struct PreviewState
    {
        public Vector3 position, alignmentPosition, rightElbow, leftElbow;
        public Quaternion rotation, alignmentRotation;
        public float ads, distance;
        public bool mirrored;
        public PreviewState(WeaponHandCombo combo)
        {
            position = combo.gunGripFrame.localPosition;
            rotation = combo.gunGripFrame.localRotation;
            alignmentPosition = combo.poseAlignment.localPosition;
            alignmentRotation = combo.poseAlignment.localRotation;
            rightElbow = combo.rightElbowOffset;
            leftElbow = combo.leftElbowOffset;
            ads = combo.previewADS;
            distance = combo.previewDistance;
            mirrored = combo.mirrorHands;
        }
        public bool SameAs(PreviewState other)
        {
            return position.Equals(other.position) && rotation.Equals(other.rotation) &&
                   alignmentPosition.Equals(other.alignmentPosition) &&
                   alignmentRotation.Equals(other.alignmentRotation) &&
                   rightElbow.Equals(other.rightElbow) && leftElbow.Equals(other.leftElbow) &&
                   ads.Equals(other.ads) && distance.Equals(other.distance) && mirrored == other.mirrored;
        }
    }

    private static readonly Dictionary<WeaponHandCombo, PreviewState> Previous =
        new Dictionary<WeaponHandCombo, PreviewState>();
    private bool showAdvanced;

    static WeaponHandComboEditor() { EditorApplication.update += UpdatePreview; }

    internal static bool IsIsolated(WeaponHandCombo combo)
    {
        if (combo == null || combo.gunGripFrame == null || combo.poseAlignment == null ||
            combo.arms == null || !combo.gameObject.scene.IsValid() ||
            PrefabUtility.IsPartOfPrefabAsset(combo.gameObject)) return false;
        string path = combo.gameObject.scene.path.Replace('\\', '/');
        bool generated = path.Contains("/Experiments/WeaponCombo/") &&
                         combo.gameObject.scene.name.StartsWith("WeaponCombo", StringComparison.OrdinalIgnoreCase);
        bool authored = path.EndsWith("/tests/WeaponCombo.unity", StringComparison.OrdinalIgnoreCase);
        return generated || authored;
    }

    internal static void ApplyPreview(WeaponHandCombo combo)
    {
        combo.PreviewPoseInEditor();
        foreach (Transform bone in combo.arms.DrivenBones)
        {
            if (bone == null) continue;
            EditorUtility.SetDirty(bone);
            PrefabUtility.RecordPrefabInstancePropertyModifications(bone);
        }
        EditorUtility.SetDirty(combo);
        EditorSceneManager.MarkSceneDirty(combo.gameObject.scene);
        SceneView.RepaintAll();
    }

    private static void UpdatePreview()
    {
        if (Application.isPlaying) return;
        foreach (WeaponHandCombo combo in Resources.FindObjectsOfTypeAll<WeaponHandCombo>())
        {
            if (!IsIsolated(combo) || !combo.enabled)
            {
                if (combo != null) Previous.Remove(combo);
                continue;
            }
            PreviewState now = new PreviewState(combo);
            PreviewState before;
            if (Previous.TryGetValue(combo, out before) && now.SameAs(before)) continue;
            ApplyPreview(combo);
            Previous[combo] = now;
        }
    }

    public override void OnInspectorGUI()
    {
        WeaponHandCombo combo = (WeaponHandCombo)target;
        if (!IsIsolated(combo))
        {
            EditorGUILayout.HelpBox("Open the isolated WeaponCombo scene. If this is an older combo, " +
                "run Tools / Weapon Feel V2 / Repair current WeaponCombo first.", MessageType.Warning);
            return;
        }
        serializedObject.Update();
        EditorGUILayout.HelpBox("MOVE WEAPON + BOTH HANDS: select Pose alignment. " +
            "Hand contact offset moves ONLY the hands relative to the gun. " +
            "Elbow rings edit each arm separately.", MessageType.Info);
        if (GUILayout.Button("Select: move whole pistol + both hands"))
            Selection.activeGameObject = combo.poseAlignment.gameObject;
        if (GUILayout.Button("Select: fine-tune hands on pistol only"))
            Selection.activeGameObject = combo.gunGripFrame.gameObject;
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("mirrorHands"),
            new GUIContent("Swap leading hand / mirror body pose"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("previewADS"),
            new GUIContent("Preview ADS (0 = low, 1 = aim)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("previewDistance"),
            new GUIContent("ADS reach (0 = near, 1 = far)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("rightElbowOffset"),
            new GUIContent("Right elbow offset (body metres)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("leftElbowOffset"),
            new GUIContent("Left elbow offset (body metres)"));
        showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced: samples, references and gameplay controls");
        if (showAdvanced)
            DrawPropertiesExcluding(serializedObject, "m_Script", "mirrorHands", "previewADS", "previewDistance",
                "rightElbowOffset", "leftElbowOffset");
        serializedObject.ApplyModifiedProperties();
    }

    private void OnSceneGUI()
    {
        WeaponHandCombo combo = (WeaponHandCombo)target;
        if (!IsIsolated(combo) || combo.arms.right.elbowHint == null || combo.arms.left.elbowHint == null) return;
        WeaponHandCombo.Pose pose = combo.CurrentPose(combo.previewADS, combo.previewDistance);
        for (int side = 0; side < 2; side++)
        {
            Transform elbow = side == 0 ? combo.arms.right.elbowHint : combo.arms.left.elbowHint;
            Handles.color = side == 0 ? new Color(0.2f, 0.85f, 1f) : new Color(1f, 0.65f, 0.25f);
            float size = HandleUtility.GetHandleSize(elbow.position) * 0.12f;
            Handles.SphereHandleCap(0, elbow.position, Quaternion.identity, size, EventType.Repaint);
            Handles.Label(elbow.position + Vector3.up * size, side == 0 ? "RIGHT elbow — drag" : "LEFT elbow — drag");
            EditorGUI.BeginChangeCheck();
            Vector3 nextPosition = Handles.PositionHandle(elbow.position, combo.arms.transform.rotation);
            if (!EditorGUI.EndChangeCheck()) continue;
            Undo.RecordObject(combo, side == 0 ? "Lower right elbow" : "Move left elbow");
            Vector3 baseHint = side == 0 ? pose.rightElbow : pose.leftElbow;
            Vector3 offset = combo.arms.transform.InverseTransformPoint(nextPosition) - baseHint;
            if (side == 0) combo.rightElbowOffset = offset;
            else combo.leftElbowOffset = offset;
            ApplyPreview(combo);
        }
        Handles.color = Color.white;
    }
}
