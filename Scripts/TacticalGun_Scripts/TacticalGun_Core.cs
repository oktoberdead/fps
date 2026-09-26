using UnityEngine;
using System.Collections;

public partial class TacticalGun
{
    private void Start()
    {
        if (!Application.isPlaying) return;
        fpsController = Object.FindAnyObjectByType<SimpleFPSController>();

        if (slideBasePos == Vector3.zero && slide != null) slideBasePos = slide.localPosition;
        if (hammerBaseRotation == Quaternion.identity && hammer != null) hammerBaseRotation = hammer.localRotation;
        if (trigger != null)
        {
            if (triggerBaseRotation == Quaternion.identity) triggerBaseRotation = trigger.localRotation;
            if (triggerBasePos == Vector3.zero) triggerBasePos = trigger.localPosition;
        }

        if (hammer != null) hammer.localRotation = isHammerCocked ? GetCockedRotation(hammerCockedAngle) : hammerBaseRotation;
        SetupLaser();
        SetupAmmoVisuals();
    }

    private void SetupLaser()
    {
        if (firePoint == null) return;
        laserLine = firePoint.GetComponent<LineRenderer>();
        if (laserLine == null)
        {
            laserLine = firePoint.gameObject.AddComponent<LineRenderer>();
            laserLine.startWidth = 0.002f; laserLine.endWidth = 0.002f; laserLine.useWorldSpace = true;
            if (laserMaterial != null) laserLine.material = laserMaterial;
        }
        laserLine.positionCount = 2;
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        isADS = Input.GetKey(adsKey);

        if (Input.GetKeyDown(laserToggleKey)) showLaser = !showLaser;
        if (Input.GetKeyDown(bulletTimeKey)) ToggleBulletTime();

        if (isReloading || isQuickRacking || isFiringRoutine) return;

        HandleSlideInput();

        // Safety catch if button released but flag stuck
        if (!Input.GetKey(slideKey) && (isHoldingSlideBtn || isManualSlidePull))
        {
            ForceEndSlideInspect(processChambering: true);
        }

        HandleFireInput();
        HandleHammerInput();
        HandleReloadInput();
    }

    private void HandleSlideInput()
    {
        if (Input.GetKeyDown(slideKey))
        {
            if (isSlideLocked) { ReleaseSlideLock(); return; }

            isHoldingSlideBtn = true;
            slideClickTimer = 0f;
            manualSlideAmount = 0f;
            manualInspectSwayX = 0f;

            if (fpsController != null) fpsController.lookSensitivityMultiplier = inspectCameraSlowdown;
        }

        if (Input.GetKey(slideKey) && isHoldingSlideBtn && !isSlideLocked)
        {
            slideClickTimer += Time.deltaTime;

            if (slideClickTimer >= slideHoldThreshold)
            {
                isManualSlidePull = true;
                float mouseY = Input.GetAxis("Mouse Y");
                float mouseX = Input.GetAxis("Mouse X");

                manualSlideAmount -= mouseY * inspectMouseYSens;
                manualSlideAmount = Mathf.Clamp01(manualSlideAmount);

                manualInspectSwayX -= mouseX * inspectMouseXSens;
                manualInspectSwayX = Mathf.Clamp(manualInspectSwayX, -inspectMaxSwayAngle, inspectMaxSwayAngle);

                if (manualSlideAmount > 0.3f && !isHammerCocked)
                {
                    isHammerCocked = true;
                    PlayRandomSound(cockSounds, 0.8f, 0.95f, 1.05f);
                }

                if (manualSlideAmount >= ejectionSlideThreshold) EjectChamberContents();
            }
        }

        if (Input.GetKeyUp(slideKey) && isHoldingSlideBtn)
        {
            ForceEndSlideInspect(processChambering: true);
        }
    }

