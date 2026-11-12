using System.Linq;
using UnityEngine;

// Only used by Man01_RigSmokeTest.unity. This is a disposable visual check,
// NOT an animation controller for the player's weapon or a gameplay IK system.
public sealed class Man01RigSmokeTest : MonoBehaviour
{
    public GameObject characterFbx;
    public Material characterMaterial;
    public TacticalGun sourceGun;
    public Camera viewCamera;
    [Min(0.5f)] public float referenceHeight = 1.75f;
    [Min(1f)] public float standDistance = 3.2f;

    private GameObject character;
    private TwoHandPosePreview pose;
    private Transform rightForearm;
    private Transform rightWrist;
    private Transform chest;
    private Quaternion forearmRest;
    private int mode; // 0 rest, 1 elbow, 2 approximate two-hand contact
    private string warning;

    private void Start()
    {
        if (characterFbx == null || sourceGun == null || viewCamera == null)
        {
            warning = "Missing model, Colt or camera reference in smoke test scene.";
            Debug.LogError("[Man01 smoke test] " + warning, this);
            return;
        }

        // A separate stand in front of the FPS player: the original ColtRoot,
        // WeaponAnchor, camera and the user's shooting settings stay untouched.
        Vector3 forward = Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f) forward = transform.forward;
        character = Instantiate(characterFbx);
        character.name = "Man 01 (test stand; not the player)";
        character.transform.SetPositionAndRotation(transform.position + forward * standDistance,
            Quaternion.LookRotation(-forward, Vector3.up));
        foreach (Animator animator in character.GetComponentsInChildren<Animator>(true))
            animator.enabled = false;

        SkinnedMeshRenderer[] skin = character.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (skin.Length == 0)
        {
            warning = "Imported FBX has no SkinnedMeshRenderer; check Unity model import.";
            Debug.LogError("[Man01 smoke test] " + warning, character);
            return;
        }
        // A defensive scene-local assignment even if a different Unity version
        // does not honor the FBX material remap in its importer.
        if (characterMaterial != null)
            foreach (SkinnedMeshRenderer renderer in skin)
            {
                int count = Mathf.Max(1, renderer.sharedMaterials.Length);
                renderer.sharedMaterials = Enumerable.Repeat(characterMaterial, count).ToArray();
            }
        Bounds size = skin[0].bounds;
        foreach (SkinnedMeshRenderer renderer in skin.Skip(1)) size.Encapsulate(renderer.bounds);
        if (size.size.y < 0.01f)
        {
            warning = "Imported character bounds are too small to fit automatically.";
            Debug.LogError("[Man01 smoke test] " + warning, character);
            return;
        }
        float scale = referenceHeight / size.size.y;
        character.transform.localScale *= scale; // scale the WHOLE bound prefab, never reset its bind pose
        size = skin[0].bounds;
        foreach (SkinnedMeshRenderer renderer in skin.Skip(1)) size.Encapsulate(renderer.bounds);
        character.transform.position += Vector3.up * (transform.position.y - 1f - size.min.y);
        Debug.LogFormat("[Man01 smoke test] Source height {0:F3}m; display scale x{1:F3}; reference {2:F2}m; skinned meshes {3}.",
            referenceHeight / scale, scale, size.size.y, skin.Length);

        Transform[] bones = character.GetComponentsInChildren<Transform>(true);
        Transform Bone(string name) { return bones.FirstOrDefault(t => t.name == name); }
        Transform ru = Bone("upper_arm.R"), rl = Bone("forearm.R"), rw = Bone("hand.R");
        Transform lu = Bone("upper_arm.L"), ll = Bone("forearm.L"), lw = Bone("hand.L");
        if (ru == null || rl == null || rw == null || lu == null || ll == null || lw == null)
        {
            warning = "An arm bone is missing on Unity import. Disable Optimize Game Objects for this FBX.";
            Debug.LogError("[Man01 smoke test] " + warning, character);
            return;
        }
        rightForearm = rl;
        rightWrist = rw;
        chest = Bone("spine.003") ?? Bone("spine.002") ?? Bone("spine");
        forearmRest = rightForearm.localRotation;

        // Draw ONLY the existing Colt's render meshes at the stand; scripts,
        // colliders, particles, sound and live weapon state are never duplicated.
        Transform referenceGun = CopyRenderTree(sourceGun.transform, null);
        referenceGun.name = "Colt VISUAL reference (no TacticalGun script)";
        referenceGun.rotation = Quaternion.AngleAxis(180f, Vector3.up) * sourceGun.transform.rotation;
        Renderer[] gunRenderers = referenceGun.GetComponentsInChildren<Renderer>(true);
        Bounds gunBounds = new Bounds(referenceGun.position, Vector3.zero);
        bool hasGunMesh = false;
        foreach (Renderer renderer in gunRenderers)
        {
            if (!renderer.enabled) continue;
            if (!hasGunMesh) { gunBounds = renderer.bounds; hasGunMesh = true; }
            else gunBounds.Encapsulate(renderer.bounds);
        }
        if (!hasGunMesh)
        {
            warning = "ColtRoot has no visible render meshes for the stand reference.";
            Debug.LogWarning("[Man01 smoke test] " + warning, sourceGun);
            return;
        }
        Vector3 desiredGunCenter = (chest != null ? chest.position : size.center + Vector3.up * size.extents.y * 0.2f)
            + character.transform.forward * 0.24f + Vector3.down * 0.1f;
        referenceGun.position += desiredGunCenter - gunBounds.center;
        gunBounds.center = desiredGunCenter;

