using System;
using UnityEngine;

// Sandbox-only pose solver: IK targets OR independent local rotations of the
// upper arm, forearm and wrist. FBX assets and production scenes are untouched.
[ExecuteAlways]
public class TwoHandPosePreview : MonoBehaviour
{
    public enum JointMode { WristIK, ManualJoints }

    [Serializable]
    public class Arm
    {
        public Transform upperArm;
        public Transform lowerArm;
        public Transform wrist;
        public Transform target;
        public Transform elbowHint;
        [HideInInspector] public Quaternion upperRest;
        [HideInInspector] public Quaternion lowerRest;
        [HideInInspector] public Quaternion wristRest;

        // Rotation offsets in each bone's PARENT space, relative to imported rest.
        // Unlike wrist IK these three angles are independent and survive previews.
        [HideInInspector] public Vector3 shoulderAngles;
        [HideInInspector] public Vector3 forearmAngles;
        [HideInInspector] public Vector3 wristAngles;

        [HideInInspector] public float fingerGrip;
        [HideInInspector] public float thumbGrip;
        [HideInInspector] public float thumbSplay; // -1..1, independent of thumb bend
        [HideInInspector] public float fingerCurlDegrees = 70f;
        [HideInInspector] public float thumbCurlDegrees = 35f;
        [HideInInspector] public float thumbSplayDegrees = 40f;
        [HideInInspector] public bool invertFingerCurl;
        [HideInInspector] public bool invertThumbCurl;
        [HideInInspector] public Transform thumb1, thumb2, thumb3, index1, index2, index3;
        [HideInInspector] public Transform middle1, middle2, middle3, ring1, ring2, ring3;
        [HideInInspector] public Transform pinky1, pinky2, pinky3;
        // Original rest values are kept for compatibility with already tuned scenes.
        [HideInInspector] public Quaternion thumbRest, indexRest, middleRest, ringRest, pinkyRest;
        [SerializeField, HideInInspector] private bool hasFingerRest;
        [HideInInspector] public Quaternion thumbProxRest, indexProxRest, middleProxRest, ringProxRest, pinkyProxRest;
        [SerializeField, HideInInspector] private bool hasProximalRest;

        public void BindFingerRest(bool recapture = false)
        {
            if (wrist == null) return;
            if (!recapture && hasFingerRest && hasProximalRest &&
                thumb1 != null && thumb2 != null && thumb3 != null &&
                index1 != null && index2 != null && index3 != null &&
                middle1 != null && middle2 != null && middle3 != null &&
                ring1 != null && ring2 != null && ring3 != null &&
                pinky1 != null && pinky2 != null && pinky3 != null) return;

            string side = wrist.name.EndsWith(".R", StringComparison.Ordinal) ? ".R" : ".L";
            Transform[] bones = wrist.GetComponentsInChildren<Transform>(true);
            Transform Find(string name)
            {
                foreach (Transform bone in bones)
                    if (bone.name == name + side) return bone;
                return null;
            }
            thumb1 = Find("Thumb1"); thumb2 = Find("Thumb2"); thumb3 = Find("Thumb3");
            index1 = Find("Index1"); index2 = Find("Index2"); index3 = Find("Index3");
            middle1 = Find("Middle1"); middle2 = Find("Middle2"); middle3 = Find("Middle3");
            ring1 = Find("Ring1"); ring2 = Find("Ring2"); ring3 = Find("Ring3");
            pinky1 = Find("Pinky1"); pinky2 = Find("Pinky2"); pinky3 = Find("Pinky3");
            if (thumb1 == null || thumb2 == null || thumb3 == null ||
                index1 == null || index2 == null || index3 == null ||
                middle1 == null || middle2 == null || middle3 == null ||
                ring1 == null || ring2 == null || ring3 == null ||
                pinky1 == null || pinky2 == null || pinky3 == null) return;

            // Old scenes contain rest rotations for Finger1 but not Finger2.
            // Do NOT recapture Finger1 from an already previewed (curled) scene.
            if (recapture || !hasFingerRest)
            {
                thumbRest = thumb1.localRotation;
                indexRest = index1.localRotation;
                middleRest = middle1.localRotation;
                ringRest = ring1.localRotation;
                pinkyRest = pinky1.localRotation;
            }
            if (recapture || !hasProximalRest)
            {
                thumbProxRest = thumb2.localRotation;
                indexProxRest = index2.localRotation;
                middleProxRest = middle2.localRotation;
                ringProxRest = ring2.localRotation;
                pinkyProxRest = pinky2.localRotation;
            }
            hasFingerRest = true;
            hasProximalRest = true;
            if (fingerCurlDegrees < 1f) fingerCurlDegrees = 70f;
            if (thumbCurlDegrees < 1f) thumbCurlDegrees = 35f;
            if (thumbSplayDegrees < 1f) thumbSplayDegrees = 40f;
        }