    private void ForceEndSlideInspect(bool processChambering)
    {
        isHoldingSlideBtn = false;
        if (fpsController != null) fpsController.lookSensitivityMultiplier = 1f;

        if (isManualSlidePull)
        {
            float pulled = manualSlideAmount;
            isManualSlidePull = false;

            bool wantManualLock = pulled >= manualLockPullThreshold &&
                                 (Input.GetKey(modifierKey) || Input.GetKey(KeyCode.RightShift));
            // The follower only locks the slide back if an EMPTY magazine is in the gun.
            bool emptyMagLock = pulled >= manualLockPullThreshold && isMagazineInserted &&
                                currentMagAmmo <= 0 && !isChamberLoaded;

            if (wantManualLock || emptyMagLock)
            {
                isSlideLocked = true;
                manualSlideAmount = 1f;
                PlayRandomSound(slideLockSounds, 1f, 0.95f, 1.05f);
            }
            else
            {
                manualSlideAmount = 0f;
                StartCoroutine(ManualSlideReturnSequence(processChambering && pulled >= chamberingPullThreshold));
            }
        }
        else if (slideClickTimer < slideHoldThreshold && slideClickTimer > 0f && !isSlideLocked)
        {
            StartCoroutine(QuickRackSequence());
        }

        slideClickTimer = 0f;
    }

    private IEnumerator ManualSlideReturnSequence(bool canFeed)
    {
        isQuickRacking = true;
        isFiringRoutine = true;
        yield return ReturnSlideAndFeed(slideCheckPullSpeed, canFeed);
        isQuickRacking = false;
        isFiringRoutine = false;
    }

    private void HandleFireInput()
    {
        // The sear can break only once per press. A held trigger stays back even
        // after the slide cycles; another shot requires release and reset.
        if (Input.GetKeyDown(fireKey) && !isTriggerCycleActive)
            StartCoroutine(TriggerCycle());
    }

    private void TryFireM1911()
    {
        if (isReloading || isFiringRoutine || isHoldingSlideBtn || isManualSlidePull || isQuickRacking || manualSlideAmount > 0.05f) return;

        if (isSlideLocked)
        {
            if (!isHammerCocked) PlayRandomSound(triggerClickClips, 0.6f, 0.9f, 1.1f);
            else
            {
                isHammerCocked = false;
                PlayRandomSound(dryFireClips, 1.0f);
                StartCoroutine(DropHammerVisualOnly());
            }
            return;
        }

        if (!isHammerCocked)
        {
            PlayRandomSound(triggerClickClips, 0.6f, 0.9f, 1.1f);
            return;
        }

        isHammerCocked = false;

        if (isChamberLoaded && !isChamberSpent)
        {
            isChamberSpent = true;
            StartCoroutine(TacticalFiringCycle());
        }
        else
        {
            PlayRandomSound(dryFireClips, 1.0f);
            StartCoroutine(DropHammerVisualOnly());
        }
    }

    private IEnumerator DropHammerVisualOnly()
    {
        isHammerDropping = true;
        Quaternion cockedRot = GetCockedRotation(hammerCockedAngle);
        Quaternion uncockedRot = hammerBaseRotation;
        float tDrop = 0f;
        while (tDrop < 1f)
        {
            tDrop += Time.deltaTime * hammerDropSpeed;
            if (hammer != null) hammer.localRotation = Quaternion.Slerp(cockedRot, uncockedRot, tDrop);
            yield return null;
        }
        if (hammer != null) hammer.localRotation = uncockedRot;
        isHammerDropping = false;
    }

