using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Video;

/// <summary>
/// Handles prototype media-wall playback, zone-driven autoplay, click toggles, and fallback pulse feedback.
/// </summary>
public class HO_MediaVideoPlayer : MonoBehaviour
{
    /// <summary>
    /// Describes the current runtime playback mode so other systems can react without touching VideoPlayer directly.
    /// </summary>
    public enum MediaPlaybackState
    {
        Stopped,
        Playing,
        Paused,
        FallbackActive
    }

    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private bool playOnZoneEnter = true;
    [SerializeField] private bool playOnClick = true;
    [SerializeField] private HO_GalleryZoneFlow galleryZoneFlow;
    [SerializeField] private bool loopPlayback = true;
    [SerializeField] private float clickMaxDistance = 8.0f;
    [SerializeField] private string mediaZoneId = "MEDIA";
    [SerializeField] private float fallbackPulseSpeed = 2.2f;

    private Camera cachedInputCamera;
    private HO_GalleryZoneFlow subscribedZoneFlow;

    /// <summary>
    /// Returns the current playback state for runtime systems such as reactive lighting.
    /// </summary>
    public MediaPlaybackState CurrentPlaybackState { get; private set; } = MediaPlaybackState.Stopped;

    /// <summary>
    /// Returns true while video playback or fallback playback feedback is actively running.
    /// </summary>
    public bool IsPlayingMedia =>
        CurrentPlaybackState == MediaPlaybackState.Playing ||
        CurrentPlaybackState == MediaPlaybackState.FallbackActive;

    /// <summary>
    /// Returns a normalized playback weight that other systems can consume directly.
    /// </summary>
    public float PlaybackActivity
    {
        get
        {
            switch (CurrentPlaybackState)
            {
                case MediaPlaybackState.Playing:
                    return 1.0f;
                case MediaPlaybackState.FallbackActive:
                    return 0.85f;
                case MediaPlaybackState.Paused:
                    return 0.35f;
                default:
                    return 0.0f;
            }
        }
    }

    /// <summary>
    /// Returns a normalized pulse value that stays available even when no clip is assigned.
    /// </summary>
    public float ReactivePulse { get; private set; }

