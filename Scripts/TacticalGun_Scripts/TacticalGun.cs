using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
public partial class TacticalGun : MonoBehaviour
{
    [System.Serializable]
    public struct DirectionalBias
    {
        public Vector3 posBase;
        public Vector3 posMin;
        public Vector3 posMax;
    }

    public enum HammerAxis { LocalX, LocalY, LocalZ }

    [Header("0. Keybindings / Controls")]
    public KeyCode fireKey = KeyCode.Mouse0;
    public KeyCode adsKey = KeyCode.Mouse1;
    public KeyCode slideKey = KeyCode.Mouse4;
    public KeyCode reloadKey = KeyCode.R;
    public KeyCode laserToggleKey = KeyCode.T;
    public KeyCode hammerKey = KeyCode.V;
    public KeyCode modifierKey = KeyCode.LeftShift;
    public KeyCode bulletTimeKey = KeyCode.G;

    [Header("1. Weapon Parts")]
    public Transform slide;
    public Transform hammer;
    public Transform trigger;
    public Transform firePoint;

    [Header("2. Base Transforms")]
    public Vector3 slideBasePos;
    [HideInInspector] public Quaternion hammerBaseRotation = Quaternion.identity;
    [HideInInspector] public Quaternion triggerBaseRotation = Quaternion.identity;
    [HideInInspector] public Vector3 triggerBasePos = Vector3.zero;

    [Header("3. Tactical Timings & Speeds")]
    public float hammerDropSpeed = 20f;
    public float hammerCockSpeed = 45f;
    public float ignitionDelay = 0.005f;
    [Min(0.005f)] public float triggerPullDuration = 0.045f;
    [Range(0.05f, 1f)] public float triggerBreakFraction = 0.45f;
    [Tooltip("Time after releasing the button before the trigger starts resetting.")]
    public float triggerResetDelay = 0.045f;
    [Min(0.005f)] public float triggerReturnDuration = 0.05f;
    public float slideBlowbackSpeed = 33f;
    public float slideReturnSpeed = 20f;

    [Header("4. Hardware Settings")]
    public HammerAxis hammerLocalAxis = HammerAxis.LocalZ;
    public float hammerCockedAngle = -65f;
    [Space]
    public HammerAxis triggerLocalAxis = HammerAxis.LocalX;
    public float triggerPullAngle = 0f;
    public Vector3 triggerPullOffset = new Vector3(0f, 0f, -0.005f);

    [Header("5. Slide Settings")]
    public Vector3 slideRecoilOffset = new Vector3(0.047f, 0, 0);

    [Header("6. Gun Positions (Idle / ADS / Slide Pull)")]
    public Vector3 baseIdlePos;
    public Vector3 baseIdleRot;
    [Space]
    public Vector3 adsPos;
    public Vector3 adsRot;
    [Space]
    public Vector3 slidePullPos;
    public Vector3 slidePullRot;
    public float adsSpeed = 17f;

    [Header("6.1. Interactive Press Check (Мышь)")]
    public float inspectCameraSlowdown = 0.15f;
    [Tooltip("Mouse Y sensitivity for dragging the slide by hand. Lower it for finer control.")]
    [UnityEngine.Serialization.FormerlySerializedAs("inspectMouseYSens")]
    [Min(0.001f)] public float slideDragSensitivity = 0.25f;
    public float inspectMouseXSens = 8.0f;
    public float inspectMaxSwayAngle = 25f;

    [Header("7. Recoil Ceilings (Асимметричные лимиты)")]
    public float globalRecoilMultiplier = 1.0f;
    public float posRecoilMultiplier = 0.7f;
    public float rotRecoilMultiplier = 1.0f;
    [Space]
    public Vector3 minPosRecoil = new Vector3(-0.1f, -0.05f, -0.5f);
    public Vector3 maxPosRecoil = new Vector3(0.5f, 5f, 10f);
    public Vector3 minRotRecoil = new Vector3(-100f, -100f, -100f);
    public Vector3 maxRotRecoil = new Vector3(100f, 100f, 100f);

