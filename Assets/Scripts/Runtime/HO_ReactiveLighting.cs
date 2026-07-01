using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Drives connected lights and emissive renderers from zone, selection, and media playback state.
/// </summary>
public class HO_ReactiveLighting : MonoBehaviour
{
    /// <summary>
    /// Stores the prototype lighting mood for a gallery zone.
    /// </summary>
    private readonly struct ZoneLightingProfile
    {
        public ZoneLightingProfile(
            Color lightTint,
            float lightTintStrength,
            float lightMultiplier,
            Color emissionTint,
            float emissionTintStrength,
            float emissionMultiplier)
        {
            LightTint = lightTint;
            LightTintStrength = lightTintStrength;
            LightMultiplier = lightMultiplier;
            EmissionTint = emissionTint;
            EmissionTintStrength = emissionTintStrength;
            EmissionMultiplier = emissionMultiplier;
        }

        public Color LightTint { get; }

        public float LightTintStrength { get; }

        public float LightMultiplier { get; }

        public Color EmissionTint { get; }

        public float EmissionTintStrength { get; }

        public float EmissionMultiplier { get; }
    }

    /// <summary>
    /// Caches each controlled light's original look so transitions can stay relative.
    /// </summary>
    private struct LightRuntimeData
    {
        public LightRuntimeData(Light targetLight)
        {
            Light = targetLight;
            OriginalIntensity = targetLight != null ? targetLight.intensity : 0.0f;
            OriginalColor = targetLight != null ? targetLight.color : Color.white;
        }

        public Light Light;
        public float OriginalIntensity;
        public Color OriginalColor;
    }

    /// <summary>
    /// Caches each controlled renderer's emission-capable material data for lightweight runtime updates.
    /// </summary>
    private struct RendererRuntimeData
    {
        public RendererRuntimeData(Renderer targetRenderer, int materialCount)
        {
            Renderer = targetRenderer;
            MaterialCount = materialCount;
            PropertyBlocks = new MaterialPropertyBlock[materialCount];
            OriginalEmissionColors = new Color[materialCount];
            OriginalBaseColors = new Color[materialCount];
            HasEmissionColor = new bool[materialCount];
            HasBaseColor = new bool[materialCount];
            HasColor = new bool[materialCount];
        }

        public Renderer Renderer;
        public int MaterialCount;
        public MaterialPropertyBlock[] PropertyBlocks;
        public Color[] OriginalEmissionColors;
        public Color[] OriginalBaseColors;
        public bool[] HasEmissionColor;
        public bool[] HasBaseColor;
        public bool[] HasColor;
    }

    [SerializeField] private HO_GalleryZoneFlow galleryZoneFlow;
    [SerializeField] private HO_ArtworkSelector artworkSelector;
    [SerializeField] private HO_MediaVideoPlayer[] mediaVideoPlayers;
    [SerializeField] private Light[] targetLights;
    [SerializeField] private Renderer[] emissiveRenderers;
    [SerializeField] private float transitionDuration = 0.45f;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly Color SelectionAccentTint = new Color(1.0f, 0.82f, 0.58f, 1.0f);
    private static readonly Color VideoAccentTint = new Color(0.40f, 0.68f, 1.0f, 1.0f);
    private static readonly ZoneLightingProfile DefaultProfile = new ZoneLightingProfile(
        Color.white,
        0.08f,
        1.0f,
        new Color(1.0f, 0.96f, 0.90f, 1.0f),
        0.14f,
        0.90f);
    private static readonly ZoneLightingProfile LobbyProfile = new ZoneLightingProfile(
        new Color(1.0f, 0.92f, 0.82f, 1.0f),
        0.18f,
        1.05f,
        new Color(1.0f, 0.88f, 0.70f, 1.0f),
        0.22f,
        0.85f);
    private static readonly ZoneLightingProfile HallwayProfile = new ZoneLightingProfile(
        new Color(1.0f, 0.94f, 0.86f, 1.0f),
        0.12f,
        0.95f,
        new Color(0.96f, 0.88f, 0.72f, 1.0f),
        0.18f,
        0.78f);
    private static readonly ZoneLightingProfile AbstractProfile = new ZoneLightingProfile(
        new Color(0.98f, 0.96f, 0.92f, 1.0f),
        0.10f,
        1.08f,
        new Color(1.0f, 0.92f, 0.80f, 1.0f),
        0.22f,
        0.95f);
    private static readonly ZoneLightingProfile MediaProfile = new ZoneLightingProfile(
        new Color(0.62f, 0.72f, 1.0f, 1.0f),
        0.35f,
        0.46f,
        new Color(0.30f, 0.54f, 1.0f, 1.0f),
        0.42f,
        1.15f);
    private static readonly ZoneLightingProfile CuratorProfile = new ZoneLightingProfile(
        new Color(1.0f, 0.90f, 0.78f, 1.0f),
        0.22f,
        0.98f,
        new Color(1.0f, 0.78f, 0.52f, 1.0f),
        0.28f,
        0.90f);
    private static readonly ZoneLightingProfile LoungeProfile = new ZoneLightingProfile(
        new Color(1.0f, 0.82f, 0.66f, 1.0f),
        0.25f,
        0.82f,
        new Color(1.0f, 0.74f, 0.46f, 1.0f),
        0.34f,
        1.00f);