    /// <summary>
    /// Caches scene references when the component is first added.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
    }

    /// <summary>
    /// Prepares cached references and playback configuration before runtime input starts.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
        ConfigureVideoOutput();
        RegisterVideoCallbacks();
        RefreshReactivePulse();
    }

    /// <summary>
    /// Reconnects zone callbacks and evaluates the current zone state when the component becomes active.
    /// </summary>
    private void OnEnable()
    {
        TrySubscribeZoneFlow();
        EvaluateZoneState();
    }

    /// <summary>
    /// Stops event subscriptions and pauses active output when the component is disabled.
    /// </summary>
    private void OnDisable()
    {
        UnregisterZoneFlow();
        PauseOutputOnDisable();
    }

    /// <summary>
    /// Removes runtime callbacks when the component is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        UnregisterZoneFlow();
        UnregisterVideoCallbacks();
    }

    /// <summary>
    /// Reads Old Input click toggles, syncs the playback state, and refreshes the fallback pulse value.
    /// </summary>
    private void Update()
    {
        TrySubscribeZoneFlow();
        HandleClickInput();
        SyncStateWithVideoOutput();
        RefreshReactivePulse();
    }

    /// <summary>
    /// Starts playback or fallback feedback depending on whether a playable clip or URL is configured.
    /// </summary>
    public void PlayMedia()
    {
        ConfigureVideoOutput();

        if (HasConfiguredMedia())
        {
            videoPlayer.Play();
            CurrentPlaybackState = MediaPlaybackState.Playing;
        }
        else
        {
            StopAudioOutput();
            CurrentPlaybackState = MediaPlaybackState.FallbackActive;
        }

        RefreshReactivePulse();
    }

    /// <summary>
    /// Pauses active playback or fallback feedback without resetting the current media setup.
    /// </summary>
    public void PauseMedia()
    {
        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Pause();
        }

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Pause();
        }

        if (CurrentPlaybackState != MediaPlaybackState.Stopped)
        {
            CurrentPlaybackState = MediaPlaybackState.Paused;
        }

        RefreshReactivePulse();
    }

    /// <summary>
    /// Stops playback, audio output, and fallback feedback completely.
    /// </summary>
    public void StopMedia()
    {
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }

        StopAudioOutput();
        CurrentPlaybackState = MediaPlaybackState.Stopped;
        ReactivePulse = 0.0f;
    }

    /// <summary>
    /// Toggles between play and pause states for click-driven interactions.
    /// </summary>
    public void TogglePlayback()
    {
        if (IsPlayingMedia)
        {
            PauseMedia();
            return;
        }

        PlayMedia();
    }

    /// <summary>
    /// Updates the loop playback flag and forwards it to the configured VideoPlayer.
    /// </summary>
    public void SetLoopPlayback(bool shouldLoop)
    {
        loopPlayback = shouldLoop;

        if (videoPlayer != null)
        {
            videoPlayer.isLooping = shouldLoop;
        }
    }

    /// <summary>
    /// Finds optional scene references while keeping the component safe when references are missing.
    /// </summary>
    private void CacheReferences()
    {
        if (videoPlayer == null)
        {
            videoPlayer = GetComponent<VideoPlayer>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (galleryZoneFlow == null)
        {
            galleryZoneFlow = FindFirstObjectByType<HO_GalleryZoneFlow>();
        }

        if (cachedInputCamera == null)
        {
            cachedInputCamera = Camera.main;
        }
    }

    /// <summary>
    /// Configures the linked VideoPlayer and AudioSource for prototype playback use.
    /// </summary>
    private void ConfigureVideoOutput()
    {
        if (videoPlayer == null)
        {
            return;
        }

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = loopPlayback;
        videoPlayer.skipOnDrop = true;
        videoPlayer.waitForFirstFrame = true;

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.SetTargetAudioSource(0, audioSource);
        }
        else if (videoPlayer.audioOutputMode == VideoAudioOutputMode.AudioSource)
        {
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        }
    }

    /// <summary>
    /// Registers the VideoPlayer loop callback so the public playback state stays in sync.
    /// </summary>
    private void RegisterVideoCallbacks()
    {
        if (videoPlayer == null)
        {
            return;
        }

        videoPlayer.loopPointReached -= HandleLoopPointReached;
        videoPlayer.loopPointReached += HandleLoopPointReached;
    }

    /// <summary>
    /// Removes the VideoPlayer loop callback during teardown.
    /// </summary>
    private void UnregisterVideoCallbacks()
    {
        if (videoPlayer == null)
        {
            return;
        }

        videoPlayer.loopPointReached -= HandleLoopPointReached;
    }

    /// <summary>
    /// Subscribes to zone-change callbacks when zone-driven autoplay is enabled.
    /// </summary>
    private void TrySubscribeZoneFlow()
    {
        if (!playOnZoneEnter)
        {
            return;
        }

        if (galleryZoneFlow == null)
        {
            galleryZoneFlow = FindFirstObjectByType<HO_GalleryZoneFlow>();
        }

        if (galleryZoneFlow == null || subscribedZoneFlow == galleryZoneFlow)
        {
            return;
        }

        UnregisterZoneFlow();
        subscribedZoneFlow = galleryZoneFlow;
        subscribedZoneFlow.ZoneChanged += HandleZoneChanged;
    }

    /// <summary>
    /// Disconnects the current zone callback subscription safely.
    /// </summary>
    private void UnregisterZoneFlow()
    {
        if (subscribedZoneFlow == null)
        {
            return;
        }

        subscribedZoneFlow.ZoneChanged -= HandleZoneChanged;
        subscribedZoneFlow = null;
    }

    /// <summary>
    /// Applies autoplay rules to the current zone state when the component first enables.
    /// </summary>
    private void EvaluateZoneState()
    {
        if (!playOnZoneEnter || galleryZoneFlow == null)
        {
            return;
        }

        ApplyZonePlaybackRule(galleryZoneFlow.CurrentZoneId);
    }

    /// <summary>
    /// Applies enter and exit playback behavior whenever the tracked gallery zone changes.
    /// </summary>
    private void HandleZoneChanged(string currentZoneId, string nextZoneId)
    {
        ApplyZonePlaybackRule(currentZoneId);
    }

    /// <summary>
    /// Starts media on media-zone entry and stops it when leaving the media zone.
    /// </summary>
    private void ApplyZonePlaybackRule(string currentZoneId)
    {
        if (IsMediaZoneId(currentZoneId))
        {
            if (CurrentPlaybackState == MediaPlaybackState.Stopped)
            {
                PlayMedia();
            }

            return;
        }

        if (CurrentPlaybackState != MediaPlaybackState.Stopped)
        {
            StopMedia();
        }
    }

    /// <summary>
    /// Reads Old Input mouse clicks and toggles playback when this media object is clicked directly.
    /// </summary>
    private void HandleClickInput()
    {
        if (!playOnClick || !Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Camera inputCamera = GetInputCamera();
        if (inputCamera == null)
        {
            return;
        }

        Ray ray = inputCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, clickMaxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

        float nearestDistance = float.MaxValue;
        bool clickedThisMediaObject = false;

        for (int index = 0; index < hits.Length; index++)
        {
            Transform hitTransform = hits[index].collider != null ? hits[index].collider.transform : null;
            if (hitTransform == null || !IsPartOfThisMediaObject(hitTransform))
            {
                continue;
            }

            if (hits[index].distance < nearestDistance)
            {
                nearestDistance = hits[index].distance;
                clickedThisMediaObject = true;
            }
        }

        if (clickedThisMediaObject)
        {
            TogglePlayback();
        }
    }

    /// <summary>
    /// Keeps the public state aligned with the underlying VideoPlayer state at runtime.
    /// </summary>
    private void SyncStateWithVideoOutput()
    {
        if (CurrentPlaybackState == MediaPlaybackState.FallbackActive)
        {
            return;
        }

        if (!HasConfiguredMedia())
        {
            if (CurrentPlaybackState == MediaPlaybackState.Playing)
            {
                CurrentPlaybackState = MediaPlaybackState.FallbackActive;
            }

            return;
        }

        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            CurrentPlaybackState = MediaPlaybackState.Playing;
            return;
        }

        if (CurrentPlaybackState != MediaPlaybackState.Playing)
        {
            return;
        }

        if (!loopPlayback && IsAtPlaybackEnd())
        {
            CurrentPlaybackState = MediaPlaybackState.Stopped;
            return;
        }

        CurrentPlaybackState = MediaPlaybackState.Paused;
    }

    /// <summary>
    /// Updates the fallback and playback pulse value so connected emissive systems can animate safely.
    /// </summary>
    private void RefreshReactivePulse()
    {
        if (CurrentPlaybackState == MediaPlaybackState.Stopped)
        {
            ReactivePulse = 0.0f;
            return;
        }

        float pulseTime = GetPulseTime();
        float pulseSpeed = CurrentPlaybackState == MediaPlaybackState.FallbackActive ? fallbackPulseSpeed : 3.0f;
        float pulseFloor = CurrentPlaybackState == MediaPlaybackState.Paused ? 0.12f : 0.28f;
        float pulseAmplitude = CurrentPlaybackState == MediaPlaybackState.FallbackActive
            ? 0.52f
            : CurrentPlaybackState == MediaPlaybackState.Playing
                ? 0.38f
                : 0.16f;

        ReactivePulse = Mathf.Clamp01(
            pulseFloor +
            (Mathf.Sin(pulseTime * pulseSpeed) * 0.5f + 0.5f) * pulseAmplitude);
    }

    /// <summary>
    /// Keeps disabled objects from leaving audio or video output running in the scene.
    /// </summary>
    private void PauseOutputOnDisable()
    {
        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Pause();
        }

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Pause();
        }

        if (IsPlayingMedia)
        {
            CurrentPlaybackState = MediaPlaybackState.Paused;
        }

        RefreshReactivePulse();
    }

    /// <summary>
    /// Stops the dedicated AudioSource when playback falls back or ends.
    /// </summary>
    private void StopAudioOutput()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }

    /// <summary>
    /// Returns the best available camera for click-based playback toggles.
    /// </summary>
    private Camera GetInputCamera()
    {
        if (cachedInputCamera == null)
        {
            cachedInputCamera = Camera.main;
        }

        return cachedInputCamera;
    }

    /// <summary>
    /// Returns true when the clicked collider belongs to this media object or one of its parents or children.
    /// </summary>
    private bool IsPartOfThisMediaObject(Transform hitTransform)
    {
        return hitTransform == transform ||
               hitTransform.IsChildOf(transform) ||
               transform.IsChildOf(hitTransform);
    }

    /// <summary>
    /// Returns true when the current VideoPlayer has a playable clip or URL configured.
    /// </summary>
    private bool HasConfiguredMedia()
    {
        if (videoPlayer == null)
        {
            return false;
        }

        if (videoPlayer.source == VideoSource.Url)
        {
            return !string.IsNullOrWhiteSpace(videoPlayer.url);
        }

        return videoPlayer.clip != null;
    }

    /// <summary>
    /// Returns true when the current zone id maps to the media-room part of the gallery route.
    /// </summary>
    private bool IsMediaZoneId(string zoneId)
    {
        if (string.IsNullOrWhiteSpace(zoneId))
        {
            return false;
        }

        return zoneId.IndexOf(mediaZoneId, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Returns true when non-looping playback has effectively reached the end of the current media.
    /// </summary>
    private bool IsAtPlaybackEnd()
    {
        if (videoPlayer == null)
        {
            return false;
        }

        if (videoPlayer.frameCount > 0 && videoPlayer.frame >= (long)videoPlayer.frameCount - 1L)
        {
            return true;
        }

        if (videoPlayer.length > 0.0d && videoPlayer.time >= videoPlayer.length - 0.05d)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns a stable pulse time source from the current video time or the global clock.
    /// </summary>
    private float GetPulseTime()
    {
        if (videoPlayer != null && videoPlayer.isPlaying && !double.IsNaN(videoPlayer.time) && !double.IsInfinity(videoPlayer.time))
        {
            return (float)videoPlayer.time;
        }

        return Time.time;
    }

    /// <summary>
    /// Updates the public state when the VideoPlayer reaches its loop point.
    /// </summary>
    private void HandleLoopPointReached(VideoPlayer source)
    {
        if (loopPlayback)
        {
            CurrentPlaybackState = MediaPlaybackState.Playing;
            return;
        }

        StopAudioOutput();
        CurrentPlaybackState = MediaPlaybackState.Stopped;
        ReactivePulse = 0.0f;
    }
}
