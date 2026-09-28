using System;
using UnityEngine;

// Prototype integration, confined to an isolated WeaponCombo scene. TacticalGun
// still owns firing, slide, ammunition, sway, bob and recoil; this script makes
// the PunkM arm rig follow that very same moving gun in LateUpdate.
[DefaultExecutionOrder(500)]
public class WeaponHandCombo : MonoBehaviour
{
    [Serializable]
    public struct Pose
    {
        // Camera-relative weapon stance from the corresponding test scene.
        // TacticalGun continues to animate ColtRoot under this parent.
        public Vector3 weaponPosition;
        public Quaternion weaponRotation;
        // Wrist goals in a calibrated frame attached to the actual ColtRoot.
        public Vector3 rightGrip, leftGrip;
        public Quaternion rightRotation, leftRotation;
        // Bend hints relative to the PunkM root (not the moving camera).
        public Vector3 rightElbow, leftElbow;
        public float rightFingerCurl, leftFingerCurl, rightThumbCurl, leftThumbCurl;
        public float rightThumbSpread, leftThumbSpread;
        public float rightFingerDegrees, leftFingerDegrees;
        public float rightThumbDegrees, leftThumbDegrees;
        public float rightSpreadDegrees, leftSpreadDegrees;
        public bool rightInvertFinger, leftInvertFinger, rightInvertThumb, leftInvertThumb;

        public static Pose Blend(Pose a, Pose b, float t)
        {
            t = Mathf.Clamp01(t);
            Pose p = new Pose();
            p.weaponPosition = Vector3.Lerp(a.weaponPosition, b.weaponPosition, t);
            p.weaponRotation = Quaternion.Slerp(a.weaponRotation, b.weaponRotation, t);
            p.rightGrip = Vector3.Lerp(a.rightGrip, b.rightGrip, t);
            p.leftGrip = Vector3.Lerp(a.leftGrip, b.leftGrip, t);
            p.rightRotation = Quaternion.Slerp(a.rightRotation, b.rightRotation, t);
            p.leftRotation = Quaternion.Slerp(a.leftRotation, b.leftRotation, t);
            p.rightElbow = Vector3.Lerp(a.rightElbow, b.rightElbow, t);
            p.leftElbow = Vector3.Lerp(a.leftElbow, b.leftElbow, t);
            p.rightFingerCurl = Mathf.Lerp(a.rightFingerCurl, b.rightFingerCurl, t);
            p.leftFingerCurl = Mathf.Lerp(a.leftFingerCurl, b.leftFingerCurl, t);
            p.rightThumbCurl = Mathf.Lerp(a.rightThumbCurl, b.rightThumbCurl, t);
            p.leftThumbCurl = Mathf.Lerp(a.leftThumbCurl, b.leftThumbCurl, t);
            p.rightThumbSpread = Mathf.Lerp(a.rightThumbSpread, b.rightThumbSpread, t);
            p.leftThumbSpread = Mathf.Lerp(a.leftThumbSpread, b.leftThumbSpread, t);
            p.rightFingerDegrees = Mathf.Lerp(a.rightFingerDegrees, b.rightFingerDegrees, t);
            p.leftFingerDegrees = Mathf.Lerp(a.leftFingerDegrees, b.leftFingerDegrees, t);
            p.rightThumbDegrees = Mathf.Lerp(a.rightThumbDegrees, b.rightThumbDegrees, t);
            p.leftThumbDegrees = Mathf.Lerp(a.leftThumbDegrees, b.leftThumbDegrees, t);
            p.rightSpreadDegrees = Mathf.Lerp(a.rightSpreadDegrees, b.rightSpreadDegrees, t);
            p.leftSpreadDegrees = Mathf.Lerp(a.leftSpreadDegrees, b.leftSpreadDegrees, t);
            p.rightInvertFinger = t < 0.5f ? a.rightInvertFinger : b.rightInvertFinger;
            p.leftInvertFinger = t < 0.5f ? a.leftInvertFinger : b.leftInvertFinger;
            p.rightInvertThumb = t < 0.5f ? a.rightInvertThumb : b.rightInvertThumb;
            p.leftInvertThumb = t < 0.5f ? a.leftInvertThumb : b.leftInvertThumb;
            return p;
        }
    }

