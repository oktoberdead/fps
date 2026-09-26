using UnityEngine;

public class SimpleFPSController : MonoBehaviour
{
    [Header("1. Weight & Physics")]
    public float playerMass = 75f;
    public float acceleration = 12f;
    public float gravity = -20f;

    [Header("2. Movement Settings")]
    public float moveSpeed = 4.5f;
    public float sprintSpeed = 7.5f;
    public float jumpHeight = 1.2f;
    [Range(0.1f, 1.0f)] public float adsSpeedMultiplier = 0.5f;

    [Header("3. Look & Freelook (PUBG Style)")]
    public float mouseSensitivity = 2f;
    public Camera playerCamera;
    public KeyCode freelookKey = KeyCode.LeftAlt;
    public float maxFreelookAngle = 110f;
    public float freelookReturnSpeed = 15f;

    [Tooltip("Множитель чувствительности (используется оружием при осмотре)")]
    [HideInInspector] public float lookSensitivityMultiplier = 1f;

    [Header("4. Dynamic FOV")]
    public float baseFOV = 60f;
    public float walkFOV = 63f;
    public float sprintFOV = 68f;
    public float fovLerpSpeed = 5f;

    [Header("5. Camera Headbob")]
    public float walkBobSpeed = 12f;
    public float walkBobAmountX = 0.015f;
    public float walkBobAmountY = 0.025f;
    [Space]
    public float sprintBobSpeed = 16f;
    public float sprintBobAmountX = 0.03f;
    public float sprintBobAmountY = 0.05f;

    [Header("6. Camera Shake (Тряска экрана от выстрела)")]
    public Vector3 shakeRotMultiplier = new Vector3(3f, 2f, 1f);
    public float shakeDecay = 10f;

    [Header("Debug Info")]
    public CharacterController controller;

    private float cameraPitch = 0f;
    private float cameraYaw = 0f;
    private Vector3 currentMoveVelocity;
    private Vector3 verticalVelocity;
    private bool isGrounded;

    private float defaultCameraY;
    private float bobTimer;
    private float currentTrauma = 0f;

    public Vector2 MovementInput { get; private set; }
    public float NormalizedSpeed { get; private set; }

    private void Start()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (controller == null) controller = GetComponent<CharacterController>();

        if (playerCamera != null)
        {
            defaultCameraY = playerCamera.transform.localPosition.y;
            playerCamera.fieldOfView = baseFOV;
        }