    [Header("7.1 Progressive Recoil (Парабола)")]
    [Tooltip("Maximum EXTRA kick in a fast shot series: 0.5 means up to +50% before recoil ceilings.")]
    public float progressiveRecoilMultiplier = 0.5f;
    [Tooltip("Shape of the buildup: >1 delays the increase until later shots.")]
    [Min(0.01f)] public float progressiveRecoilCurve = 2.0f;
    [Range(0f, 1f)] public float progressiveRecoilBuildPerShot = 0.5f;
    [Min(0f)] public float progressiveRecoilDecayPerSecond = 0.45f;
    [Tooltip("Print buildup and applied factor on each shot for tuning.")]
    public bool debugProgressiveRecoil;

    [Header("8. POSITIONAL RECOIL (HIP & ADS)")]
    public Vector3 posKickHip = new Vector3(0.25f, 0.15f, -0.3f);
    public Vector3 posKickHipMin = new Vector3(0.1f, 0.1f, -0.7f);
    public Vector3 posKickHipMax = new Vector3(0.4f, 0.5f, 0.1f);
    public Vector3 posKickADS = new Vector3(0.12f, 0.04f, 0f);
    public Vector3 posKickADSMin = new Vector3(0.02f, 0.02f, -0.07f);
    public Vector3 posKickADSMax = new Vector3(0.19f, 0.07f, 0.07f);
    public float posSnappiness = 34f;
    public float posReturnSpeed = 58.7f;

    [Header("9. ROTATIONAL RECOIL (HIP & ADS)")]
    public Vector3 rotAngleHip = new Vector3(-20f, 40f, -62.2f);
    public Vector3 rotAngleHipMin = new Vector3(-50f, 65f, -89.9f);
    public Vector3 rotAngleHipMax = new Vector3(45f, -30f, -46.2f);
    public Vector3 rotAngleADS = new Vector3(0f, 0f, -25.6f);
    public Vector3 rotAngleADSMin = new Vector3(-82.8f, -52.5f, -65f);
    public Vector3 rotAngleADSMax = new Vector3(82.8f, 52.5f, -15f);
    public float rotSnappiness = 66f;
    public float rotReturnSpeed = 149.8f;

