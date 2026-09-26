using UnityEngine;

public partial class TacticalGun
{
    // In-gun cartridges are visual only. Rigidbody physics is reserved for objects
    // that have actually left the gun. The magazine count includes its top round.
    private void SetupAmmoVisuals()
    {
        // These two fields describe *scene visuals*, not prefab assets. A prefab
        // asset has scale 1 in its own space and cannot be toggled as a scene object.
        // Treat accidental asset assignments as empty slots and create proper
        // visual instances under ChamberPoint instead.
        if (liveRoundMesh != null && !IsSceneVisual(liveRoundMesh))
        {
            Debug.LogWarning("[TacticalGun] Live Round Mesh must be a scene object (or None), not a prefab asset. Creating a chamber visual instead.", this);
            liveRoundMesh = null;
        }
        if (spentCasingMesh != null && !IsSceneVisual(spentCasingMesh))
        {
            Debug.LogWarning("[TacticalGun] Spent Casing Mesh must be a scene object (or None), not a prefab asset. Creating a chamber visual instead.", this);
            spentCasingMesh = null;
        }
        if (magazineTopPoint != null && !IsSceneVisual(magazineTopPoint.gameObject)) magazineTopPoint = null;
        if (chamberPoint != null && !IsSceneVisual(chamberPoint.gameObject)) chamberPoint = null;

        // Existing chamber meshes are optional: prefab instances left in the gun
        // work, but deleted ones can be recreated as visual-only runtime clones.
        if (chamberPoint == null && liveRoundMesh != null)
        {
            // Never parent a chamber marker to a visual: hiding the live round
            // must not hide the spent casing or the feed target with it.
            chamberPoint = new GameObject("ChamberPoint (runtime)").transform;
            chamberPoint.SetParent(liveRoundMesh.transform.parent, false);
            chamberPoint.localPosition = liveRoundMesh.transform.localPosition;
            chamberPoint.localRotation = liveRoundMesh.transform.localRotation;
            chamberPoint.localScale = liveRoundMesh.transform.localScale;
        }
        if (chamberPoint == null && ejectionPoint != null && ejectionPoint.parent != null)
        {
            chamberPoint = ejectionPoint.parent.Find("ChamberPoint");
            if (chamberPoint == null)
            {
                chamberPoint = new GameObject("ChamberPoint (runtime)").transform;
                chamberPoint.SetParent(ejectionPoint.parent, false);
                chamberPoint.localPosition = chamberLocalPosition;
            }
        }
        if (magazineTopPoint == null && magMesh != null)
        {
            magazineTopPoint = magMesh.transform.Find("MagazineTopPoint");
            if (magazineTopPoint == null)
            {
                magazineTopPoint = new GameObject("MagazineTopPoint (runtime)").transform;
                magazineTopPoint.SetParent(magMesh.transform, false);
                magazineTopPoint.localPosition = magazineTopLocalPosition;
            }
        }

        if (liveRoundMesh == null && roundPrefab != null && chamberPoint != null)
            liveRoundMesh = CreateChamberVisual(roundPrefab, "ChamberLiveRound");
        if (spentCasingMesh == null && casingPrefab != null && chamberPoint != null)
            spentCasingMesh = CreateChamberVisual(casingPrefab, "ChamberSpentCase");

        // A casing prefab in the hierarchy still has a Rigidbody and a collider;
        // only the ejected clone is allowed to interact with physics.
        if (spentCasingMesh != null)
        {
            Rigidbody chamberBody = spentCasingMesh.GetComponent<Rigidbody>();
            if (chamberBody != null)
            {
                chamberBody.isKinematic = true;
                chamberBody.useGravity = false;
            }
            foreach (Collider collider in spentCasingMesh.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            casingRestLocalPos = spentCasingMesh.transform.localPosition;
            casingRestLocalRot = spentCasingMesh.transform.localRotation;
        }

        if (roundPrefab != null && magazineTopPoint != null && liveRoundMesh != null)
        {
            magazineTopVisual = CreateRoundVisual("MagazineTopRound");
            feedingRoundVisual = CreateRoundVisual("FeedingRound");
        }
        else
        {
            Debug.LogWarning("[TacticalGun] Assign the round/casing prefab assets and a magazine and chamber position to display ammo.", this);
        }

        if (magMesh != null) magMesh.SetActive(isMagazineInserted);
        UpdateAmmoVisuals();
    }

    private bool IsSceneVisual(GameObject visual)
    {
        return visual.scene.IsValid() && visual.scene == gameObject.scene;
    }

    private GameObject CreateChamberVisual(GameObject prefab, string visualName)
    {
        GameObject visual = Instantiate(prefab, chamberPoint);
        visual.name = visualName;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        return visual;
    }

    private GameObject CreateRoundVisual(string visualName)
    {
        GameObject visual = Instantiate(roundPrefab, transform);
        visual.name = visualName;
        visual.transform.localScale = new Vector3(
            liveRoundMesh.transform.lossyScale.x / transform.lossyScale.x,
            liveRoundMesh.transform.lossyScale.y / transform.lossyScale.y,
            liveRoundMesh.transform.lossyScale.z / transform.lossyScale.z);
        foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        Rigidbody body = visual.GetComponent<Rigidbody>();
        if (body != null) { body.isKinematic = true; body.useGravity = false; }
        visual.SetActive(false);
        return visual;
    }

    private void UpdateAmmoVisuals()
    {
        if (liveRoundMesh != null) liveRoundMesh.SetActive(isChamberLoaded && !isChamberSpent);
        if (spentCasingMesh != null) spentCasingMesh.SetActive(isChamberLoaded && isChamberSpent);

        if (magazineTopVisual != null)
        {
            magazineTopVisual.transform.SetPositionAndRotation(magazineTopPoint.position, magazineTopPoint.rotation);
            magazineTopVisual.SetActive(isMagazineInserted && currentMagAmmo > 0 && !isFeedingRound);
        }

        if (feedingRoundVisual != null)
        {
            if (isFeedingRound)
            {
                float t = Mathf.Clamp01(feedingProgress);
                Vector3 start = magazineTopPoint.position;
                Vector3 end = liveRoundMesh.transform.position;
                Vector3 position = Vector3.Lerp(start, end, t) + transform.up * (Mathf.Sin(t * Mathf.PI) * feedArcHeight);
                Quaternion rotation = Quaternion.Slerp(magazineTopPoint.rotation, liveRoundMesh.transform.rotation, t);
                feedingRoundVisual.transform.SetPositionAndRotation(position, rotation);
            }
            feedingRoundVisual.SetActive(isFeedingRound);
        }
    }

    private void BeginFeed()
    {
        if (!isMagazineInserted || currentMagAmmo <= 0 || isChamberLoaded) return;
        isFeedingRound = true;
        feedingProgress = 0f;
    }

    private void CancelFeed()
    {
        isFeedingRound = false;
        feedingProgress = 0f;
    }

    private void AdvanceFeed(float slideReturnProgress)
    {
        if (!isFeedingRound) return;
        float end = Mathf.Max(feedStartOnReturn + 0.01f, feedEndOnReturn);
        feedingProgress = Mathf.InverseLerp(feedStartOnReturn, end, slideReturnProgress);
        if (slideReturnProgress >= end) CompleteFeed();
    }

    private void CompleteFeed()
    {
        if (!isFeedingRound) return;
        isFeedingRound = false;
        if (isMagazineInserted && currentMagAmmo > 0 && !isChamberLoaded)
        {
            currentMagAmmo--;
            isChamberLoaded = true;
            isChamberSpent = false;
        }
    }

    private void BeginSpentCaseExtraction()
    {
        if (spentCasingMesh == null || slide == null || slide.parent == null) return;
        isCasingExtracting = true;
        casingStartInSlideParent = slide.parent.InverseTransformPoint(spentCasingMesh.transform.position);
        slideStartLocalPos = slide.localPosition;
    }

    private void AdvanceSpentCaseExtraction(float slideBackProgress, float catchAt)
    {
        if (!isCasingExtracting || spentCasingMesh == null || slide == null) return;
        // The slide first travels freely while the case remains in the chamber.
        // Only the travel AFTER the catch point moves the case. Doing this in
        // slide-parent space also cancels automatic motion if an older scene has
        // the visual parented to the slide rather than the stationary frame.
        float travelAfterCatch = Mathf.Max(0f, slideBackProgress - catchAt);
        Vector3 fullSlideTravel = slideBasePos + slideRecoilOffset - slideStartLocalPos;
        Vector3 delta = fullSlideTravel * (travelAfterCatch * spentCaseSlideFollow);
        spentCasingMesh.transform.position = slide.parent.TransformPoint(casingStartInSlideParent + delta);
    }

    private void EndSpentCaseExtraction()
    {
        if (!isCasingExtracting) return;
        isCasingExtracting = false;
        if (spentCasingMesh == null) return;
        spentCasingMesh.SetActive(false);
        spentCasingMesh.transform.localPosition = casingRestLocalPos;
        spentCasingMesh.transform.localRotation = casingRestLocalRot;
    }

    private void EjectChamberContents()
    {
        if (!isChamberLoaded) return;
        bool spent = isChamberSpent;
        SpawnEjectedRound(spent);
        PlaySlideOneShot(spent ? spentCaseEjectClips : liveRoundEjectClips);
        isChamberLoaded = false;
        isChamberSpent = false;
        EndSpentCaseExtraction();
    }

    private void SpawnEjectedRound(bool spent)
    {
        GameObject prefab = spent ? casingPrefab : roundPrefab;
        if (prefab == null || ejectionPoint == null) return;

        Quaternion spawnRotation = ejectionPoint.rotation *
            (spent ? Quaternion.Euler(casingEjectionRotationOffset) : Quaternion.identity);
        GameObject ejected = Instantiate(prefab, ejectionPoint.position, spawnRotation);
        ejected.SetActive(true);
        ejected.transform.localScale = spent ? casingScale :
            (liveRoundMesh != null ? liveRoundMesh.transform.lossyScale : casingScale);

        Collider[] colliders = ejected.GetComponentsInChildren<Collider>(true);
        if (colliders.Length == 0)
        {
            MeshFilter mesh = ejected.GetComponentInChildren<MeshFilter>();
            if (mesh != null && mesh.sharedMesh != null)
            {
                Bounds bounds = mesh.sharedMesh.bounds;
                Vector3 size = bounds.size;
                CapsuleCollider capsule = ejected.AddComponent<CapsuleCollider>();
                capsule.center = bounds.center;
                capsule.direction = size.x > size.y && size.x > size.z ? 0 : (size.y > size.z ? 1 : 2);
                capsule.radius = Mathf.Max(0.001f, (capsule.direction == 0 ? Mathf.Max(size.y, size.z) :
                    capsule.direction == 1 ? Mathf.Max(size.x, size.z) : Mathf.Max(size.x, size.y)) * 0.5f);
                capsule.height = Mathf.Max(capsule.radius * 2f,
                    capsule.direction == 0 ? size.x : capsule.direction == 1 ? size.y : size.z);
                colliders = new Collider[] { capsule };
            }
        }
        foreach (Collider collider in colliders) collider.enabled = true;

        Rigidbody body = ejected.GetComponent<Rigidbody>();
        if (body == null) body = ejected.AddComponent<Rigidbody>();
        body.isKinematic = false;
        body.useGravity = true;
        if (!spent) body.mass = 0.018f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Avoid the ejected object striking the invisible Player capsule on its way out.
        if (fpsController != null)
            foreach (Collider playerCollider in fpsController.GetComponents<Collider>())
                foreach (Collider ejectedCollider in colliders)
                    Physics.IgnoreCollision(ejectedCollider, playerCollider);

        body.linearVelocity = ejectionPoint.TransformDirection(ejectionDirection.normalized) * ejectionSpeed;
        Vector3 fixedSpin = spent ? ejectionPoint.TransformDirection(casingSpinBiasLocal) : Vector3.zero;
        body.maxAngularVelocity = Mathf.Max(body.maxAngularVelocity, fixedSpin.magnitude + Mathf.Abs(ejectionSpin));
        body.angularVelocity = fixedSpin + Random.onUnitSphere * Mathf.Abs(ejectionSpin);
        Destroy(ejected, casingLifetime);
    }
}