        public void RestoreFingers()
        {
            if (hasFingerRest)
            {
                if (thumb1 != null) thumb1.localRotation = thumbRest;
                if (index1 != null) index1.localRotation = indexRest;
                if (middle1 != null) middle1.localRotation = middleRest;
                if (ring1 != null) ring1.localRotation = ringRest;
                if (pinky1 != null) pinky1.localRotation = pinkyRest;
            }
            if (hasProximalRest)
            {
                if (thumb2 != null) thumb2.localRotation = thumbProxRest;
                if (index2 != null) index2.localRotation = indexProxRest;
                if (middle2 != null) middle2.localRotation = middleProxRest;
                if (ring2 != null) ring2.localRotation = ringProxRest;
                if (pinky2 != null) pinky2.localRotation = pinkyProxRest;
            }
        }

        public Transform[] FingerBones
        {
            get { return new[] { thumb1, thumb2, index1, index2, middle1, middle2,
                                 ring1, ring2, pinky1, pinky2 }; }
        }
    }

    public Arm right = new Arm();
    public Arm left = new Arm();
    [HideInInspector] public JointMode controlMode;
    [Range(0f, 1f)] public float poseWeight = 1f;
    [Tooltip("Update the rig immediately as joints or IK targets change in a sandbox scene.")]
    public bool livePreviewInEditMode = true;
    [SerializeField, HideInInspector] private bool hasRestPose;

    public bool HasRestPose { get { return hasRestPose; } }

    public Transform[] DrivenBones
    {
        get
        {
            return new[] { right.upperArm, right.lowerArm, right.wrist,
                           left.upperArm, left.lowerArm, left.wrist,
                           right.thumb1, right.thumb2, right.index1, right.index2,
                           right.middle1, right.middle2, right.ring1, right.ring2, right.pinky1, right.pinky2,
                           left.thumb1, left.thumb2, left.index1, left.index2,
                           left.middle1, left.middle2, left.ring1, left.ring2, left.pinky1, left.pinky2 };
        }
    }

    private static bool IsReady(Arm arm)
    {
        return arm != null && arm.upperArm != null && arm.lowerArm != null &&
               arm.wrist != null && arm.target != null && arm.elbowHint != null;
    }

    public void CaptureRestPose()
    {
        if (!IsReady(right) || !IsReady(left)) return;
        Capture(right);
        Capture(left);
        hasRestPose = true;
    }

    private static void Capture(Arm arm)
    {
        arm.upperRest = arm.upperArm.localRotation;
        arm.lowerRest = arm.lowerArm.localRotation;
        arm.wristRest = arm.wrist.localRotation;
        arm.BindFingerRest(true);
    }

    public void RestoreRestPose()
    {
        if (!hasRestPose) return;
        Restore(right);
        Restore(left);
    }

    private static void Restore(Arm arm)
    {
        if (arm.upperArm != null) arm.upperArm.localRotation = arm.upperRest;
        if (arm.lowerArm != null) arm.lowerArm.localRotation = arm.lowerRest;
        if (arm.wrist != null) arm.wrist.localRotation = arm.wristRest;
        arm.RestoreFingers();
    }

    // Start manual editing without jumping to T-pose. The current solved pose
    // becomes the initial three independent joint rotations on each side.
    public void ConvertCurrentPoseToManual()
    {
        if (!hasRestPose || !IsReady(right) || !IsReady(left)) return;
        PreviewPose();
        StoreOffsets(right);
        StoreOffsets(left);
        poseWeight = 1f; // Offsets were captured from the already blended pose.
        controlMode = JointMode.ManualJoints;
        PreviewPose();
    }

