using System;
using UnityEngine;

// Sandbox-only pose solver. It does not move the camera, gun, or production
// TacticalGun. Targets are separate scene objects, never FBX bone children.
[ExecuteAlways]
public class TwoHandPosePreview : MonoBehaviour
{
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
    }

    public Arm right = new Arm();
    public Arm left = new Arm();
    [Range(0f, 1f)] public float poseWeight = 1f;
    [SerializeField, HideInInspector] private bool hasRestPose;

    public bool HasRestPose { get { return hasRestPose; } }

    public Transform[] DrivenBones
    {
        get { return new[] { right.upperArm, right.lowerArm, right.wrist,
                           left.upperArm, left.lowerArm, left.wrist }; }
    }

    private bool IsReady(Arm arm)
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
    }

    public void PreviewPose()
    {
        if (!hasRestPose || !IsReady(right) || !IsReady(left)) return;
        RestoreRestPose();
        SolveArm(right, poseWeight);
        SolveArm(left, poseWeight);
    }

    private void LateUpdate()
    {
        // Explicit button in Edit Mode: no permanent, per-frame FBX overrides.
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

        // Use the hint only to choose which side the elbow bends towards.
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

        // In the imported rig each bone is the pivot of its child. Rotating the
        // shoulder moves the elbow; rotating the elbow moves the wrist.
        arm.upperArm.rotation = Quaternion.FromToRotation(elbow - shoulder, desiredElbow - shoulder) * arm.upperArm.rotation;
        Vector3 newForearm = arm.wrist.position - arm.lowerArm.position;
        Vector3 desiredForearm = desiredWrist - arm.lowerArm.position;
        if (newForearm.sqrMagnitude > 0.000001f && desiredForearm.sqrMagnitude > 0.000001f)
            arm.lowerArm.rotation = Quaternion.FromToRotation(newForearm, desiredForearm) * arm.lowerArm.rotation;
        arm.wrist.rotation = arm.target.rotation;

        // Blend the solved bone rotations against the captured Rest Pose. This is
        // intentionally independent of the old one-handed FBX animations.
        Quaternion upperSolved = arm.upperArm.localRotation;
        Quaternion lowerSolved = arm.lowerArm.localRotation;
        Quaternion wristSolved = arm.wrist.localRotation;
        arm.upperArm.localRotation = Quaternion.Slerp(arm.upperRest, upperSolved, weight);
        arm.lowerArm.localRotation = Quaternion.Slerp(arm.lowerRest, lowerSolved, weight);
        arm.wrist.localRotation = Quaternion.Slerp(arm.wristRest, wristSolved, weight);
    }

    private void OnDrawGizmosSelected()
    {
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