    [Header("10. Sway & Bobbing Constants")]
    public float mouseSwayIdle = 1.0f;
    public float mouseSwayWalk = 1.2f;
    public float mouseSwaySprint = 1.55f;
    public float mouseSwayAdsIdle = 0.1f;
    public float mouseSwayAdsWalk = 0.15f;
    public float mouseSwayAdsSprint = 0.2f;
    public float moveSwayWalk = 1.0f;
    public float moveSwaySprint = 1.5f;
    public float moveSwayAdsWalk = 0.2f;
    public float moveSwayAdsSprint = 0.3f;
    [Space]
    public Vector3 mouseSwayPosAmount = new Vector3(0.3f, 0.005f, 0f);
    public Vector3 mouseSwayPosBase = Vector3.zero;
    public Vector3 mouseSwayPosMin = new Vector3(-0.1f, -0.01f, 5f);
    public Vector3 mouseSwayPosMax = new Vector3(0.1f, 0.005f, 5f);
    public Vector3 mouseSwayRotAmount = new Vector3(5f, 2f, 3f);
    public Vector3 mouseSwayRotBase = Vector3.zero;
    public Vector3 mouseSwayRotMin = new Vector3(-5f, -10f, -30f);
    public Vector3 mouseSwayRotMax = new Vector3(5f, 50f, 30f);
    [Space]
    public Vector3 moveSwayPosAmount = new Vector3(-0.08f, 0f, -0.02f);
    public Vector3 moveSwayPosBase = Vector3.zero;
    public Vector3 moveSwayPosMin = new Vector3(-0.25f, 0f, -0.07f);
    public Vector3 moveSwayPosMax = new Vector3(0.07f, 0f, 0.07f);
    public Vector3 moveSwayRotAmount = new Vector3(0.5f, 10f, 8f);
    public Vector3 moveSwayRotBase = Vector3.zero;
    public Vector3 moveSwayRotMin = new Vector3(-2f, -10f, -15f);
    public Vector3 moveSwayRotMax = new Vector3(2f, 20f, 15f);
    [Space]
    public Vector3 adsMouseSwayPosAmount = new Vector3(0.05f, 0.001f, 0f);
    public Vector3 adsMouseSwayPosBase = Vector3.zero;
    public Vector3 adsMouseSwayPosMin = new Vector3(-0.02f, -0.005f, 0f);
    public Vector3 adsMouseSwayPosMax = new Vector3(0.02f, 0.005f, 0f);
    public Vector3 adsMouseSwayRotAmount = new Vector3(1f, 0.5f, 1f);
    public Vector3 adsMouseSwayRotBase = Vector3.zero;
    public Vector3 adsMouseSwayRotMin = new Vector3(-1f, -2f, -5f);
    public Vector3 adsMouseSwayRotMax = new Vector3(1f, 2f, 5f);
    [Space]
    public Vector3 adsMoveSwayPosAmount = new Vector3(-0.02f, 0f, -0.005f);
    public Vector3 adsMoveSwayPosBase = Vector3.zero;
    public Vector3 adsMoveSwayPosMin = new Vector3(-0.05f, 0f, -0.02f);
    public Vector3 adsMoveSwayPosMax = new Vector3(0.02f, 0f, 0.02f);
    public Vector3 adsMoveSwayRotAmount = new Vector3(0.1f, 2f, 2f);
    public Vector3 adsMoveSwayRotBase = Vector3.zero;
    public Vector3 adsMoveSwayRotMin = new Vector3(-0.5f, -2f, -3f);
    public Vector3 adsMoveSwayRotMax = new Vector3(0.5f, 4f, 3f);
    public float swaySmoothPos = 10f;
    public float swaySmoothRot = 20f;

    [Header("11. Physics, Audio, Laser & VFX")]
    public float bulletImpactForce = 5f;
    public AudioSource shootAudioSource;
    public AudioClip[] shootClips;
    public ParticleSystem muzzleFlash;
    public bool showLaser = true;
    public Material laserMaterial;
    public LayerMask laserIgnoreLayers;
    [Range(0.01f, 1.0f)] public float bulletTimeScale = 0.328f;
    public bool isBulletTimeActive = false;

    [Header("13. Ejection Settings")]
    [Tooltip("Asset prefab of the spent casing, not the casing displayed in the chamber.")]
    [SerializeField] private GameObject casingPrefab;
    [Tooltip("Asset prefab of a complete cartridge; used for the magazine, feeding and live-round ejection.")]
    [SerializeField] private GameObject roundPrefab;
    [Tooltip("A marker at the feed lips, parented to the magazine mesh.")]
    [SerializeField] private Transform magazineTopPoint;
    [Tooltip("Use an empty chamber marker when no chamber meshes are in the hierarchy.")]
    [SerializeField] private Transform chamberPoint;
    [Tooltip("Fallback under the body (EjectionPoint's parent); tune if you removed the chamber meshes.")]
    [SerializeField] private Vector3 chamberLocalPosition = new Vector3(-0.95196813f, 0.020080771f, 2.5098643f);
    [SerializeField] private Vector3 magazineTopLocalPosition = new Vector3(0.5f, 0f, -0.7f);
    [Range(0f, 0.9f)] [SerializeField] private float feedStartOnReturn = 0.15f;
    [Range(0.1f, 1f)] [SerializeField] private float feedEndOnReturn = 0.85f;
    [SerializeField] private float feedArcHeight = 0.003f;
    [SerializeField] private Transform ejectionPoint;
    [SerializeField] private Vector3 ejectionDirection = new Vector3(1f, 1f, 0.2f);
    [Tooltip("Fixed initial rotation of EJECTED CASES, in degrees relative to Ejection Point. Positive Y turns right.")]
    [SerializeField] private Vector3 casingEjectionRotationOffset;
    [Tooltip("Predictable casing spin, in radians/second on the Ejection Point's local axes. Positive Y yaws right.")]
    [SerializeField] private Vector3 casingSpinBiasLocal = new Vector3(0f, 16f, 0f);
    [UnityEngine.Serialization.FormerlySerializedAs("ejectionForce")]
    [SerializeField] private float ejectionSpeed = 2.5f; // metres per second, not a Rigidbody impulse
    [UnityEngine.Serialization.FormerlySerializedAs("ejectionTorque")]
    [Tooltip("Random additional spin on top of the fixed casing spin (radians/second).")]
    [SerializeField] private float ejectionSpin = 10f;
    [Tooltip("Slide travel (0 = closed, 1 = fully back) when the case STARTS following the slide.")]
    [Range(0f, 0.95f)] [SerializeField] private float spentCaseExtractionStart = 0.55f;
    [Tooltip("Slide travel at ejection; must be greater than Spent Case Extraction Start.")]
    [Range(0f, 1f)] [SerializeField] private float ejectionSlideThreshold = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float spentCaseSlideFollow = 0.7f;
    [Min(0f)] [SerializeField] private float minSpentExtractionTime = 0.04f;
    [SerializeField] private float casingLifetime = 5f;
    [SerializeField] private Vector3 casingScale = new Vector3(0.023f, 0.023f, 0.023f);

