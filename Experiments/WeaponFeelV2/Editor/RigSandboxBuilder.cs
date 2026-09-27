// Isolated editor-only setup: never changes SampleScene or the imported FBX.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RigSandboxBuilder
{
    private const string MenuPath = "Tools/Weapon Feel V2/Create PunkM rig sandbox";

    [MenuItem(MenuPath)]
    private static void Create()
    {
        // The active production scene may contain unsaved Inspector values.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string[] candidates = AssetDatabase.FindAssets("PunkM")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith("/Models/PunkM.fbx", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (candidates.Length != 1)
        {
            EditorUtility.DisplayDialog("Weapon Feel V2",
                "Expected exactly one Assets/.../Models/PunkM.fbx. Found: " + candidates.Length +
                ". Import the FBX first, then retry.", "OK");
            return;
        }

        string modelPath = candidates[0];
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null)
        {
            EditorUtility.DisplayDialog("Weapon Feel V2", "Could not import the PunkM FBX as a model.", "OK");
            return;
        }

        string assetsRoot = Path.GetDirectoryName(Path.GetDirectoryName(modelPath));
        if (string.IsNullOrEmpty(assetsRoot)) return;
        string outputDirectory = (assetsRoot + "/Experiments/WeaponFeelV2").Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(outputDirectory))
        {
            EditorUtility.DisplayDialog("Weapon Feel V2",
                "Missing " + outputDirectory + ". Copy the Experiments folder into the same Assets root as Models.", "OK");
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject character = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
        if (character == null)
        {
            Debug.LogError("[Weapon Feel V2] Could not instantiate the FBX in a new scene.");
            return;
        }
        character.name = "PunkM - skeleton reference (leave intact)";
        character.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        character.transform.localScale = Vector3.one;

        SkinnedMeshRenderer[] skinned = character.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        Transform[] bones = character.GetComponentsInChildren<Transform>(true);
        string[] required = { "Hips", "Chest", "Shoulder.L", "UpperArm.L", "LowerArm.L", "Wrist.L",
                              "Shoulder.R", "UpperArm.R", "LowerArm.R", "Wrist.R" };
        string[] missing = required.Where(name => !bones.Any(bone => bone.name == name)).ToArray();
        if (missing.Length > 0 || skinned.Length == 0)
            Debug.LogWarning("[Weapon Feel V2] Check the import. Missing bones: " +
                string.Join(", ", missing) + "; skinned renderers: " + skinned.Length, character);

        // Fit the reference camera to the imported mesh regardless of FBX scale.
        Bounds bounds = new Bounds(character.transform.position, Vector3.zero);
        bool hasBounds = false;
        foreach (SkinnedMeshRenderer renderer in skinned)
        {
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (!hasBounds || bounds.size.y < 0.001f)
            bounds = new Bounds(Vector3.up, new Vector3(1f, 2f, 1f));

        GameObject cameraObject = new GameObject("Rig preview camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.tag = "MainCamera";
        camera.fieldOfView = 45f;
        float distance = Mathf.Max(bounds.size.y * 1.9f, bounds.extents.z + 0.1f);
        camera.transform.position = bounds.center + new Vector3(0f, bounds.size.y * 0.05f, distance);
        camera.transform.LookAt(bounds.center);
        camera.nearClipPlane = Mathf.Max(0.01f, distance / 1000f);
        camera.farClipPlane = Mathf.Max(100f, distance * 10f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);

        GameObject lightObject = new GameObject("Rig preview light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.3f;
        lightObject.transform.rotation = Quaternion.Euler(35f, -35f, 0f);

        // Generate a unique scene name so running this command cannot overwrite
        // an existing test scene or any tuned production scene.
        string target = AssetDatabase.GenerateUniqueAssetPath(outputDirectory + "/PunkM_RigSandbox.unity");
        if (!EditorSceneManager.SaveScene(scene, target))
        {
            Debug.LogError("[Weapon Feel V2] Unable to save rig sandbox: " + target);
            return;
        }
        Selection.activeGameObject = character;
        EditorGUIUtility.PingObject(character);

        ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(modelPath)
            .OfType<AnimationClip>().Where(clip => !clip.name.StartsWith("__preview__")).ToArray();
        Debug.Log("[Weapon Feel V2] Created " + target + ". Skinned meshes: " + skinned.Length +
            ", transforms: " + bones.Length + ", clips: " + clips.Length +
            ", rig: " + (importer != null ? importer.animationType.ToString() : "unknown") +
            ". Check height (in meters), Rest Pose and wrist/elbow deformation BEFORE adding the gun.", character);
    }
}
