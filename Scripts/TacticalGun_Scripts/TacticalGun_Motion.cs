using UnityEngine;

public partial class TacticalGun
{
    private void LateUpdate()
    {
        UpdateLaserLogic();
        if (!Application.isPlaying) return;

        targetRecoilPos = Vector3.Lerp(targetRecoilPos, Vector3.zero, posReturnSpeed * Time.deltaTime);
        currentRecoilPos = Vector3.Slerp(currentRecoilPos, targetRecoilPos, posSnappiness * Time.deltaTime);
        targetRecoilRot = Vector3.Lerp(targetRecoilRot, Vector3.zero, rotReturnSpeed * Time.deltaTime);
        currentRecoilRot = Vector3.Slerp(currentRecoilRot, targetRecoilRot, rotSnappiness * Time.deltaTime);

        CalculateSwayAndBob();

        if (!isManualSlidePull)
        {
            manualInspectSwayX = Mathf.Lerp(manualInspectSwayX, 0f, Time.deltaTime * 10f);
            if (Mathf.Abs(manualInspectSwayX) < 0.05f) manualInspectSwayX = 0f;
        }

        bool inInspectPose = isManualSlidePull || Mathf.Abs(manualInspectSwayX) > 0.05f;
        Vector3 targetPos = baseIdlePos;
        Vector3 targetRot = baseIdleRot;

        if (inInspectPose)
        {
            targetPos = slidePullPos;
            targetRot = slidePullRot + new Vector3(0f, manualInspectSwayX, 0f);
        }
        else if (isADS)
        {
            targetPos = adsPos;
            targetRot = adsRot;
        }

        float currentMoveSpeed = inInspectPose ? slideCheckPullSpeed : adsSpeed;
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos + currentRecoilPos + currentSwayPos, Time.deltaTime * currentMoveSpeed);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.Euler(targetRot + currentRecoilRot + currentSwayRot), Time.deltaTime * currentMoveSpeed);

        if (liveRoundMesh != null) liveRoundMesh.SetActive(isChamberLoaded && !isChamberSpent);
        if (spentCasingMesh != null) spentCasingMesh.SetActive(isChamberLoaded && isChamberSpent);

        if (slide != null && !isFiringRoutine && !isQuickRacking)
        {
            if (isSlideLocked)
            {
                slide.localPosition = Vector3.Lerp(slide.localPosition, slideBasePos + slideRecoilOffset, Time.deltaTime * slideCheckPullSpeed);
            }
            else if (isManualSlidePull)
            {
                Vector3 targetSlidePos = Vector3.Lerp(slideBasePos, slideBasePos + slideRecoilOffset, manualSlideAmount);
                slide.localPosition = Vector3.Lerp(slide.localPosition, targetSlidePos, Time.deltaTime * 35f);
            }
            else
            {
                slide.localPosition = Vector3.Lerp(slide.localPosition, slideBasePos, Time.deltaTime * slideReturnSpeed);
            }
        }

        if (!isFiringRoutine && hammer != null && !isManualSlidePull)
        {
            Quaternion targetHammerRot = isHammerCocked ? GetCockedRotation(hammerCockedAngle) : hammerBaseRotation;
            hammer.localRotation = Quaternion.Slerp(hammer.localRotation, targetHammerRot, Time.deltaTime * 25f);
        }
    }

    private float EvalSway(float input, float amount, float baseVal, float minVal, float maxVal)
    {
        return baseVal + Mathf.Clamp(input * amount, minVal, maxVal);
    }

    private Vector3 EvalBobWave(Vector3 baseV, Vector3 minV, Vector3 maxV, Vector3 waves)
    {
        float x = waves.x >= 0 ? Mathf.Lerp(0, maxV.x, waves.x) : Mathf.Lerp(0, minV.x, -waves.x);
        float y = waves.y >= 0 ? Mathf.Lerp(0, maxV.y, waves.y) : Mathf.Lerp(0, minV.y, -waves.y);
        float z = waves.z >= 0 ? Mathf.Lerp(0, maxV.z, waves.z) : Mathf.Lerp(0, minV.z, -waves.z);
        return baseV + new Vector3(x, y, z);
    }

    private void CalculateSwayAndBob()
    {
        float speedRatio = fpsController != null ? fpsController.NormalizedSpeed : 0f;
        bool isMoving = speedRatio > 0.05f;
        bool isSprinting = speedRatio > 0.6f;

        float mouseMult = isADS ? (isSprinting ? mouseSwayAdsSprint : (isMoving ? mouseSwayAdsWalk : mouseSwayAdsIdle)) : (isSprinting ? mouseSwaySprint : (isMoving ? mouseSwayWalk : mouseSwayIdle));
        float moveMult = isADS ? (isSprinting ? moveSwayAdsSprint : moveSwayAdsWalk) : (isSprinting ? moveSwaySprint : moveSwayWalk);

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
        float moveX = fpsController != null ? fpsController.MovementInput.x : Input.GetAxisRaw("Horizontal");
        float moveY = fpsController != null ? fpsController.MovementInput.y : Input.GetAxisRaw("Vertical");

        Vector3 mPosAmt = isADS ? adsMouseSwayPosAmount : mouseSwayPosAmount; Vector3 mPosBase = isADS ? adsMouseSwayPosBase : mouseSwayPosBase; Vector3 mPosMin = isADS ? adsMouseSwayPosMin : mouseSwayPosMin; Vector3 mPosMax = isADS ? adsMouseSwayPosMax : mouseSwayPosMax;
        Vector3 mvPosAmt = isADS ? adsMoveSwayPosAmount : moveSwayPosAmount; Vector3 mvPosBase = isADS ? adsMoveSwayPosBase : moveSwayPosBase; Vector3 mvPosMin = isADS ? adsMoveSwayPosMin : moveSwayPosMin; Vector3 mvPosMax = isADS ? adsMoveSwayPosMax : moveSwayPosMax;
        Vector3 mRotAmt = isADS ? adsMouseSwayRotAmount : mouseSwayRotAmount; Vector3 mRotBase = isADS ? adsMouseSwayRotBase : mouseSwayRotBase; Vector3 mRotMin = isADS ? adsMouseSwayRotMin : mouseSwayRotMin; Vector3 mRotMax = isADS ? adsMouseSwayRotMax : mouseSwayRotMax;
        Vector3 mvRotAmt = isADS ? adsMoveSwayRotAmount : moveSwayRotAmount; Vector3 mvRotBase = isADS ? adsMoveSwayRotBase : moveSwayRotBase; Vector3 mvRotMin = isADS ? adsMoveSwayRotMin : moveSwayRotMin; Vector3 mvRotMax = isADS ? adsMoveSwayRotMax : moveSwayRotMax;

        float mSwayPosX = EvalSway(-mouseX, mPosAmt.x, mPosBase.x, mPosMin.x, mPosMax.x);
        float mSwayPosY = EvalSway(-mouseY, mPosAmt.y, mPosBase.y, mPosMin.y, mPosMax.y);
        float mSwayPosZ = EvalSway(-mouseX, mPosAmt.z, mPosBase.z, mPosMin.z, mPosMax.z);
        float mvSwayPosX = EvalSway(-moveX, mvPosAmt.x, mvPosBase.x, mvPosMin.x, mvPosMax.x);
        float mvSwayPosY = EvalSway(-Mathf.Abs(moveX + moveY), mvPosAmt.y, mvPosBase.y, mvPosMin.y, mvPosMax.y);
        float mvSwayPosZ = EvalSway(-moveY, mvPosAmt.z, mvPosBase.z, mvPosMin.z, mvPosMax.z);
        Vector3 targetPosSway = new Vector3((mvSwayPosZ * moveMult), (mSwayPosY * mouseMult) + (mvSwayPosZ * moveMult), (mSwayPosX * mouseMult) + (mvSwayPosX * moveMult));

        float mSwayRotX = EvalSway(-mouseY, mRotAmt.x, mRotBase.x, mRotMin.x, mRotMax.x);
        float mSwayRotY = EvalSway(mouseX, mRotAmt.y, mRotBase.y, mRotMin.y, mRotMax.y);
        float mSwayRotZ = EvalSway(-mouseX, mRotAmt.z, mRotBase.z, mRotMin.z, mRotMax.z);
        float mvSwayRotX = EvalSway(-moveY, mvRotAmt.x, mvRotBase.x, mvRotMin.x, mvRotMax.x);
        float mvSwayRotY = EvalSway(moveX, mvRotAmt.y, mvRotBase.y, mvRotMin.y, mvRotMax.y);
        float mvSwayRotZ = EvalSway(moveX, mvRotAmt.z, mvRotBase.z, mvRotMin.z, mvRotMax.z);
        Vector3 targetRotSway = new Vector3((mSwayRotZ * mouseMult) + (mvSwayRotZ * moveMult), (mSwayRotY * mouseMult) + (mvSwayRotY * moveMult), (mSwayRotX * mouseMult) + (mvSwayRotX * moveMult));

        Vector3 curBobPos = Vector3.zero; Vector3 curBobRot = Vector3.zero;

        if (isMoving)
        {
            float freq = isADS ? (isSprinting ? adsBobFreqSprint : adsBobFreqWalk) : (isSprinting ? bobFreqSprint : bobFreqWalk);
            weaponBobTimer += Time.deltaTime * freq * speedRatio;
            Vector3 bobWaves = new Vector3(Mathf.Cos(weaponBobTimer / 2f), Mathf.Sin(weaponBobTimer), Mathf.Sin(weaponBobTimer));

            Vector3 bPosBase = isADS ? adsBobPosBase : bobPosBase; Vector3 bPosMin = isADS ? adsBobPosMin : bobPosMin; Vector3 bPosMax = isADS ? adsBobPosMax : bobPosMax;
            curBobPos = EvalBobWave(bPosBase, bPosMin, bPosMax, bobWaves);

            float noiseMult = isADS ? adsBobPosRandomness : bobPosRandomness;
            if (noiseMult > 0f) { curBobPos += new Vector3((Mathf.PerlinNoise(Time.time * 2f, 1f) - 0.5f) * 2f, (Mathf.PerlinNoise(Time.time * 2f, 2f) - 0.5f) * 2f, (Mathf.PerlinNoise(Time.time * 2f, 3f) - 0.5f) * 2f) * noiseMult; }

            Vector3 bRotBase = isADS ? adsBobRotBase : bobRotBase; Vector3 bRotMin = isADS ? adsBobRotMin : bobRotMin; Vector3 bRotMax = isADS ? adsBobRotMax : bobRotMax;
            curBobRot = EvalBobWave(bRotBase, bRotMin, bRotMax, bobWaves);
        }
        else { weaponBobTimer = 0f; }

        currentSwayPos = Vector3.Lerp(currentSwayPos, targetPosSway + curBobPos, Time.deltaTime * swaySmoothPos);
        currentSwayRot = Vector3.Lerp(currentSwayRot, targetRotSway + curBobRot, Time.deltaTime * swaySmoothRot);
    }

    private void UpdateLaserLogic()
    {
        if (laserLine == null) SetupLaser();
        if (laserLine == null || firePoint == null) return;
        if (!showLaser) { laserLine.enabled = false; return; }

        laserLine.enabled = true;
        laserLine.SetPosition(0, firePoint.position);
        int mask = ~laserIgnoreLayers.value;
        if (Physics.Raycast(firePoint.position, firePoint.forward, out RaycastHit hit, 100f, mask, QueryTriggerInteraction.Ignore)) laserLine.SetPosition(1, hit.point);
        else laserLine.SetPosition(1, firePoint.position + firePoint.forward * 100f);
    }
}