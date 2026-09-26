using UnityEngine;

public partial class TacticalGun
{
    private AudioClip activeSlideDragLoop;

    private void PlaySlideOneShot(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;
        if (slideAudioSource == null)
        {
            // One-shots need their own source so changing the pitch of a slide
            // click cannot change the tail of a gunshot still playing.
            GameObject host = shootAudioSource != null ? shootAudioSource.gameObject : gameObject;
            slideAudioSource = host.AddComponent<AudioSource>();
            slideAudioSource.playOnAwake = false;
            if (shootAudioSource != null)
            {
                slideAudioSource.spatialBlend = shootAudioSource.spatialBlend;
                slideAudioSource.volume = shootAudioSource.volume;
                slideAudioSource.outputAudioMixerGroup = shootAudioSource.outputAudioMixerGroup;
            }
        }
        slideAudioSource.pitch = Random.Range(0.95f, 1.05f);
        slideAudioSource.PlayOneShot(clip);
    }

    private void UpdateManualSlideSound(float slideDelta)
    {
        // Only play the click when the hand changes direction, not on every frame.
        if (Mathf.Abs(slideDelta) < 0.002f)
        {
            StopSlideDragLoop();
            return;
        }

        int direction = slideDelta > 0f ? 1 : -1;
        if (direction != manualSlideAudioDirection)
        {
            PlaySlideOneShot(direction > 0 ? slideBackClips : slideForwardClips);
            manualSlideAudioDirection = direction;
        }

        AudioClip loop = direction > 0 ? slideBackDragLoop : slideForwardDragLoop;
        if (loop == null)
        {
            StopSlideDragLoop();
            return;
        }

        // The friction sound MUST have its own AudioSource: Stop()/pitch on the
        // shot or one-shot source would interrupt other sounds.
        if (slideDragAudioSource == null)
        {
            GameObject host = shootAudioSource != null ? shootAudioSource.gameObject : gameObject;
            slideDragAudioSource = host.AddComponent<AudioSource>();
            slideDragAudioSource.playOnAwake = false;
            slideDragAudioSource.spatialBlend = shootAudioSource != null ? shootAudioSource.spatialBlend : 1f;
        }
        if (slideDragAudioSource == shootAudioSource || slideDragAudioSource == slideAudioSource) return;

        if (activeSlideDragLoop != loop || !slideDragAudioSource.isPlaying)
        {
            slideDragAudioSource.Stop();
            slideDragAudioSource.clip = loop;
            slideDragAudioSource.loop = true;
            slideDragAudioSource.Play();
            activeSlideDragLoop = loop;
        }
        float speed = Mathf.Abs(slideDelta) / Mathf.Max(Time.deltaTime, 0.001f);
        float normalizedSpeed = Mathf.Clamp01(speed / 3f);
        slideDragAudioSource.pitch = Mathf.Lerp(slideDragMinPitch, slideDragMaxPitch, normalizedSpeed);
        slideDragAudioSource.volume = slideDragLoopVolume * Mathf.Lerp(0.35f, 1f, normalizedSpeed);
    }

    private void StopSlideDragLoop()
    {
        if (activeSlideDragLoop == null || slideDragAudioSource == null) return;
        slideDragAudioSource.Stop();
        slideDragAudioSource.loop = false;
        slideDragAudioSource.clip = null;
        activeSlideDragLoop = null;
    }

    private void ResetManualSlideAudio()
    {
        StopSlideDragLoop();
        manualSlideAudioDirection = 0;
    }
}
