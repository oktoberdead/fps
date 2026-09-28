using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// In-place, non-destructive upgrade for the user's already tuned tests/WeaponCombo.
// Does NOT recreate SampleScene or overwrite any of the three authored poses.
public static class WeaponComboRepair
{
    [MenuItem("Tools/Weapon Feel V2/Repair current WeaponCombo (keep tuning)")]
    private static void Repair()
    {
        Scene scene = SceneManager.GetActiveScene();
        string path = scene.path.Replace('\\', '/');
        bool generated = path.Contains("/Experiments/WeaponCombo/") &&
                         scene.name.StartsWith("WeaponCombo", StringComparison.OrdinalIgnoreCase);
        bool authored = path.Contains("/tests/") &&
                        scene.name.StartsWith("WeaponCombo", StringComparison.OrdinalIgnoreCase);
        if (!scene.IsValid() || (!generated && !authored))
        {
            EditorUtility.DisplayDialog("Weapon combo", "Open tests/WeaponCombo*.unity or an isolated " +
                "WeaponCombo scene first. SampleScene is never modified.", "OK");
            return;
        }
        WeaponHandCombo combo = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            combo = root.GetComponentInChildren<WeaponHandCombo>(true);
            if (combo != null) break;
        }
        if (combo == null || combo.weaponAnchor == null || combo.gunGripFrame == null || combo.arms == null)
        {
            EditorUtility.DisplayDialog("Weapon combo", "Missing combo/weapon/arm references in this scene.", "OK");
            return;
        }
        if (combo.poseAlignment != null)
        {
            StabilizeGunPreview(combo);
            WeaponHandComboEditor.ApplyPreview(combo);
            Selection.activeGameObject = combo.poseAlignment.gameObject;
            Debug.Log("[Weapon combo] Kept alignment and elbow offsets. Neutralized ColtRoot's " +
                "stale edit-only ADS/recoil preview; actual ADS and firing settings are unchanged.");
            return;
        }

        Transform anchor = combo.weaponAnchor;
        Transform originalParent = anchor.parent;
        if (originalParent == null)
        {
            Debug.LogError("[Weapon combo] WeaponAnchor has no camera parent; nothing changed.");
            return;
        }
        Vector3 originalPosition = anchor.localPosition;
        Quaternion originalRotation = anchor.localRotation;
        // Mirror hand roles, NOT the mesh of the actual right-handed pistol.
        Undo.RecordObject(combo, "Add combo pose alignment and mirrored hands");
        combo.mirrorHands = true;
        if (combo.rightElbowOffset == Vector3.zero) combo.rightElbowOffset = new Vector3(0f, -0.38f, 0f);
        if (combo.leftElbowOffset == Vector3.zero) combo.leftElbowOffset = new Vector3(0f, 0.15f, 0f);
        WeaponHandCombo.Pose pose = combo.CurrentPose(combo.previewADS, combo.previewDistance);

        Transform alignment = new GameObject("MOVE WEAPON + BOTH HANDS (pose alignment)").transform;
        Undo.RegisterCreatedObjectUndo(alignment.gameObject, "Add whole-pose alignment");
        alignment.SetParent(originalParent, false);
        // Preserve the assembled gun's EXACT world pose despite changing its
        // parent and mirroring the goals. Current user-edited WeaponAnchor is not lost.
        Quaternion alignmentRotation = originalRotation * Quaternion.Inverse(pose.weaponRotation);
        alignment.localRotation = alignmentRotation;
        alignment.localPosition = originalPosition - alignmentRotation * pose.weaponPosition;
        Undo.SetTransformParent(anchor, alignment, "Reparent assembled pistol under whole-pose alignment");
        Undo.RecordObject(anchor, "Keep authored weapon position");
        anchor.localPosition = pose.weaponPosition;
        anchor.localRotation = pose.weaponRotation;
        combo.poseAlignment = alignment;
        Undo.RecordObject(combo.gunGripFrame.gameObject, "Clarify hand-only offset name");
        combo.gunGripFrame.name = "Hand contact offset (hands only; gun stays put)";
        StabilizeGunPreview(combo);
        WeaponHandComboEditor.ApplyPreview(combo);
        EditorUtility.SetDirty(alignment);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = alignment.gameObject;
        Debug.Log("[Weapon combo] Upgraded in place: your assembled pistol/hand offset and all three poses " +
            "were kept. New selected parent moves gun + both hands; select Player for elbow controls. " +
            "Preview before saving the scene.", alignment);
    }

    private static void StabilizeGunPreview(WeaponHandCombo combo)
    {
        TacticalGun gun = combo.gun;
        if (gun == null || (!gun.previewInADS && gun.currentPreviewMode == TacticalGun.PreviewMode.None &&
                            Mathf.Approximately(gun.previewSlider, 0f))) return;
        Undo.RecordObject(gun, "Stop stale Colt edit-only ADS preview");
        Undo.RecordObject(gun.transform, "Return Colt to its configured idle base");
        gun.previewInADS = false;
        gun.currentPreviewMode = TacticalGun.PreviewMode.None;
        gun.previewSlider = 0f;
        gun.transform.localPosition = gun.baseIdlePos;
        gun.transform.localRotation = Quaternion.Euler(gun.baseIdleRot);
        EditorUtility.SetDirty(gun);
        EditorSceneManager.MarkSceneDirty(gun.gameObject.scene);
    }
}