    private static void StoreOffsets(Arm arm)
    {
        arm.shoulderAngles = SignedEuler(Quaternion.Inverse(arm.upperRest) * arm.upperArm.localRotation);
        arm.forearmAngles = SignedEuler(Quaternion.Inverse(arm.lowerRest) * arm.lowerArm.localRotation);
        arm.wristAngles = SignedEuler(Quaternion.Inverse(arm.wristRest) * arm.wrist.localRotation);
    }

    private static Vector3 SignedEuler(Quaternion rotation)
    {
        Vector3 euler = rotation.eulerAngles;
        return new Vector3(Mathf.DeltaAngle(0f, euler.x), Mathf.DeltaAngle(0f, euler.y),
                           Mathf.DeltaAngle(0f, euler.z));
    }

    public void PreviewPose()
    {
        if (!hasRestPose || !IsReady(right) || !IsReady(left)) return;
        right.BindFingerRest();
        left.BindFingerRest();
        RestoreRestPose();
        if (controlMode == JointMode.ManualJoints)
        {
            ApplyManual(right, poseWeight);
            ApplyManual(left, poseWeight);
        }
        else
        {
            SolveArm(right, poseWeight);
            SolveArm(left, poseWeight);
        }
        CurlProximalFingers(right, poseWeight);
        CurlProximalFingers(left, poseWeight);
    }

    private static void ApplyManual(Arm arm, float weight)
    {
        arm.upperArm.localRotation = Quaternion.Slerp(arm.upperRest,
            arm.upperRest * Quaternion.Euler(arm.shoulderAngles), weight);
        arm.lowerArm.localRotation = Quaternion.Slerp(arm.lowerRest,
            arm.lowerRest * Quaternion.Euler(arm.forearmAngles), weight);
        arm.wrist.localRotation = Quaternion.Slerp(arm.wristRest,
            arm.wristRest * Quaternion.Euler(arm.wristAngles), weight);
    }

    private void LateUpdate()
    {
        // Editor previews are updated by the editor tool only when values change.
        if (Application.isPlaying) PreviewPose();
    }

    private static void SolveArm(Arm arm, float weight)
    {
        if (weight <= 0f) return;

        Vector3 shoulder = arm.upperArm.position;
        Vector3 elbow = arm.lowerArm.position;
        Vector3 wrist = arm.wrist.position;
        float upperLength = Vector3.Distance(shoulder, elbow);
        float lowerLength = Vector3.Distance(elbow, wrist);
        Vector3 toTarget = arm.target.position - shoulder;
        if (upperLength < 0.0001f || lowerLength < 0.0001f || toTarget.sqrMagnitude < 0.000001f) return;

        Vector3 direction = toTarget.normalized;
        float reach = Mathf.Clamp(toTarget.magnitude,
            Mathf.Abs(upperLength - lowerLength) + 0.0001f,
            upperLength + lowerLength - 0.0001f);

        Vector3 hint = arm.elbowHint.position - shoulder;
        Vector3 bend = Vector3.ProjectOnPlane(hint, direction);
        if (bend.sqrMagnitude < 0.000001f)
            bend = Vector3.ProjectOnPlane(arm.upperArm.right, direction);
        if (bend.sqrMagnitude < 0.000001f)
            bend = Vector3.Cross(direction, Vector3.up);
        bend.Normalize();

        float along = (upperLength * upperLength - lowerLength * lowerLength + reach * reach) / (2f * reach);
        float away = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
        Vector3 desiredElbow = shoulder + direction * along + bend * away;
        Vector3 desiredWrist = shoulder + direction * reach;

        arm.upperArm.rotation = Quaternion.FromToRotation(elbow - shoulder, desiredElbow - shoulder) * arm.upperArm.rotation;
        Vector3 newForearm = arm.wrist.position - arm.lowerArm.position;
        Vector3 desiredForearm = desiredWrist - arm.lowerArm.position;
        if (newForearm.sqrMagnitude > 0.000001f && desiredForearm.sqrMagnitude > 0.000001f)
            arm.lowerArm.rotation = Quaternion.FromToRotation(newForearm, desiredForearm) * arm.lowerArm.rotation;
        arm.wrist.rotation = arm.target.rotation;

        Quaternion upperSolved = arm.upperArm.localRotation;
        Quaternion lowerSolved = arm.lowerArm.localRotation;
        Quaternion wristSolved = arm.wrist.localRotation;
        arm.upperArm.localRotation = Quaternion.Slerp(arm.upperRest, upperSolved, weight);
        arm.lowerArm.localRotation = Quaternion.Slerp(arm.lowerRest, lowerSolved, weight);
        arm.wrist.localRotation = Quaternion.Slerp(arm.wristRest, wristSolved, weight);
    }

