using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Watch only the isolated integrated scene; dragging the calibration frame or
// moving the ADS preview sliders gives a LIVE edit-mode arm/gun preview.
[InitializeOnLoad]
[CustomEditor(typeof(WeaponHandCombo))]
public class WeaponHandComboEditor : Editor
{
    private struct PreviewState
    {
        public Vector3 position;
        public Quaternion rotation;
        public float ads, distance;
        public PreviewState(WeaponHandCombo combo)
        {
            position = combo.gunGripFrame.localPosition;
            rotation = combo.gunGripFrame.localRotation;
            ads = combo.previewADS;
            distance = combo.previewDistance;
        }
        public bool SameAs(PreviewState other)
        {
            return position.Equals(other.position) && rotation.Equals(other.rotation) &&
                   ads.Equals(other.ads) && distance.Equals(other.distance);
        }
    }

    private static readonly Dictionary<WeaponHandCombo, PreviewState> Previous =
        new Dictionary<WeaponHandCombo, PreviewState>();

    static WeaponHandComboEditor() { EditorApplication.update += UpdatePreview; }

    private static bool IsIsolated(WeaponHandCombo combo)
    {
        if (combo == null || combo.gunGripFrame == null || combo.arms == null ||
            !combo.gameObject.scene.IsValid() || PrefabUtility.IsPartOfPrefabAsset(combo.gameObject)) return false;
        string path = combo.gameObject.scene.path.Replace('\\', '/');
        return path.Contains("/Experiments/WeaponCombo/") &&
               combo.gameObject.scene.name.StartsWith("WeaponCombo", StringComparison.OrdinalIgnoreCase);
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
            combo.PreviewPoseInEditor();
            foreach (Transform bone in combo.arms.DrivenBones)
            {
                if (bone == null) continue;
                EditorUtility.SetDirty(bone);
                PrefabUtility.RecordPrefabInstancePropertyModifications(bone);
            }
            EditorSceneManager.MarkSceneDirty(combo.gameObject.scene);
            Previous[combo] = now;
            SceneView.RepaintAll();
        }
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (IsIsolated((WeaponHandCombo)target))
            EditorGUILayout.HelpBox("Move 'Grip calibration' in Scene View to fit both palms to the assembled " +
                "pistol. Preview ADS/Distance update live without Play. Play: RMB ADS, 1 near, 2 far; " +
                "SampleScene is never modified.", MessageType.Info);
    }
}