    [Header("Existing SampleScene mechanics (do not duplicate the gun)")]
    public SimpleFPSController controller;
    public TacticalGun gun;
    public Camera viewCamera;
    public TwoHandPosePreview arms;
    public Transform weaponAnchor;
    [Tooltip("Move this to reposition the assembled pistol AND both hands together. Never drag the child grip frame for this.")]
    public Transform poseAlignment;
    [Tooltip("Fine correction of hand contact relative to the pistol mesh ONLY.")]
    public Transform gunGripFrame;
    public Transform chest;

    [Header("Authored pose samples: tests/ folder")]
    public Pose hip;
    public Pose shortADS;
    public Pose longADS;

    [Header("Prototype controls")]
    [Min(0.1f)] public float transitionSpeed = 8f;
    [Range(0f, 1f)] public float adsDistance = 1f;
    [Range(0f, 1f)] public float previewADS = 1f;
    [Range(0f, 1f)] public float previewDistance = 1f;
    [Tooltip("Swap right/left pose goals and reflect them across the body. The gun mesh is not scaled or reversed.")]
    public bool mirrorHands;
    [Tooltip("This scene's saved grip is the hip stance even though it was originally previewed as long ADS. Keeps a hand-calibrated hip scene unchanged on Play; source samples are not edited.")]
    public bool useSavedLongGripAtHip;
    [Tooltip("Additional elbow bend relative to PunkM root, in metres; moves with the body, not camera.")]
    public Vector3 rightElbowOffset;
    public Vector3 leftElbowOffset;
    public KeyCode shortAdsKey = KeyCode.Alpha1;
    public KeyCode longAdsKey = KeyCode.Alpha2;
    [Tooltip("Follow camera freelook with only part of the upper torso; legs still face movement direction.")]
    [Range(0f, 1f)] public float torsoFreelookShare = 0.35f;
    [Range(0f, 70f)] public float maxTorsoYaw = 35f;
    private Quaternion chestRest;
    private float adsBlend;
    private float distanceBlend;
    // Authored scene pose is the origin of every transition. Never treat the
    // imported test scene's WeaponAnchor coordinates as a replacement for it.
    private Vector3 authoredAnchorPosition;
    private Quaternion authoredAnchorRotation;
    private Pose authoredReferencePose;
    private bool initialized;

    private void Awake()
    {
        if (arms == null || gun == null || weaponAnchor == null || gunGripFrame == null ||
            viewCamera == null || controller == null)
        {
            Debug.LogError("[WeaponHandCombo] Missing gun/rig/camera references. Regenerate the isolated scene.", this);
            enabled = false;
            return;
        }
        if (chest != null) chestRest = chest.localRotation;
        arms.controlMode = TwoHandPosePreview.JointMode.WristIK;
        arms.poseWeight = 1f;
        arms.livePreviewInEditMode = false;
        // Scene View and the first frame of Play use the SAME stance and the
        // SAME hand-calibrated WeaponAnchor (including manual local edits).
        authoredAnchorPosition = weaponAnchor.localPosition;
        authoredAnchorRotation = weaponAnchor.localRotation;
        authoredReferencePose = CurrentPose(previewADS, previewDistance);
        adsBlend = useSavedLongGripAtHip ? 0f : Mathf.Clamp01(previewADS);
        distanceBlend = Mathf.Clamp01(previewDistance);
        initialized = true;
    }

