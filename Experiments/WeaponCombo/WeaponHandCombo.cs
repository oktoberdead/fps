using System;
using UnityEngine;

// Prototype integration, confined to a COPY of SampleScene. TacticalGun still
// owns firing, slide, ammunition, sway, bob and recoil; this script only makes
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
    public KeyCode shortAdsKey = KeyCode.Alpha1;
    public KeyCode longAdsKey = KeyCode.Alpha2;
    [Tooltip("Follow camera freelook with only part of the upper torso; legs still face movement direction.")]
    [Range(0f, 1f)] public float torsoFreelookShare = 0.35f;
    [Range(0f, 70f)] public float maxTorsoYaw = 35f;
    private Quaternion chestRest;
    private float adsBlend;
    private float distanceBlend;
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
        distanceBlend = adsDistance;
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
        Pose pose = Pose.Blend(hip, Pose.Blend(shortADS, longADS, distanceBlend), adsBlend);
        if (chest != null && torsoFreelookShare > 0f)
        {
            float yaw = Mathf.DeltaAngle(0f, viewCamera.transform.localEulerAngles.y);
            float partial = Mathf.Clamp(yaw * torsoFreelookShare, -maxTorsoYaw, maxTorsoYaw);
            // Apply a world-up yaw in the chest's parent space; don't spin the hips.
            Quaternion worldRest = chest.parent.rotation * chestRest;
            chest.rotation = Quaternion.AngleAxis(partial, transform.up) * worldRest;
        }
        else if (chest != null) chest.localRotation = chestRest;
        ApplyPose(pose);
    }

    // Invoked by the editor tool only in the isolated WeaponCombo scene. Lets a
    // user move the grip calibration and see the arm deformation without Play.
    public void PreviewPoseInEditor()
    {
        if (Application.isPlaying || arms == null || gunGripFrame == null || weaponAnchor == null) return;
        ApplyPose(Pose.Blend(hip, Pose.Blend(shortADS, longADS, previewDistance), previewADS));
    }

    private void ApplyPose(Pose pose)
    {
        // Move the entire assembled gun between authored camera-relative stances;
        // don't overwrite ColtRoot, whose TacticalGun motion adds ADS, sway & recoil.
        weaponAnchor.localPosition = pose.weaponPosition;
        weaponAnchor.localRotation = pose.weaponRotation;
        arms.right.target.SetPositionAndRotation(
            gunGripFrame.TransformPoint(pose.rightGrip), gunGripFrame.rotation * pose.rightRotation);
        arms.left.target.SetPositionAndRotation(
            gunGripFrame.TransformPoint(pose.leftGrip), gunGripFrame.rotation * pose.leftRotation);
        arms.right.elbowHint.position = arms.transform.TransformPoint(pose.rightElbow);
        arms.left.elbowHint.position = arms.transform.TransformPoint(pose.leftElbow);
        SetGrip(arms.right, pose.rightFingerCurl, pose.rightThumbCurl, pose.rightThumbSpread,
            pose.rightFingerDegrees, pose.rightThumbDegrees, pose.rightSpreadDegrees,
            pose.rightInvertFinger, pose.rightInvertThumb);
        SetGrip(arms.left, pose.leftFingerCurl, pose.leftThumbCurl, pose.leftThumbSpread,
            pose.leftFingerDegrees, pose.leftThumbDegrees, pose.leftSpreadDegrees,
            pose.leftInvertFinger, pose.leftInvertThumb);
        // TacticalGun ran earlier in LateUpdate: arms now track its final recoil/sway.
        arms.PreviewPose();
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