    private PropertyInfo selectedInteractableProperty;
    private FieldInfo selectedInteractableField;
    private LightRuntimeData[] lightRuntimeData = new LightRuntimeData[0];
    private RendererRuntimeData[] rendererRuntimeData = new RendererRuntimeData[0];
    private float currentLightMultiplier = 1.0f;
    private float currentLightTintStrength;
    private float currentEmissionMultiplier = 1.0f;
    private float currentEmissionTintStrength;
    private float targetLightMultiplier = 1.0f;
    private float targetLightTintStrength;
    private float targetEmissionMultiplier = 1.0f;
    private float targetEmissionTintStrength;
    private Color currentLightTint = Color.white;
    private Color currentEmissionTint = Color.white;
    private Color targetLightTint = Color.white;
    private Color targetEmissionTint = Color.white;

    /// <summary>
    /// Caches scene references when the component is first added.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
    }

    /// <summary>
    /// Caches reference accessors and original lighting data before runtime transitions begin.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
        CacheSelectorAccessors();
        CacheLightRuntimeData();
        CacheRendererRuntimeData();
        UpdateTargetState();
        SnapCurrentStateToTarget();
        ApplyCurrentState();
    }

    /// <summary>
    /// Clamps transition timing to safe prototype values in the inspector.
    /// </summary>
    private void OnValidate()
    {
        transitionDuration = Mathf.Max(0.0f, transitionDuration);
    }

    /// <summary>
    /// Recomputes target lighting, smooths toward it, and applies the controlled light and emission values.
    /// </summary>
    private void Update()
    {
        UpdateTargetState();
        SmoothTowardTargetState();
        ApplyCurrentState();
    }

    /// <summary>
    /// Fills optional runtime references so the lighting system remains null-safe.
    /// </summary>
    private void CacheReferences()
    {
        if (galleryZoneFlow == null)
        {
            galleryZoneFlow = FindFirstObjectByType<HO_GalleryZoneFlow>();
        }

        if (artworkSelector == null)
        {
            artworkSelector = FindFirstObjectByType<HO_ArtworkSelector>();
        }

        if (mediaVideoPlayers == null || mediaVideoPlayers.Length == 0)
        {
            mediaVideoPlayers = FindObjectsByType<HO_MediaVideoPlayer>(FindObjectsSortMode.None);
        }

        transitionDuration = Mathf.Max(0.0f, transitionDuration);
    }

    /// <summary>
    /// Caches the selector members that can expose the current selected interactable.
    /// </summary>
    private void CacheSelectorAccessors()
    {
        Type selectorType = typeof(HO_ArtworkSelector);
        selectedInteractableProperty = selectorType.GetProperty(
            "SelectedInteractable",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        selectedInteractableField = selectorType.GetField(
            "selectedInteractable",
            BindingFlags.Instance | BindingFlags.NonPublic);
    }

    /// <summary>
    /// Records the original intensity and color of each controlled light.
    /// </summary>
    private void CacheLightRuntimeData()
    {
        if (targetLights == null || targetLights.Length == 0)
        {
            lightRuntimeData = new LightRuntimeData[0];
            return;
        }

        List<LightRuntimeData> runtimeData = new List<LightRuntimeData>(targetLights.Length);

        for (int index = 0; index < targetLights.Length; index++)
        {
            Light targetLight = targetLights[index];
            if (targetLight == null)
            {
                continue;
            }

            runtimeData.Add(new LightRuntimeData(targetLight));
        }

        lightRuntimeData = runtimeData.ToArray();
    }

    /// <summary>
    /// Records the original emission-capable material values of each controlled renderer.
    /// </summary>
    private void CacheRendererRuntimeData()
    {
        if (emissiveRenderers == null || emissiveRenderers.Length == 0)
        {
            rendererRuntimeData = new RendererRuntimeData[0];
            return;
        }

        List<RendererRuntimeData> runtimeData = new List<RendererRuntimeData>(emissiveRenderers.Length);

        for (int rendererIndex = 0; rendererIndex < emissiveRenderers.Length; rendererIndex++)
        {
            Renderer targetRenderer = emissiveRenderers[rendererIndex];
            if (targetRenderer == null)
            {
                continue;
            }

            Material[] liveMaterials = targetRenderer.materials;
            Material[] sharedMaterials = targetRenderer.sharedMaterials;
            int materialCount = liveMaterials != null && liveMaterials.Length > 0
                ? liveMaterials.Length
                : sharedMaterials != null
                    ? sharedMaterials.Length
                    : 0;

            if (materialCount == 0)
            {
                continue;
            }

            RendererRuntimeData rendererData = new RendererRuntimeData(targetRenderer, materialCount);

            for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
            {
                Material liveMaterial = liveMaterials != null && materialIndex < liveMaterials.Length ? liveMaterials[materialIndex] : null;
                Material sharedMaterial = sharedMaterials != null && materialIndex < sharedMaterials.Length ? sharedMaterials[materialIndex] : null;

                rendererData.PropertyBlocks[materialIndex] = new MaterialPropertyBlock();
                rendererData.HasEmissionColor[materialIndex] = MaterialHasProperty(sharedMaterial, liveMaterial, EmissionColorId);
                rendererData.HasBaseColor[materialIndex] = MaterialHasProperty(sharedMaterial, liveMaterial, BaseColorId);
                rendererData.HasColor[materialIndex] = MaterialHasProperty(sharedMaterial, liveMaterial, ColorId);
                rendererData.OriginalBaseColors[materialIndex] = ReadBaseColor(sharedMaterial, liveMaterial);
                rendererData.OriginalEmissionColors[materialIndex] = ReadColor(
                    sharedMaterial,
                    liveMaterial,
                    EmissionColorId,
                    rendererData.OriginalBaseColors[materialIndex] * 0.08f);

                if (rendererData.OriginalEmissionColors[materialIndex].maxColorComponent <= 0.001f)
                {
                    rendererData.OriginalEmissionColors[materialIndex] = rendererData.OriginalBaseColors[materialIndex] * 0.08f;
                }

                if (liveMaterial != null && rendererData.HasEmissionColor[materialIndex])
                {
                    liveMaterial.EnableKeyword("_EMISSION");
                }
            }

            runtimeData.Add(rendererData);
        }

        rendererRuntimeData = runtimeData.ToArray();
    }

    /// <summary>
    /// Computes the next lighting target from zone ambience, artwork selection, and media playback state.
    /// </summary>
    private void UpdateTargetState()
    {
        string currentZoneId = galleryZoneFlow != null ? galleryZoneFlow.CurrentZoneId : string.Empty;
        ZoneLightingProfile zoneProfile = GetZoneProfile(currentZoneId);
        GalleryInteractable selectedInteractable = GetSelectedInteractable();
        float selectionWeight = selectedInteractable != null && IsSelectionRelevantToZone(selectedInteractable, currentZoneId) ? 1.0f : 0.0f;
        float videoWeight = GetVideoActivity(out float videoPulse);
        float overlapWeight = Mathf.Min(selectionWeight, videoWeight);
        float dampedSelectionWeight = selectionWeight * (1.0f - overlapWeight * 0.25f);
        float dampedVideoWeight = videoWeight * (1.0f - overlapWeight * 0.15f);
        float accentBlend = Mathf.Clamp01(dampedSelectionWeight * 0.45f + dampedVideoWeight * 0.65f);
        float videoBlend = dampedVideoWeight > 0.0f
            ? dampedVideoWeight / Mathf.Max(dampedSelectionWeight + dampedVideoWeight, 0.0001f)
            : 0.0f;
        Color accentTint = Color.Lerp(SelectionAccentTint, VideoAccentTint, videoBlend);

        targetLightMultiplier = Mathf.Max(0.05f, zoneProfile.LightMultiplier + dampedSelectionWeight * 0.08f + dampedVideoWeight * 0.12f);
        targetEmissionMultiplier = Mathf.Max(0.05f, zoneProfile.EmissionMultiplier + dampedSelectionWeight * 0.20f + dampedVideoWeight * 0.32f + videoPulse * 0.12f);
        targetLightTint = Color.Lerp(zoneProfile.LightTint, accentTint, accentBlend * 0.08f);
        targetEmissionTint = Color.Lerp(zoneProfile.EmissionTint, accentTint, accentBlend * 0.20f);
        targetLightTintStrength = Mathf.Clamp01(zoneProfile.LightTintStrength + accentBlend * 0.05f);
        targetEmissionTintStrength = Mathf.Clamp01(zoneProfile.EmissionTintStrength + dampedSelectionWeight * 0.08f + dampedVideoWeight * 0.15f);
    }

    /// <summary>
    /// Smooths current runtime lighting values toward the most recent target values.
    /// </summary>
    private void SmoothTowardTargetState()
    {
        float interpolationFactor = transitionDuration <= 0.0f
            ? 1.0f
            : 1.0f - Mathf.Exp(-Time.deltaTime / transitionDuration);

        currentLightMultiplier = Mathf.Lerp(currentLightMultiplier, targetLightMultiplier, interpolationFactor);
        currentEmissionMultiplier = Mathf.Lerp(currentEmissionMultiplier, targetEmissionMultiplier, interpolationFactor);
        currentLightTintStrength = Mathf.Lerp(currentLightTintStrength, targetLightTintStrength, interpolationFactor);
        currentEmissionTintStrength = Mathf.Lerp(currentEmissionTintStrength, targetEmissionTintStrength, interpolationFactor);
        currentLightTint = Color.Lerp(currentLightTint, targetLightTint, interpolationFactor);
        currentEmissionTint = Color.Lerp(currentEmissionTint, targetEmissionTint, interpolationFactor);
    }

    /// <summary>
    /// Applies the smoothed light and emission state to the connected runtime targets only.
    /// </summary>
    private void ApplyCurrentState()
    {
        ApplyLightState();
        ApplyEmissionState();
    }

    /// <summary>
    /// Applies relative intensity and tint updates to each connected light.
    /// </summary>
    private void ApplyLightState()
    {
        for (int index = 0; index < lightRuntimeData.Length; index++)
        {
            Light targetLight = lightRuntimeData[index].Light;
            if (targetLight == null)
            {
                continue;
            }

            targetLight.intensity = lightRuntimeData[index].OriginalIntensity * currentLightMultiplier;
            targetLight.color = TintColor(lightRuntimeData[index].OriginalColor, currentLightTint, currentLightTintStrength);
        }
    }

    /// <summary>
    /// Applies relative emission or safe surface-color fallback updates to each connected renderer.
    /// </summary>
    private void ApplyEmissionState()
    {
        for (int rendererIndex = 0; rendererIndex < rendererRuntimeData.Length; rendererIndex++)
        {
            RendererRuntimeData rendererData = rendererRuntimeData[rendererIndex];
            if (rendererData.Renderer == null)
            {
                continue;
            }

            for (int materialIndex = 0; materialIndex < rendererData.MaterialCount; materialIndex++)
            {
                MaterialPropertyBlock propertyBlock = rendererData.PropertyBlocks[materialIndex];
                rendererData.Renderer.GetPropertyBlock(propertyBlock, materialIndex);

                if (rendererData.HasEmissionColor[materialIndex])
                {
                    Color targetEmission = TintColor(
                            rendererData.OriginalEmissionColors[materialIndex],
                            currentEmissionTint,
                            currentEmissionTintStrength) *
                        currentEmissionMultiplier;

                    propertyBlock.SetColor(EmissionColorId, targetEmission);
                }
                else if (rendererData.HasBaseColor[materialIndex])
                {
                    Color surfaceColor = TintColor(
                        rendererData.OriginalBaseColors[materialIndex],
                        currentEmissionTint,
                        currentEmissionTintStrength * 0.65f);
                    float surfaceBoost = 1.0f + Mathf.Max(0.0f, currentEmissionMultiplier - 1.0f) * 0.22f;
                    propertyBlock.SetColor(BaseColorId, surfaceColor * surfaceBoost);
                }
                else if (rendererData.HasColor[materialIndex])
                {
                    Color surfaceColor = TintColor(
                        rendererData.OriginalBaseColors[materialIndex],
                        currentEmissionTint,
                        currentEmissionTintStrength * 0.65f);
                    float surfaceBoost = 1.0f + Mathf.Max(0.0f, currentEmissionMultiplier - 1.0f) * 0.22f;
                    propertyBlock.SetColor(ColorId, surfaceColor * surfaceBoost);
                }

                rendererData.Renderer.SetPropertyBlock(propertyBlock, materialIndex);
            }
        }
    }

    /// <summary>
    /// Maps the current zone id to the prototype ambience profile used for connected lights and emitters.
    /// </summary>
    private static ZoneLightingProfile GetZoneProfile(string zoneId)
    {
        if (MatchesZone(zoneId, "LOBBY"))
        {
            return LobbyProfile;
        }

        if (MatchesZone(zoneId, "HALLWAY"))
        {
            return HallwayProfile;
        }

        if (MatchesZone(zoneId, "ABSTRACT"))
        {
            return AbstractProfile;
        }

        if (MatchesZone(zoneId, "MEDIA"))
        {
            return MediaProfile;
        }

        if (MatchesZone(zoneId, "CURATOR"))
        {
            return CuratorProfile;
        }

        if (MatchesZone(zoneId, "LOUNGE"))
        {
            return LoungeProfile;
        }

        return DefaultProfile;
    }

    /// <summary>
    /// Reads the current artwork selection from the selector property first and field fallback second.
    /// </summary>
    private GalleryInteractable GetSelectedInteractable()
    {
        if (artworkSelector == null)
        {
            return null;
        }

        if (selectedInteractableProperty != null &&
            typeof(GalleryInteractable).IsAssignableFrom(selectedInteractableProperty.PropertyType))
        {
            object propertyValue = selectedInteractableProperty.GetValue(artworkSelector, null);
            return propertyValue as GalleryInteractable;
        }

        if (selectedInteractableField != null &&
            typeof(GalleryInteractable).IsAssignableFrom(selectedInteractableField.FieldType))
        {
            object fieldValue = selectedInteractableField.GetValue(artworkSelector);
            return fieldValue as GalleryInteractable;
        }

        return null;
    }

    /// <summary>
    /// Returns true when the selected artwork belongs to the current zone or no zone mapping is available.
    /// </summary>
    private static bool IsSelectionRelevantToZone(GalleryInteractable selectedInteractable, string currentZoneId)
    {
        if (selectedInteractable == null)
        {
            return false;
        }

        string selectedZoneId = GetZoneIdFromTransform(selectedInteractable.transform);
        if (string.IsNullOrWhiteSpace(currentZoneId) || string.IsNullOrWhiteSpace(selectedZoneId))
        {
            return true;
        }

        return MatchesZone(currentZoneId, selectedZoneId);
    }

    /// <summary>
    /// Returns the strongest media playback weight and pulse value from the connected media players.
    /// </summary>
    private float GetVideoActivity(out float videoPulse)
    {
        float strongestPlayback = 0.0f;
        float strongestPulse = 0.0f;

        if (mediaVideoPlayers != null)
        {
            for (int index = 0; index < mediaVideoPlayers.Length; index++)
            {
                HO_MediaVideoPlayer mediaVideoPlayer = mediaVideoPlayers[index];
                if (mediaVideoPlayer == null)
                {
                    continue;
                }

                strongestPlayback = Mathf.Max(strongestPlayback, mediaVideoPlayer.PlaybackActivity);
                strongestPulse = Mathf.Max(strongestPulse, mediaVideoPlayer.ReactivePulse);
            }
        }

        videoPulse = strongestPulse;
        return strongestPlayback;
    }

    /// <summary>
    /// Maps known room roots back to the zone ids used by the gallery flow system.
    /// </summary>
    private static string GetZoneIdFromTransform(Transform targetTransform)
    {
        Transform current = targetTransform;

        while (current != null)
        {
            switch (current.name)
            {
                case "Room_01_Lobby":
                    return "LOBBY";
                case "Room_02_Time_Corridor":
                    return "HALLWAY";
                case "Room_03_Abstract_Color_Field":
                    return "ABSTRACT";
                case "Room_04_Media_Extension":
                    return "MEDIA";
                case "Room_05_Curator_Focus":
                    return "CURATOR";
                case "Room_06_Reflection_Lounge":
                    return "LOUNGE";
            }

            current = current.parent;
        }

        return string.Empty;
    }

    /// <summary>
    /// Returns true when a zone id matches or contains the compared zone token.
    /// </summary>
    private static bool MatchesZone(string sourceZoneId, string comparisonZoneId)
    {
        if (string.IsNullOrWhiteSpace(sourceZoneId) || string.IsNullOrWhiteSpace(comparisonZoneId))
        {
            return false;
        }

        return sourceZoneId.IndexOf(comparisonZoneId, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Returns true when either the shared or live material contains the requested color property.
    /// </summary>
    private static bool MaterialHasProperty(Material sharedMaterial, Material liveMaterial, int propertyId)
    {
        return (sharedMaterial != null && sharedMaterial.HasProperty(propertyId)) ||
               (liveMaterial != null && liveMaterial.HasProperty(propertyId));
    }

    /// <summary>
    /// Reads a renderer's original base color from _BaseColor or _Color with a white fallback.
    /// </summary>
    private static Color ReadBaseColor(Material sharedMaterial, Material liveMaterial)
    {
        if (sharedMaterial != null)
        {
            if (sharedMaterial.HasProperty(BaseColorId))
            {
                return sharedMaterial.GetColor(BaseColorId);
            }

            if (sharedMaterial.HasProperty(ColorId))
            {
                return sharedMaterial.GetColor(ColorId);
            }
        }

        if (liveMaterial != null)
        {
            if (liveMaterial.HasProperty(BaseColorId))
            {
                return liveMaterial.GetColor(BaseColorId);
            }

            if (liveMaterial.HasProperty(ColorId))
            {
                return liveMaterial.GetColor(ColorId);
            }
        }

        return Color.white;
    }

    /// <summary>
    /// Reads a material color property from shared data first and live data second.
    /// </summary>
    private static Color ReadColor(Material sharedMaterial, Material liveMaterial, int propertyId, Color fallbackColor)
    {
        if (sharedMaterial != null && sharedMaterial.HasProperty(propertyId))
        {
            return sharedMaterial.GetColor(propertyId);
        }

        if (liveMaterial != null && liveMaterial.HasProperty(propertyId))
        {
            return liveMaterial.GetColor(propertyId);
        }

        return fallbackColor;
    }

    /// <summary>
    /// Tints an original color toward a mood color while preserving its relative character.
    /// </summary>
    private static Color TintColor(Color originalColor, Color tintColor, float tintStrength)
    {
        return Color.Lerp(originalColor, MultiplyColors(originalColor, tintColor), tintStrength);
    }

    /// <summary>
    /// Multiplies two colors component-wise for lightweight prototype tinting.
    /// </summary>
    private static Color MultiplyColors(Color firstColor, Color secondColor)
    {
        return new Color(
            firstColor.r * secondColor.r,
            firstColor.g * secondColor.g,
            firstColor.b * secondColor.b,
            firstColor.a);
    }

    /// <summary>
    /// Snaps current lighting values to the latest targets for the initial frame.
    /// </summary>
    private void SnapCurrentStateToTarget()
    {
        currentLightMultiplier = targetLightMultiplier;
        currentEmissionMultiplier = targetEmissionMultiplier;
        currentLightTintStrength = targetLightTintStrength;
        currentEmissionTintStrength = targetEmissionTintStrength;
        currentLightTint = targetLightTint;
        currentEmissionTint = targetEmissionTint;
    }
}