    private IEnumerator TriggerCycle()
    {
        isTriggerCycleActive = true;
        Vector3 axis = triggerLocalAxis == HammerAxis.LocalX ? Vector3.right :
            (triggerLocalAxis == HammerAxis.LocalY ? Vector3.up : Vector3.forward);
        Quaternion pulledRot = triggerBaseRotation * Quaternion.AngleAxis(triggerPullAngle, axis);
        Vector3 pulledPos = triggerBasePos + triggerPullOffset;

        float pull = 0f;
        bool searReleased = false;
        while (Input.GetKey(fireKey) && pull < 1f)
        {
            pull = Mathf.Min(1f, pull + Time.deltaTime / Mathf.Max(0.005f, triggerPullDuration));
            if (trigger != null)
            {
                trigger.localRotation = Quaternion.Lerp(triggerBaseRotation, pulledRot, pull);
                trigger.localPosition = Vector3.Lerp(triggerBasePos, pulledPos, pull);
            }

            // The hammer starts falling BEFORE the trigger finishes its travel.
            // An early release (before this threshold) does not fire.
            if (!searReleased && pull >= triggerBreakFraction)
            {
                searReleased = true;
                TryFireM1911();
            }
            yield return null;
        }

        // Once pulled, keep the trigger back as long as the finger is on it.
        while (Input.GetKey(fireKey)) yield return null;
        if (triggerResetDelay > 0f) yield return new WaitForSeconds(triggerResetDelay);

        Quaternion resetFromRot = trigger != null ? trigger.localRotation : pulledRot;
        Vector3 resetFromPos = trigger != null ? trigger.localPosition : pulledPos;
        float reset = 0f;
        while (reset < 1f)
        {
            reset = Mathf.Min(1f, reset + Time.deltaTime / Mathf.Max(0.005f, triggerReturnDuration));
            if (trigger != null)
            {
                trigger.localRotation = Quaternion.Lerp(resetFromRot, triggerBaseRotation, reset);
                trigger.localPosition = Vector3.Lerp(resetFromPos, triggerBasePos, reset);
            }
            yield return null;
        }
        if (trigger != null)
        {
            trigger.localRotation = triggerBaseRotation;
            trigger.localPosition = triggerBasePos;
        }
        isTriggerCycleActive = false;
    }

    private void HandleHammerInput()
    {
        if (!Input.GetKeyDown(hammerKey)) return;
        if (Input.GetKey(modifierKey) || Input.GetKey(KeyCode.RightShift))
        {
            isHammerCocked = false;
            PlayRandomSound(cockSounds, 0.7f, 0.85f, 0.95f);
        }
        else if (!isHammerCocked)
        {
            isHammerCocked = true;
            PlayRandomSound(cockSounds, 1.0f, 0.98f, 1.02f);
        }
    }

    private void HandleReloadInput()
    {
        if (Input.GetKeyDown(reloadKey) && currentMagAmmo < maxMagAmmo && !isFiringRoutine)
        {
            StartCoroutine(ReloadSequence());
        }
    }

    private IEnumerator QuickRackSequence()
    {
        isQuickRacking = true;
        isFiringRoutine = true;
        Vector3 slideStart = slide != null ? slide.localPosition : slideBasePos;
        Vector3 slideBack = slideBasePos + slideRecoilOffset;
        float t = 0f;
        bool ejected = false;
        bool cockedThisRack = false;

        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime * Mathf.Max(0.01f, slideQuickRackSpeed));
            if (slide != null) slide.localPosition = Vector3.Lerp(slideStart, slideBack, t);