        LockCursor();
    }

    private void Update()
    {
        if (controller == null) return;

        HandleCursorState();

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            HandleMouseLook();
        }

        HandleMovementAndJump();
        HandleDynamicFOVAndBob();
    }

    private void HandleCursorState()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Cursor.lockState == CursorLockMode.Locked) UnlockCursor();
            else LockCursor();
        }
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked) LockCursor();
    }

    private void LockCursor() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    private void UnlockCursor() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

    private void HandleMouseLook()
    {
        if (playerCamera == null) return;

        bool isFreelooking = Input.GetKey(freelookKey);

        // УЧИТЫВАЕМ МНОЖИТЕЛЬ ЧУВСТВИТЕЛЬНОСТИ
        float currentSens = mouseSensitivity * lookSensitivityMultiplier;
        float mouseX = Input.GetAxis("Mouse X") * currentSens;
        float mouseY = Input.GetAxis("Mouse Y") * currentSens;

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -85f, 85f);

        if (isFreelooking)
        {
            cameraYaw += mouseX;
            cameraYaw = Mathf.Clamp(cameraYaw, -maxFreelookAngle, maxFreelookAngle);
        }
        else
        {
            transform.Rotate(Vector3.up * mouseX);
            cameraYaw = Mathf.Lerp(cameraYaw, 0f, Time.deltaTime * freelookReturnSpeed);
            if (Mathf.Abs(cameraYaw) < 0.1f) cameraYaw = 0f;
        }

        currentTrauma = Mathf.Lerp(currentTrauma, 0f, Time.deltaTime * shakeDecay);
        float shakePitch = (Mathf.PerlinNoise(Time.time * 20f, 0f) - 0.5f) * 2f * currentTrauma * shakeRotMultiplier.x;
        float shakeYaw = (Mathf.PerlinNoise(0f, Time.time * 20f) - 0.5f) * 2f * currentTrauma * shakeRotMultiplier.y;
        float shakeRoll = (Mathf.PerlinNoise(Time.time * 20f, Time.time * 20f) - 0.5f) * 2f * currentTrauma * shakeRotMultiplier.z;

        playerCamera.transform.localRotation = Quaternion.Euler(cameraPitch + shakePitch, cameraYaw + shakeYaw, shakeRoll);
    }

    public void AddCameraShake(float traumaAmount)
    {
        currentTrauma = Mathf.Clamp01(currentTrauma + traumaAmount);
    }

    private void HandleMovementAndJump()
    {
        isGrounded = controller.isGrounded;
        if (isGrounded && verticalVelocity.y < 0) verticalVelocity.y = -3f;

        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");
        MovementInput = new Vector2(moveX, moveZ);

        bool isADS = Input.GetMouseButton(1);
        bool isSprinting = Input.GetKey(KeyCode.LeftShift) && moveZ > 0 && !isADS;

        float targetSpeed = (moveX != 0 || moveZ != 0) ? (isSprinting ? sprintSpeed : moveSpeed) : 0f;
        if (isADS) targetSpeed *= adsSpeedMultiplier;

        Vector3 targetDirection = (transform.right * moveX + transform.forward * moveZ).normalized;

        currentMoveVelocity = Vector3.Lerp(currentMoveVelocity, targetDirection * targetSpeed, acceleration * Time.deltaTime);
        controller.Move(currentMoveVelocity * Time.deltaTime);

        NormalizedSpeed = sprintSpeed > 0 ? currentMoveVelocity.magnitude / sprintSpeed : 0;

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity.y += gravity * Time.deltaTime;
        controller.Move(verticalVelocity * Time.deltaTime);
    }

    private void HandleDynamicFOVAndBob()
    {
        if (playerCamera == null) return;

        float flatSpeed = new Vector3(currentMoveVelocity.x, 0, currentMoveVelocity.z).magnitude;
        bool isMoving = flatSpeed > 0.1f;
        bool isSprinting = flatSpeed > moveSpeed + 0.5f;

        float targetFOV = isSprinting ? sprintFOV : (isMoving ? walkFOV : baseFOV);
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, Time.deltaTime * fovLerpSpeed);

        if (isGrounded && isMoving)
        {
            float currentBobSpeed = isSprinting ? sprintBobSpeed : walkBobSpeed;
            float currentBobAmpX = isSprinting ? sprintBobAmountX : walkBobAmountX;
            float currentBobAmpY = isSprinting ? sprintBobAmountY : walkBobAmountY;

            bobTimer += Time.deltaTime * currentBobSpeed;

            float targetBobX = Mathf.Cos(bobTimer / 2f) * currentBobAmpX;
            float targetBobY = Mathf.Sin(bobTimer) * currentBobAmpY;

            Vector3 localPos = playerCamera.transform.localPosition;
            playerCamera.transform.localPosition = Vector3.Lerp(localPos, new Vector3(targetBobX, defaultCameraY + targetBobY, localPos.z), Time.deltaTime * 10f);
        }
        else
        {
            bobTimer = 0f;
            Vector3 localPos = playerCamera.transform.localPosition;
            playerCamera.transform.localPosition = Vector3.Lerp(localPos, new Vector3(0, defaultCameraY, localPos.z), Time.deltaTime * 5f);
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;
        if (body == null || body.isKinematic) return;
        if (hit.moveDirection.y < -0.3f) return;

        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);
        float pushForce = (playerMass / body.mass) * currentMoveVelocity.magnitude;
        body.AddForceAtPosition(pushDir * pushForce, hit.point, ForceMode.Force);
    }
}