    [Header("14. Weapon Bobbing")]
    public float bobFreqWalk = 10f;
    public float bobFreqSprint = 15f;
    public Vector3 bobPosBase = Vector3.zero;
    public Vector3 bobPosMin = new Vector3(-0.005f, 0.03f, -0.03f);
    public Vector3 bobPosMax = new Vector3(0.005f, 0.03f, 0.03f);
    public float bobPosRandomness = 0.025f;
    public Vector3 bobRotBase = Vector3.zero;
    public Vector3 bobRotMin = new Vector3(-3f, -3f, -1f);
    public Vector3 bobRotMax = new Vector3(3f, 3f, 3f);
    [Space]
    public float adsBobFreqWalk = 8f;
    public float adsBobFreqSprint = 12f;
    public Vector3 adsBobPosBase = Vector3.zero;
    public Vector3 adsBobPosMin = new Vector3(-0.01f, -0.007f, -0.0017f);
    public Vector3 adsBobPosMax = new Vector3(0.01f, 0.001f, 0.0023f);
    public float adsBobPosRandomness = 0.0002f;
    public Vector3 adsBobRotBase = Vector3.zero;
    public Vector3 adsBobRotMin = new Vector3(-1.25f, -0.8f, -1f);
    public Vector3 adsBobRotMax = new Vector3(1.25f, 0.8f, 0.5f);

    [Header("15. Recoil Speed Multipliers")]
    public float hipPosSpeedMult = 0.5f;
    public float hipRotSpeedMult = 0.5f;
    public float adsPosSpeedMult = 1.5f;
    public float adsRotSpeedMult = 1.5f;

    [Header("16. Directional Bias")]
    public DirectionalBias biasMoveRight;
    public DirectionalBias biasMoveLeft;
    public DirectionalBias biasMoveForward;
    public DirectionalBias biasMoveBackward;

    [Header("17. Camera Shake")]
    public float camShakeHip = 0.3f;
    public float camShakeADS = 0.1f;

    [Header("18. M1911 SAO Mechanics")]
    public int maxMagAmmo = 7;
    public int currentMagAmmo = 7;
    public bool isMagazineInserted = true;
    public bool isChamberLoaded = true;
    public bool isChamberSpent = false;
    public bool isHammerCocked = true;
    public bool isSlideLocked = false;
    [Space]
    public GameObject magMesh;
    public GameObject liveRoundMesh;
    public GameObject spentCasingMesh;
    public GameObject droppedMagPrefab;
    [Space]
    public AudioClip[] dryFireClips;
    public AudioClip[] triggerClickClips;
    public AudioClip[] cockSounds;
    public AudioClip[] slideLockSounds;
    public AudioClip[] magDropSounds;
    public AudioClip[] magInsertSounds;