            if (!cockedThisRack && t >= 0.3f)
            {
                isHammerCocked = true;
                cockedThisRack = true;
                PlayRandomSound(cockSounds, 0.9f);
            }
            if (!ejected && t >= ejectionSlideThreshold)
            {
                EjectChamberContents();
                ejected = true;
            }
            yield return null;
        }

        if (slide != null) slide.localPosition = slideBack;
        if (isMagazineInserted && currentMagAmmo <= 0 && !isChamberLoaded)
        {
            isSlideLocked = true;
            PlayRandomSound(slideLockSounds, 1f, 0.95f, 1.05f);
        }
        else
        {
            yield return ReturnSlideAndFeed(slideQuickRackSpeed, true);
        }
        isQuickRacking = false;
        isFiringRoutine = false;
    }

    // One slide-return path for a shot, a tap rack, a manual pull and slide release.
    // The top cartridge leaves the magazine on the forward stroke; the counter
    // changes only when it has actually reached the chamber.
    private IEnumerator ReturnSlideAndFeed(float speed, bool canFeed)
    {
        Vector3 start = slide != null ? slide.localPosition : slideBasePos + slideRecoilOffset;
        float t = 0f;
        bool feedStarted = false;
        float feedStart = Mathf.Clamp01(feedStartOnReturn);
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime * Mathf.Max(0.01f, speed));
            if (slide != null) slide.localPosition = Vector3.Lerp(start, slideBasePos, t);
            if (canFeed && !feedStarted && t >= feedStart)
            {
                BeginFeed();
                feedStarted = true;
            }
            AdvanceFeed(t);
            yield return null;
        }
        CompleteFeed(); // also handles a short/low-frame-rate animation
        if (slide != null) slide.localPosition = slideBasePos;
    }

    private IEnumerator ReloadSequence()
    {
        isReloading = true;
        isMagazineInserted = false;
        if (magMesh != null) magMesh.SetActive(false);

        if (droppedMagPrefab != null && magMesh != null && fpsController != null)
        {
            GameObject droppedMag = Instantiate(droppedMagPrefab, magMesh.transform.position, magMesh.transform.rotation);
            droppedMag.transform.localScale = magMesh.transform.lossyScale;
            if (currentMagAmmo > 0 && roundPrefab != null && magazineTopPoint != null)
            {
                GameObject top = Instantiate(roundPrefab, droppedMag.transform);
                top.name = "MagazineTopRound";
                top.transform.localPosition = magazineTopPoint.localPosition;
                top.transform.localRotation = magazineTopPoint.localRotation;
                top.transform.localScale = Vector3.one;
                foreach (Collider collider in top.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                Rigidbody topBody = top.GetComponent<Rigidbody>();
                if (topBody != null) { topBody.isKinematic = true; topBody.useGravity = false; }
            }

            // The model prefab has no collider, so without one the dropped mag
            // passes straight through the floor.
            if (droppedMag.GetComponent<Collider>() == null)
            {
                MeshFilter mesh = droppedMag.GetComponent<MeshFilter>();
                if (mesh != null && mesh.sharedMesh != null)
                {
                    BoxCollider box = droppedMag.AddComponent<BoxCollider>();
                    box.center = mesh.sharedMesh.bounds.center;
                    box.size = mesh.sharedMesh.bounds.size;
                }
            }
            Rigidbody rb = droppedMag.GetComponent<Rigidbody>();
            if (rb == null) { rb = droppedMag.AddComponent<Rigidbody>(); rb.mass = 0.25f; rb.interpolation = RigidbodyInterpolation.Interpolate; }

            Vector3 dropDirection = fpsController.playerCamera.transform.TransformDirection(magDropDirection.normalized);
            rb.linearVelocity = dropDirection * Mathf.Max(0f, magDropSpeed);
            rb.angularVelocity = Random.onUnitSphere * Mathf.Abs(magDropSpin);
            foreach (Collider playerCollider in fpsController.GetComponents<Collider>())
                foreach (Collider droppedCollider in droppedMag.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(droppedCollider, playerCollider);

            Destroy(droppedMag, 5f);
        }

        PlayRandomSound(magDropSounds, 1f, 0.9f, 1.1f);
        yield return new WaitForSeconds(1.2f);

        currentMagAmmo = maxMagAmmo;
        isMagazineInserted = true;
        if (magMesh != null) magMesh.SetActive(true);
        PlayRandomSound(magInsertSounds, 1f, 0.95f, 1.05f);

        if (isSlideLocked)
        {
            yield return new WaitForSeconds(0.15f);
            ReleaseSlideLock();
        }

        isReloading = false;
    }

    private void ReleaseSlideLock()
    {
        if (!isSlideLocked) return;
        isSlideLocked = false;
        isQuickRacking = true;
        isFiringRoutine = true;
        StartCoroutine(ReleaseSlideSequence());
        PlayRandomSound(slideLockSounds, 1f, 1.05f, 1.15f);
    }

    private IEnumerator ReleaseSlideSequence()
    {
        yield return ReturnSlideAndFeed(slideReturnSpeed, true);
        isQuickRacking = false;
        isFiringRoutine = false;
    }

    public void ToggleBulletTime()
    {
        isBulletTimeActive = !isBulletTimeActive;
        Time.timeScale = isBulletTimeActive ? bulletTimeScale : 1.0f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
    }

    private float GetSeverityAxis(float v, float min, float max)
    {
        if (v >= 0f) return max > 0f ? Mathf.Clamp01(v / max) : 0f;
        return min < 0f ? Mathf.Clamp01(v / min) : 0f;
    }

    private IEnumerator TacticalFiringCycle()
    {
        isFiringRoutine = true;

        Quaternion cockedRot = GetCockedRotation(hammerCockedAngle);
        Quaternion uncockedRot = hammerBaseRotation;

        float tDrop = 0f;
        while (tDrop < 1f) { tDrop += Time.deltaTime * hammerDropSpeed; if (hammer != null) hammer.localRotation = Quaternion.Slerp(cockedRot, uncockedRot, tDrop); yield return null; }
        if (hammer != null) hammer.localRotation = uncockedRot;

        if (ignitionDelay > 0f) yield return new WaitForSeconds(ignitionDelay);

        if (muzzleFlash != null) muzzleFlash.Play();
        PlayRandomSound(shootClips, 1f, 0.87f, 0.95f);

        float speedRatio = fpsController != null ? fpsController.NormalizedSpeed : 0f;
        float inputX = fpsController != null ? fpsController.MovementInput.x : 0f;
        float inputZ = fpsController != null ? fpsController.MovementInput.y : 0f;

        Vector3 curPosBase = isADS ? posKickADS : posKickHip; Vector3 curPosMin = isADS ? posKickADSMin : posKickHipMin; Vector3 curPosMax = isADS ? posKickADSMax : posKickHipMax;
        Vector3 curRotBase = isADS ? rotAngleADS : rotAngleHip; Vector3 curRotMin = isADS ? rotAngleADSMin : rotAngleHipMin; Vector3 curRotMax = isADS ? rotAngleADSMax : rotAngleHipMax;

        void ApplyPosBias(DirectionalBias b, float weight) { curPosBase += b.posBase * weight; curPosMin += b.posMin * weight; curPosMax += b.posMax * weight; }
        if (inputX > 0.05f) ApplyPosBias(biasMoveRight, inputX); else if (inputX < -0.05f) ApplyPosBias(biasMoveLeft, -inputX);
        if (inputZ > 0.05f) ApplyPosBias(biasMoveForward, inputZ); else if (inputZ < -0.05f) ApplyPosBias(biasMoveBackward, -inputZ);

        Vector3 rawPosKick = GetAsymmetricRandomVector(curPosBase, curPosMin, curPosMax);
        Vector3 rawRotKick = GetAsymmetricRandomVector(curRotBase, curRotMin, curRotMax);

        rawPosKick *= (1f + (isADS ? adsPosSpeedMult : hipPosSpeedMult) * speedRatio);
        rawRotKick *= (1f + (isADS ? adsRotSpeedMult : hipRotSpeedMult) * speedRatio);

        float sPx = GetSeverityAxis(targetRecoilPos.x, minPosRecoil.x, maxPosRecoil.x);
        float sPy = GetSeverityAxis(targetRecoilPos.y, minPosRecoil.y, maxPosRecoil.y);
        float sPz = GetSeverityAxis(targetRecoilPos.z, minPosRecoil.z, maxPosRecoil.z);
        float sRx = GetSeverityAxis(targetRecoilRot.x, minRotRecoil.x, maxRotRecoil.x);
        float sRy = GetSeverityAxis(targetRecoilRot.y, minRotRecoil.y, maxRotRecoil.y);
        float sRz = GetSeverityAxis(targetRecoilRot.z, minRotRecoil.z, maxRotRecoil.z);

        float severity = Mathf.Max(sPx, sPy, sPz, sRx, sRy, sRz);
        float progressiveMultiplier = 1f + (progressiveRecoilMultiplier * Mathf.Pow(severity, progressiveRecoilCurve));

        rawPosKick *= progressiveMultiplier;
        rawRotKick *= progressiveMultiplier;

        targetRecoilPos += rawPosKick * posRecoilMultiplier * globalRecoilMultiplier;
        targetRecoilRot += rawRotKick * rotRecoilMultiplier * globalRecoilMultiplier;

        targetRecoilPos = new Vector3(
            Mathf.Clamp(targetRecoilPos.x, minPosRecoil.x, maxPosRecoil.x),
            Mathf.Clamp(targetRecoilPos.y, minPosRecoil.y, maxPosRecoil.y),
            Mathf.Clamp(targetRecoilPos.z, minPosRecoil.z, maxPosRecoil.z));

        targetRecoilRot = new Vector3(
            Mathf.Clamp(targetRecoilRot.x, minRotRecoil.x, maxRotRecoil.x),
            Mathf.Clamp(targetRecoilRot.y, minRotRecoil.y, maxRotRecoil.y),
            Mathf.Clamp(targetRecoilRot.z, minRotRecoil.z, maxRotRecoil.z));

        if (fpsController != null) fpsController.AddCameraShake(isADS ? camShakeADS : camShakeHip);

        if (firePoint != null)
        {
            int mask = ~laserIgnoreLayers.value;
            if (Physics.Raycast(firePoint.position, firePoint.forward, out RaycastHit hit, 100f, mask, QueryTriggerInteraction.Ignore))
            {
                Rigidbody rb = hit.collider.attachedRigidbody;
                if (rb != null) rb.AddForce(firePoint.forward * bulletImpactForce, ForceMode.Impulse);
            }
        }

        Vector3 slideStartPos = slide != null ? slide.localPosition : slideBasePos;
        Vector3 slideBackTarget = slideBasePos + slideRecoilOffset;
        float tSlideBack = 0f; float tHammerCock = 0f; bool casingEjected = false;

        while (tSlideBack < 1f || tHammerCock < 1f)
        {
            if (tSlideBack < 1f)
            {
                tSlideBack += Time.deltaTime * slideBlowbackSpeed;
                if (slide != null) slide.localPosition = Vector3.Lerp(slideStartPos, slideBackTarget, tSlideBack);
                if (!casingEjected && tSlideBack >= ejectionSlideThreshold) { EjectChamberContents(); casingEjected = true; }
            }
            if (tHammerCock < 1f)
            {
                tHammerCock += Time.deltaTime * hammerCockSpeed;
                if (hammer != null) hammer.localRotation = Quaternion.Slerp(uncockedRot, cockedRot, tHammerCock);
            }
            yield return null;
        }

        if (!casingEjected) EjectChamberContents();
        if (hammer != null) hammer.localRotation = cockedRot;
        isHammerCocked = true;

        if (isMagazineInserted && currentMagAmmo <= 0)
        {
            isSlideLocked = true;
            PlayRandomSound(slideLockSounds, 1f, 0.95f, 1.05f);
        }
        else
        {
            yield return ReturnSlideAndFeed(slideReturnSpeed, true);
        }
        isFiringRoutine = false;
    }

    private Vector3 GetAsymmetricRandomVector(Vector3 baseV, Vector3 minO, Vector3 maxO) { return new Vector3(baseV.x + Random.Range(minO.x, maxO.x), baseV.y + Random.Range(minO.y, maxO.y), baseV.z + Random.Range(minO.z, maxO.z)); }
    private Quaternion GetCockedRotation(float angle) { Vector3 axis = hammerLocalAxis == HammerAxis.LocalX ? Vector3.right : (hammerLocalAxis == HammerAxis.LocalY ? Vector3.up : Vector3.forward); return hammerBaseRotation * Quaternion.AngleAxis(angle, axis); }
    private void PlayRandomSound(AudioClip[] clips, float volume = 1f, float minPitch = 0.95f, float maxPitch = 1.05f) { if (clips == null || clips.Length == 0 || shootAudioSource == null) return; AudioClip clip = clips[Random.Range(0, clips.Length)]; if (clip == null) return; shootAudioSource.pitch = Random.Range(minPitch, maxPitch); shootAudioSource.PlayOneShot(clip, volume); }
}