    private void Update()
    {
        if (!initialized) return;
        if (Input.GetKeyDown(shortAdsKey)) adsDistance = 0f;
        if (Input.GetKeyDown(longAdsKey)) adsDistance = 1f;
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || !initialized) return;
        float follow = 1f - Mathf.Exp(-Mathf.Max(0.1f, transitionSpeed) * Time.deltaTime);
        // Use TacticalGun's existing ADS binding, not a second weapon state machine.
        adsBlend = Mathf.Lerp(adsBlend, Input.GetKey(gun.adsKey) ? 1f : 0f, follow);
        distanceBlend = Mathf.Lerp(distanceBlend, adsDistance, follow);
        Pose pose = CurrentPose(adsBlend, distanceBlend);
        if (chest != null && torsoFreelookShare > 0f)
        {
            float yaw = Mathf.DeltaAngle(0f, viewCamera.transform.localEulerAngles.y);
            float partial = Mathf.Clamp(yaw * torsoFreelookShare, -maxTorsoYaw, maxTorsoYaw);
            // Apply a world-up yaw in the chest's parent space; don't spin the hips.
            Quaternion worldRest = chest.parent.rotation * chestRest;
            chest.rotation = Quaternion.AngleAxis(partial, transform.up) * worldRest;
        }
        else if (chest != null) chest.localRotation = chestRest;
        // Offset relative to the *saved* anchor, not the test scene's anchor.
        // That is essential when an authored hip calibration has local (0,0,0).
        weaponAnchor.localPosition = authoredAnchorPosition +
            pose.weaponPosition - authoredReferencePose.weaponPosition;
        weaponAnchor.localRotation = authoredAnchorRotation *
            Quaternion.Inverse(authoredReferencePose.weaponRotation) * pose.weaponRotation;
        ApplyHands(pose);
    }

    // Parent/contact movement only updates the hands. A deliberate inspector
    // stance change advances the anchor BY the pose delta, retaining any manual
    // calibration of the current scene across edits and into Play Mode.
    public void PreviewPoseInEditor(Pose? previousPose = null)
    {
        if (Application.isPlaying || arms == null || gunGripFrame == null || weaponAnchor == null) return;
        Pose pose = CurrentPose(previewADS, previewDistance);
        if (previousPose.HasValue)
        {
            Pose before = previousPose.Value;
            weaponAnchor.localPosition += pose.weaponPosition - before.weaponPosition;
            weaponAnchor.localRotation = weaponAnchor.localRotation *
                Quaternion.Inverse(before.weaponRotation) * pose.weaponRotation;
        }
        ApplyHands(pose);
    }

    private void ApplyHands(Pose pose)
    {
        // TacticalGun owns ColtRoot's local motion; solve targets from the gun's
        // actual final transform without ever moving it from the arm solver.
        arms.right.target.SetPositionAndRotation(
            gunGripFrame.TransformPoint(pose.rightGrip), gunGripFrame.rotation * pose.rightRotation);
        arms.left.target.SetPositionAndRotation(
            gunGripFrame.TransformPoint(pose.leftGrip), gunGripFrame.rotation * pose.leftRotation);
        arms.right.elbowHint.position = arms.transform.TransformPoint(pose.rightElbow + rightElbowOffset);
        arms.left.elbowHint.position = arms.transform.TransformPoint(pose.leftElbow + leftElbowOffset);
        SetGrip(arms.right, pose.rightFingerCurl, pose.rightThumbCurl, pose.rightThumbSpread,
            pose.rightFingerDegrees, pose.rightThumbDegrees, pose.rightSpreadDegrees,
            pose.rightInvertFinger, pose.rightInvertThumb);
        SetGrip(arms.left, pose.leftFingerCurl, pose.leftThumbCurl, pose.leftThumbSpread,
            pose.leftFingerDegrees, pose.leftThumbDegrees, pose.leftSpreadDegrees,
            pose.leftInvertFinger, pose.leftInvertThumb);
        // TacticalGun ran earlier in LateUpdate: arms now track its final recoil/sway.
        arms.PreviewPose();
    }

    // The current pose is also used by the Scene View elbow handles.
    public Pose CurrentPose(float ads, float distance)
    {
        return PoseFor(ads, distance, mirrorHands, useSavedLongGripAtHip);
    }

    public Pose PoseFor(float ads, float distance, bool mirrored, bool savedHipFromLong)
    {
        // In a scene calibrated visually as hip using the old long-ADS preview,
        // retain that grip as the hip baseline instead of switching to the
        // imported (unfitted) hip sample when Play starts.
        Pose hipBaseline = savedHipFromLong ? longADS : hip;
        Pose pose = Pose.Blend(hipBaseline, Pose.Blend(shortADS, longADS, distance), ads);
        return mirrored ? MirrorPose(pose) : pose;
    }

    // Reflect camera-space wrist positions/rotations across the sagittal plane
    // and SWAP hand assignments. Never use a negative transform scale on PunkM
    // or the real Colt: that would reverse meshes, normals and ejection side.
    public static Pose MirrorPose(Pose original)
    {
        Pose p = original;
        p.weaponPosition.x = -original.weaponPosition.x;
        p.rightGrip = MirrorGrip(original.leftGrip, original.weaponPosition,
            p.weaponPosition, original.weaponRotation);
        p.leftGrip = MirrorGrip(original.rightGrip, original.weaponPosition,
            p.weaponPosition, original.weaponRotation);
        p.rightRotation = MirrorGripRotation(original.leftRotation, original.weaponRotation);
        p.leftRotation = MirrorGripRotation(original.rightRotation, original.weaponRotation);
        p.rightElbow = Reflect(original.leftElbow);
        p.leftElbow = Reflect(original.rightElbow);
        p.rightFingerCurl = original.leftFingerCurl;
        p.leftFingerCurl = original.rightFingerCurl;
        p.rightThumbCurl = original.leftThumbCurl;
        p.leftThumbCurl = original.rightThumbCurl;
        p.rightThumbSpread = -original.leftThumbSpread;
        p.leftThumbSpread = -original.rightThumbSpread;
        p.rightFingerDegrees = original.leftFingerDegrees;
        p.leftFingerDegrees = original.rightFingerDegrees;
        p.rightThumbDegrees = original.leftThumbDegrees;
        p.leftThumbDegrees = original.rightThumbDegrees;
        p.rightSpreadDegrees = original.leftSpreadDegrees;
        p.leftSpreadDegrees = original.rightSpreadDegrees;
        // Curl inversion describes each physical rig's bone winding, not the
        // role of a hand in the stance. Keep it attached to the actual side.
        p.rightInvertFinger = original.rightInvertFinger;
        p.leftInvertFinger = original.leftInvertFinger;
        p.rightInvertThumb = original.rightInvertThumb;
        p.leftInvertThumb = original.leftInvertThumb;
        return p;
    }

    private static Vector3 Reflect(Vector3 v) { return new Vector3(-v.x, v.y, v.z); }

    private static Vector3 MirrorGrip(Vector3 local, Vector3 sourceAnchor,
        Vector3 mirroredAnchor, Quaternion anchorRotation)
    {
        Vector3 cameraPosition = sourceAnchor + anchorRotation * local;
        return Quaternion.Inverse(anchorRotation) * (Reflect(cameraPosition) - mirroredAnchor);
    }

    private static Quaternion MirrorGripRotation(Quaternion local, Quaternion anchorRotation)
    {
        Quaternion cameraRotation = anchorRotation * local;
        // Reflection S R S for S = diag(-1, 1, 1), expressed as a quaternion.
        Quaternion mirrored = new Quaternion(cameraRotation.x, -cameraRotation.y,
            -cameraRotation.z, cameraRotation.w);
        return Quaternion.Inverse(anchorRotation) * mirrored;
    }

    private static void SetGrip(TwoHandPosePreview.Arm arm, float curl, float thumb, float spread,
        float fingerDegrees, float thumbDegrees, float spreadDegrees, bool invertFingers, bool invertThumb)
    {
        arm.fingerGrip = curl;
        arm.thumbGrip = thumb;
        arm.thumbSplay = spread;
        arm.fingerCurlDegrees = fingerDegrees;
        arm.thumbCurlDegrees = thumbDegrees;
        arm.thumbSplayDegrees = spreadDegrees;
        arm.invertFingerCurl = invertFingers;
        arm.invertThumbCurl = invertThumb;
    }
}