    [Header("18.1 Tactical Reload Physics")]
    public Vector3 magDropDirection = new Vector3(0.2f, -1f, 0f);
    [UnityEngine.Serialization.FormerlySerializedAs("magDropForce")]
    public float magDropSpeed = 1.5f; // metres per second
    [UnityEngine.Serialization.FormerlySerializedAs("magDropTorque")]
    public float magDropSpin = 15f; // radians per second

    [Header("18.2 Magazine Top Visibility")]
    [Tooltip("Hide the magazine's top round behind a closed slide. It still exists logically and appears when the slide opens.")]
    public bool hideMagTopWhenSlideClosed = true;
    [Range(0f, 1f)] public float magTopRevealSlideFraction = 0.15f;

    [Header("19. Manual Slide Settings")]
    public float slideHoldThreshold = 0.2f;
    public float slideCheckPullSpeed = 10f;
    public float slideQuickRackSpeed = 30f;
    public float chamberingPullThreshold = 0.7f;
    public float manualLockPullThreshold = 0.9f;

    [Header("19.1 Manual Slide & Ejection Sounds")]
    [Tooltip("Separate source for mechanical one-shots; created at runtime if empty and clips assigned.")]
    public AudioSource slideAudioSource;
    public AudioClip[] slideBackClips;
    public AudioClip[] slideForwardClips;
    [Tooltip("Minimum actual slide travel (0..1) in a new direction before a back/front one-shot plays. Filters mouse jitter.")]
    [Range(0f, 1f)] public float slideOneShotTravelThreshold = 0.1f;
    public AudioClip[] liveRoundEjectClips;
    public AudioClip[] spentCaseEjectClips;
    [Tooltip("Optional separate source for a short seamless dragging loop (not for the one-shot clacks).")]
    public AudioSource slideDragAudioSource;
    public AudioClip slideBackDragLoop;
    public AudioClip slideForwardDragLoop;
    [Range(0f, 1f)] public float slideDragLoopVolume = 0.35f;
    public float slideDragMinPitch = 0.8f;
    public float slideDragMaxPitch = 1.25f;

    // --- ПРИВАТНЫЕ ПЕРЕМЕННЫЕ (ЕДИНСТВЕННЫЙ БЛОК НА ВЕСЬ КЛАСС) ---
    private Vector3 currentRecoilPos, targetRecoilPos;
    private Vector3 currentRecoilRot, targetRecoilRot;
    private float progressiveRecoilHeat;
    private Vector3 currentSwayPos, currentSwayRot;

    private bool isADS = false;
    private bool isFiringRoutine = false;
    private bool isTriggerCycleActive = false;
    private bool isHammerDropping = false;
    private bool isReloading = false;
    private bool isManualSlidePull = false;
    private bool isQuickRacking = false;
    private bool isHoldingSlideBtn = false;

    private float slideClickTimer = 0f;
    private float manualSlideAmount = 0f;
    private float manualInspectSwayX = 0f;
    private bool manualStrokeReadyToFeed;
    private bool manualEjectedThisStroke;
    private int manualSlideAudioDirection;
    private int pendingSlideAudioDirection;
    private float pendingSlideAudioTravel;

    private LineRenderer laserLine;
    private GameObject magazineTopVisual;
    private GameObject feedingRoundVisual;
    private bool isCasingExtracting;
    private Vector3 casingRestLocalPos;
    private Quaternion casingRestLocalRot;
    private Vector3 casingStartInSlideParent;
    private Vector3 slideStartLocalPos;
    private bool isFeedingRound;
    private float feedingProgress;
    private float weaponBobTimer;
    private SimpleFPSController fpsController;

#if UNITY_EDITOR
    [Header("20. ADVANCED PREVIEW (EDITOR)")]
    public bool previewInADS = false;
    public bool previewHammerCocked = false;
    public bool previewSlidePulled = false;
    public enum PreviewMode { None, Recoil, SwayMouse, SwayMove, SlidePull, ProgressiveKick }
    public PreviewMode currentPreviewMode = PreviewMode.None;
    