    private static void CurlProximalFingers(Arm arm, float weight)
    {
        if (arm.index2 == null || arm.index3 == null || arm.pinky2 == null || arm.pinky3 == null ||
            arm.middle2 == null || arm.middle3 == null || arm.ring2 == null || arm.ring3 == null ||
            arm.thumb1 == null || arm.thumb2 == null || arm.thumb3 == null) return;

        // PunkM: Finger1 starts at the wrist and spans most of the PALM (~12 cm).
        // Finger2 starts at the actual knuckle. Never bend Finger1 for a grip:
        // doing so visibly folds the palm in half, rather than curling fingers.
        Vector3 across = arm.index2.position - arm.pinky2.position;
        Vector3 length = arm.middle2.position - arm.wrist.position;
        Vector3 palmNormal = Vector3.Cross(across, length);
        if (palmNormal.sqrMagnitude < 0.000001f)
            palmNormal = arm.wrist.up;
        palmNormal.Normalize();

        float amount = Mathf.Clamp01(arm.fingerGrip * weight);
        if (amount > 0f)
        {
            Vector3 inward = arm.invertFingerCurl ? -palmNormal : palmNormal;
            float bend = Mathf.Max(1f, arm.fingerCurlDegrees) * amount;
            Bend(arm.index2, arm.index3, arm.indexProxRest, inward, bend);
            Bend(arm.middle2, arm.middle3, arm.middleProxRest, inward, bend);
            Bend(arm.ring2, arm.ring3, arm.ringProxRest, inward, bend);
            Bend(arm.pinky2, arm.pinky3, arm.pinkyProxRest, inward, bend);
        }

        // Thumb1 pivots at the palm: use it only to spread/oppose the thumb
        // sideways IN the palm plane. Thumb2 bends independently across the palm.
        float spread = Mathf.Clamp(arm.thumbSplay, -1f, 1f) * weight;
        if (Mathf.Abs(spread) > 0f)
        {
            Vector3 localNormal = arm.thumb1.parent.InverseTransformDirection(palmNormal);
            arm.thumb1.localRotation = Quaternion.AngleAxis(spread * Mathf.Max(1f, arm.thumbSplayDegrees),
                localNormal) * arm.thumbRest;
        }
        float thumbAmount = Mathf.Clamp01(arm.thumbGrip * weight);
        if (thumbAmount > 0f)
        {
            Vector3 inward = arm.invertThumbCurl ? -palmNormal : palmNormal;
            Bend(arm.thumb2, arm.thumb3, arm.thumbProxRest, inward,
                Mathf.Max(1f, arm.thumbCurlDegrees) * thumbAmount);
        }
    }

    private static void Bend(Transform proximal, Transform second, Quaternion rest, Vector3 inward, float degrees)
    {
        if (proximal == null || second == null || inward.sqrMagnitude < 0.0001f) return;
        Vector3 fingerDirection = second.position - proximal.position;
        Vector3 axis = Vector3.Cross(fingerDirection, inward).normalized;
        if (axis.sqrMagnitude < 0.0001f) return;
        Vector3 localAxis = proximal.parent.InverseTransformDirection(axis);
        proximal.localRotation = Quaternion.AngleAxis(degrees, localAxis) * rest;
    }

    private void OnDrawGizmos()
    {
        if (controlMode == JointMode.ManualJoints) return;
        DrawTargets(right, new Color(0.4f, 0.8f, 1f));
        DrawTargets(left, new Color(1f, 0.7f, 0.3f));
    }

    private static void DrawTargets(Arm arm, Color color)
    {
        if (arm == null || arm.target == null) return;
        Gizmos.color = color;
        Gizmos.DrawWireSphere(arm.target.position, 0.025f);
        if (arm.upperArm != null) Gizmos.DrawLine(arm.upperArm.position, arm.target.position);
        if (arm.elbowHint != null)
        {
            Gizmos.DrawWireSphere(arm.elbowHint.position, 0.018f);
            if (arm.lowerArm != null) Gizmos.DrawLine(arm.lowerArm.position, arm.elbowHint.position);
        }
    }
}