        Transform goals = new GameObject("Temporary wrist/elbow targets - smoke test only").transform;
        goals.SetParent(character.transform, false);
        pose = character.AddComponent<TwoHandPosePreview>();
        pose.livePreviewInEditMode = false;
        pose.right.upperArm = ru; pose.right.lowerArm = rl; pose.right.wrist = rw;
        pose.left.upperArm = lu; pose.left.lowerArm = ll; pose.left.wrist = lw;
        pose.right.target = MakeGoal("Right wrist (rough)", goals, rw.position, rw.rotation);
        pose.left.target = MakeGoal("Left wrist (rough)", goals, lw.position, lw.rotation);
        pose.right.elbowHint = MakeGoal("Right elbow (rough)", goals,
            rl.position + character.transform.right * 0.16f + Vector3.down * 0.13f, rl.rotation);
        pose.left.elbowHint = MakeGoal("Left elbow (rough)", goals,
            ll.position - character.transform.right * 0.16f + Vector3.down * 0.13f, ll.rotation);
        pose.right.target.position = gunBounds.center + character.transform.right * 0.055f + Vector3.down * 0.065f;
        pose.left.target.position = gunBounds.center - character.transform.right * 0.075f + Vector3.down * 0.025f;
        pose.controlMode = TwoHandPosePreview.JointMode.WristIK;
        pose.CaptureRestPose();
        // Do not let the sandbox solver's own LateUpdate run on top of the
        // elbow-only test. Call it explicitly ONLY in the grip-check mode.
        pose.enabled = false;
    }

    private static Transform MakeGoal(string name, Transform parent, Vector3 position, Quaternion rotation)
    {
        Transform goal = new GameObject(name).transform;
        goal.SetParent(parent, false);
        goal.SetPositionAndRotation(position, rotation);
        return goal;
    }

    private static Transform CopyRenderTree(Transform source, Transform parent)
    {
        Transform copy = new GameObject(source.name).transform;
        if (parent != null)
        {
            copy.SetParent(parent, false);
            copy.localPosition = source.localPosition;
            copy.localRotation = source.localRotation;
            copy.localScale = source.localScale;
        }
        MeshFilter mesh = source.GetComponent<MeshFilter>();
        MeshRenderer renderer = source.GetComponent<MeshRenderer>();
        if (mesh != null && renderer != null && renderer.enabled && source.gameObject.activeInHierarchy)
        {
            copy.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
            MeshRenderer visual = copy.gameObject.AddComponent<MeshRenderer>();
            visual.sharedMaterials = renderer.sharedMaterials;
            visual.shadowCastingMode = renderer.shadowCastingMode;
        }
        foreach (Transform child in source)
            CopyRenderTree(child, copy);
        return copy;
    }

    private void Update()
    {
        if (pose == null) return;
        if (Input.GetKeyDown(KeyCode.F6)) mode = mode == 1 ? 0 : 1;
        if (Input.GetKeyDown(KeyCode.F7)) mode = mode == 2 ? 0 : 2;
        if (Input.GetKeyDown(KeyCode.F8)) mode = 0;
    }

    // Default execution order runs after the older sandbox component's disabled
    // LateUpdate; the production gun's transform is never written here.
    private void LateUpdate()
    {
        if (pose == null) return;
        pose.RestoreRestPose();
        if (mode == 2) { pose.PreviewPose(); return; }
        if (mode != 1 || rightForearm == null || rightWrist == null) return;
        Vector3 from = rightWrist.position - rightForearm.position;
        Vector3 to = (chest != null ? chest.position : character.transform.position + Vector3.up) - rightForearm.position;
        if (from.sqrMagnitude > 0.0001f && to.sqrMagnitude > 0.0001f)
        {
            Quaternion bend = Quaternion.RotateTowards(Quaternion.identity,
                Quaternion.FromToRotation(from, to), 65f);
            rightForearm.rotation = bend * rightForearm.rotation;
        }
    }

    private void OnGUI()
    {
        if (!Application.isPlaying) return;
        GUILayout.BeginArea(new Rect(12, 12, 500, 95), GUI.skin.box);
        GUILayout.Label("MAN 01 / COLT — rig smoke test (not a saved weapon pose)");
        GUILayout.Label("F6: right elbow bend   F7: rough two-hand contact   F8: rest");
        GUILayout.Label(warning ?? (mode == 2 ? "Approximate wrists only: finger grip NOT calibrated."
            : "Original FPS Colt still shoots; stand Colt is render-only."));
        GUILayout.EndArea();
    }
}