    [Header("Preview Axes")]
    public bool applyPosX = true; public bool applyPosY = true; public bool applyPosZ = true;
    public bool applyRotX = true; public bool applyRotY = true; public bool applyRotZ = true;
    
    [Space]
    [Range(-1f, 1f)] 
    [Tooltip("Recoil: 0 = rest, +/-1 = recoil ceilings. ProgressiveKick: +/-1 = one max/min kick at full shot heat (after ceilings), with base.")]
    public float previewSlider = 0f;

    private const string PREFS_KEY = "TacticalGun_SaveData";
    private const string PREFS_FLAG = "TacticalGun_HasSave";

    [ContextMenu("!!! APPLY RUNTIME SETTINGS !!!")]
    public void ApplyRuntimeSettings()
    {
        if (Application.isPlaying)
        {
            EditorPrefs.SetString(PREFS_KEY, JsonUtility.ToJson(this));
            EditorPrefs.SetBool(PREFS_FLAG, true);
            Debug.Log("<color=yellow><b>[TacticalGun] Настройки сохранены! Выйдите из Play Mode.</b></color>");
        }
        else
        {
            Undo.RecordObject(this, "Save Gun Settings");
            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
    }

    [InitializeOnLoadMethod]
    private static void AutoSaveRuntimeOnEditMode()
    {
        EditorApplication.playModeStateChanged += (state) =>
        {
            if (state == PlayModeStateChange.EnteredEditMode && EditorPrefs.GetBool(PREFS_FLAG, false))
            {
                EditorPrefs.SetBool(PREFS_FLAG, false);
                string json = EditorPrefs.GetString(PREFS_KEY);
                TacticalGun gun = Object.FindAnyObjectByType<TacticalGun>();
                
                if (gun != null && !string.IsNullOrEmpty(json))
                {
                    Undo.RecordObject(gun, "Apply Runtime Settings");
                    JsonUtility.FromJsonOverwrite(json, gun);
                    EditorUtility.SetDirty(gun);
                    EditorSceneManager.MarkSceneDirty(gun.gameObject.scene);
                }
            }
        };
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
        if (this == null || gameObject == null || !gameObject.scene.IsValid()) return;
        
        if (slide != null && slideBasePos == Vector3.zero) slideBasePos = slide.localPosition;
        if (hammer != null && hammerBaseRotation == Quaternion.identity) hammerBaseRotation = hammer.localRotation;
        if (trigger != null)
        {
            if (triggerBaseRotation == Quaternion.identity) triggerBaseRotation = trigger.localRotation;
            if (triggerBasePos == Vector3.zero) triggerBasePos = trigger.localPosition;
        }

        Vector3 finalPos = previewInADS ? adsPos : baseIdlePos;
        Vector3 finalRot = previewInADS ? adsRot : baseIdleRot;
        
        bool isPreviewingSlide = (currentPreviewMode == PreviewMode.SlidePull) || previewSlidePulled;
        if (isPreviewingSlide) { finalPos = slidePullPos; finalRot = slidePullRot; }

        if (currentPreviewMode != PreviewMode.None && !isPreviewingSlide && Mathf.Abs(previewSlider) > 0.0001f)
        {
            float weight = Mathf.Clamp01(Mathf.Abs(previewSlider));
            bool towardMin = previewSlider < 0f;

            Vector3 posExtreme = Vector3.zero;
            Vector3 rotExtreme = Vector3.zero;

            if (currentPreviewMode == PreviewMode.Recoil)
            {
                posExtreme = towardMin ? minPosRecoil : maxPosRecoil;
                rotExtreme = towardMin ? minRotRecoil : maxRotRecoil;
            }
            else if (currentPreviewMode == PreviewMode.ProgressiveKick)
            {
                Vector3 posBase = previewInADS ? posKickADS : posKickHip;
                Vector3 rotBase = previewInADS ? rotAngleADS : rotAngleHip;
                Vector3 posVariation = previewInADS
                    ? (towardMin ? posKickADSMin : posKickADSMax)
                    : (towardMin ? posKickHipMin : posKickHipMax);
                Vector3 rotVariation = previewInADS
                    ? (towardMin ? rotAngleADSMin : rotAngleADSMax)
                    : (towardMin ? rotAngleHipMin : rotAngleHipMax);
                float factor = 1f + progressiveRecoilMultiplier *
                               Mathf.Pow(weight, Mathf.Max(0.01f, progressiveRecoilCurve));
                Vector3 posKick = (posBase + posVariation) * factor * posRecoilMultiplier * globalRecoilMultiplier;
                Vector3 rotKick = (rotBase + rotVariation) * factor * rotRecoilMultiplier * globalRecoilMultiplier;
                posExtreme = new Vector3(
                    Mathf.Clamp(posKick.x, minPosRecoil.x, maxPosRecoil.x),
                    Mathf.Clamp(posKick.y, minPosRecoil.y, maxPosRecoil.y),
                    Mathf.Clamp(posKick.z, minPosRecoil.z, maxPosRecoil.z));
                rotExtreme = new Vector3(
                    Mathf.Clamp(rotKick.x, minRotRecoil.x, maxRotRecoil.x),
                    Mathf.Clamp(rotKick.y, minRotRecoil.y, maxRotRecoil.y),
                    Mathf.Clamp(rotKick.z, minRotRecoil.z, maxRotRecoil.z));
            }
            else if (currentPreviewMode == PreviewMode.SwayMouse)
            {
                posExtreme = towardMin
                    ? (previewInADS ? adsMouseSwayPosMin : mouseSwayPosMin)
                    : (previewInADS ? adsMouseSwayPosMax : mouseSwayPosMax);
                rotExtreme = towardMin
                    ? (previewInADS ? adsMouseSwayRotMin : mouseSwayRotMin)
                    : (previewInADS ? adsMouseSwayRotMax : mouseSwayRotMax);
            }
            else if (currentPreviewMode == PreviewMode.SwayMove)
            {
                posExtreme = towardMin
                    ? (previewInADS ? adsMoveSwayPosMin : moveSwayPosMin)
                    : (previewInADS ? adsMoveSwayPosMax : moveSwayPosMax);
                rotExtreme = towardMin
                    ? (previewInADS ? adsMoveSwayRotMin : moveSwayRotMin)
                    : (previewInADS ? adsMoveSwayRotMax : moveSwayRotMax);
            }

            Vector3 posOff = posExtreme * weight;
            Vector3 rotOff = rotExtreme * weight;

            if (applyPosX) finalPos.x += posOff.x; if (applyPosY) finalPos.y += posOff.y; if (applyPosZ) finalPos.z += posOff.z;
            if (applyRotX) finalRot.x += rotOff.x; if (applyRotY) finalRot.y += rotOff.y; if (applyRotZ) finalRot.z += rotOff.z;
        }

        transform.localPosition = finalPos;
        transform.localEulerAngles = finalRot;

        if (slide != null) slide.localPosition = isPreviewingSlide ? (slideBasePos + slideRecoilOffset) : slideBasePos;
        
        if (hammer != null) 
        {
            Quaternion cockedRot = GetCockedRotation(hammerCockedAngle);
            hammer.localRotation = previewHammerCocked ? cockedRot : hammerBaseRotation;
        } 
        
        if (firePoint != null && showLaser)
        {
            LineRenderer lr = firePoint.GetComponent<LineRenderer>();
            if (lr != null) { lr.enabled = true; lr.SetPosition(0, firePoint.position); lr.SetPosition(1, firePoint.position + firePoint.forward * 100f); }
        }
    }
#endif
}