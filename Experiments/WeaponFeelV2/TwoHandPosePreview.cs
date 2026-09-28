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
        [HideInInspector] public float fingerCurlDegrees = 70f;
        [HideInInspector] public float thumbCurlDegrees = 35f;
        [HideInInspector] public bool invertFingerCurl;
        [HideInInspector] public Transform thumb1, thumb2, index1, index2, middle1, middle2;
        [HideInInspector] public Transform ring1, ring2, pinky1, pinky2;
        [HideInInspector] public Quaternion thumbRest, indexRest, middleRest, ringRest, pinkyRest;
        [SerializeField, HideInInspector] private bool hasFingerRest;

        public void BindFingerRest(bool recapture = false)
        {
            if (wrist == null) return;
            if (!recapture && hasFingerRest && thumb1 != null && index1 != null &&
                middle1 != null && ring1 != null && pinky1 != null) return;
            hasFingerRest = false;
            string side = wrist.name.EndsWith(".R", StringComparison.Ordinal) ? ".R" : ".L";
            Transform[] bones = wrist.GetComponentsInChildren<Transform>(true);
            Transform Find(string name)
            {
                foreach (Transform bone in bones)
                    if (bone.name == name + side) return bone;
                return null;
            }
            thumb1 = Find("Thumb1"); thumb2 = Find("Thumb2");
            index1 = Find("Index1"); index2 = Find("Index2");
            middle1 = Find("Middle1"); middle2 = Find("Middle2");
            ring1 = Find("Ring1"); ring2 = Find("Ring2");
            pinky1 = Find("Pinky1"); pinky2 = Find("Pinky2");
            if (thumb1 == null || thumb2 == null || index1 == null || index2 == null ||
                middle1 == null || middle2 == null || ring1 == null || ring2 == null ||
                pinky1 == null || pinky2 == null) return;
            thumbRest = thumb1.localRotation;
            indexRest = index1.localRotation;
            middleRest = middle1.localRotation;
            ringRest = ring1.localRotation;
            pinkyRest = pinky1.localRotation;
            hasFingerRest = true;
            // Old serialized scenes did not contain these new fields.
            if (fingerCurlDegrees < 1f) fingerCurlDegrees = 70f;
            if (thumbCurlDegrees < 1f) thumbCurlDegrees = 35f;
        }

        public void RestoreFingers()
        {
            if (!hasFingerRest || thumb1 == null || index1 == null || middle1 == null ||
                ring1 == null || pinky1 == null) return;
            thumb1.localRotation = thumbRest;
            index1.localRotation = indexRest;
            middle1.localRotation = middleRest;
            ring1.localRotation = ringRest;
            pinky1.localRotation = pinkyRest;
        }

        public Transform[] FingerBones
        {
            get { return new[] { thumb1, index1, middle1, ring1, pinky1 }; }
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
                           right.thumb1, right.index1, right.middle1, right.ring1, right.pinky1,
                           left.thumb1, left.index1, left.middle1, left.ring1, left.pinky1 };
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
        if (arm.index1 == null || arm.pinky1 == null || arm.middle1 == null || arm.thumb1 == null) return;
        float amount = Mathf.Clamp01(arm.fingerGrip * weight);
        if (amount > 0f)
        {
            // Plane of the palm, derived from knuckle positions after solving the
            // wrist. The sign can be flipped per hand for differently exported rigs.
            Vector3 across = arm.index1.position - arm.pinky1.position;
            Vector3 length = arm.middle1.position - arm.wrist.position;
            Vector3 inward = Vector3.Cross(across, length).normalized;
            if (arm.invertFingerCurl) inward = -inward;
            float bend = Mathf.Max(1f, arm.fingerCurlDegrees) * amount;
            Bend(arm.index1, arm.index2, arm.indexRest, inward, bend);
            Bend(arm.middle1, arm.middle2, arm.middleRest, inward, bend);
            Bend(arm.ring1, arm.ring2, arm.ringRest, inward, bend);
            Bend(arm.pinky1, arm.pinky2, arm.pinkyRest, inward, bend);
        }

        float thumbAmount = Mathf.Clamp01(arm.thumbGrip * weight);
        if (thumbAmount > 0f)
        {
            Vector3 from = arm.thumb2.position - arm.thumb1.position;
            Vector3 toward = arm.index1.position - arm.thumb1.position;
            if (from.sqrMagnitude > 0.000001f && toward.sqrMagnitude > 0.000001f)
            {
                Quaternion fold = Quaternion.RotateTowards(Quaternion.identity,
                    Quaternion.FromToRotation(from, toward), Mathf.Max(1f, arm.thumbCurlDegrees) * thumbAmount);
                arm.thumb1.rotation = fold * arm.thumb1.rotation;
            }
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
