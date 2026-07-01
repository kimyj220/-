using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders a lightweight route HUD that mirrors the ordered gallery path from the zone flow system.
/// </summary>
[DisallowMultipleComponent]
public class HO_MiniRouteUI : MonoBehaviour
{
    [SerializeField] private HO_GalleryZoneFlow galleryZoneFlow;
    [SerializeField] private GameObject[] zoneMarkers;
    [SerializeField] private TMP_Text currentZoneText;
    [SerializeField] private TMP_Text nextZoneText;
    [SerializeField] private TMP_Text routeHintText;
    [SerializeField] private TMP_Text destinationText;
    [SerializeField] private TMP_Text summaryText;

    private readonly Color activeColor = new Color(0.96f, 0.84f, 0.38f, 1.0f);
    private readonly Color nextColor = new Color(0.82f, 0.88f, 0.95f, 0.96f);
    private readonly Color idleColor = new Color(0.35f, 0.38f, 0.44f, 0.88f);
    private HO_GalleryZoneFlow subscribedZoneFlow;
    private string lastCurrentZoneId = string.Empty;
    private string lastNextZoneId = string.Empty;

    /// <summary>
    /// Prepares references before the first route refresh.
    /// </summary>
    private void Awake()
    {
        CacheGalleryZoneFlow();
        CacheOptionalLabels();
        SubscribeToZoneFlow();
        RefreshRouteUI();
    }

    /// <summary>
    /// Restores subscriptions when the route UI is re-enabled.
    /// </summary>
    private void OnEnable()
    {
        CacheGalleryZoneFlow();
        CacheOptionalLabels();
        SubscribeToZoneFlow();
        RefreshRouteUI();
    }

    /// <summary>
    /// Removes route subscriptions while the UI is disabled.
    /// </summary>
    private void OnDisable()
    {
        UnsubscribeFromZoneFlow();
    }

    /// <summary>
    /// Keeps the route UI resilient if the scene spawns the zone flow later than the HUD,
    /// and also refreshes when the zone state changes without a live event subscription.
    /// </summary>
    private void Update()
    {
        if (galleryZoneFlow == null)
        {
            CacheGalleryZoneFlow();
            CacheOptionalLabels();
            SubscribeToZoneFlow();
            RefreshRouteUI();
            return;
        }

        if (!string.Equals(lastCurrentZoneId, galleryZoneFlow.CurrentZoneId, System.StringComparison.Ordinal) ||
            !string.Equals(lastNextZoneId, galleryZoneFlow.NextZoneId, System.StringComparison.Ordinal))
        {
            RefreshRouteUI();
        }
    }

    /// <summary>
    /// Finds the shared gallery zone flow component in the active scene.
    /// </summary>
    private void CacheGalleryZoneFlow()
    {
        if (galleryZoneFlow == null)
        {
            galleryZoneFlow = FindAnyObjectByType<HO_GalleryZoneFlow>();
        }
    }

    /// <summary>
    /// Picks up optional labels by name so the HUD still works if references were not wired in the inspector.
    /// </summary>
    private void CacheOptionalLabels()
    {
        if (destinationText == null)
        {
            destinationText = FindLabel("ZoneLabel_Destination");
        }

        if (summaryText == null)
        {
            summaryText = FindLabel("ZoneLabel_Summary");
        }

        if (routeHintText == null)
        {
            routeHintText = FindLabel("ZoneLabel_RouteHint");
        }
    }

    /// <summary>
    /// Subscribes to zone changes once so the HUD refreshes immediately on trigger entry.
    /// </summary>
    private void SubscribeToZoneFlow()
    {
        if (galleryZoneFlow == null || subscribedZoneFlow == galleryZoneFlow)
        {
            return;
        }

        UnsubscribeFromZoneFlow();
        galleryZoneFlow.ZoneChanged += HandleZoneChanged;
        subscribedZoneFlow = galleryZoneFlow;
    }

    /// <summary>
    /// Detaches the zone-change listener from the previously tracked zone flow.
    /// </summary>
    private void UnsubscribeFromZoneFlow()
    {
        if (subscribedZoneFlow == null)
        {
            return;
        }

        subscribedZoneFlow.ZoneChanged -= HandleZoneChanged;
        subscribedZoneFlow = null;
    }

    /// <summary>
    /// Refreshes the route HUD the moment the player crosses into another zone trigger.
    /// </summary>
    private void HandleZoneChanged(string currentZoneId, string nextZoneId)
    {
        RefreshRouteUI();
    }

    /// <summary>
    /// Updates current, next, destination, and progress text from the shared zone route model.
    /// </summary>
    private void RefreshRouteUI()
    {
        if (galleryZoneFlow == null)
        {
            return;
        }

        int currentIndex = galleryZoneFlow.CurrentZoneIndex;
        int nextIndex = galleryZoneFlow.NextZoneIndex;
        lastCurrentZoneId = galleryZoneFlow.CurrentZoneId;
        lastNextZoneId = galleryZoneFlow.NextZoneId;

        if (currentZoneText != null)
        {
            currentZoneText.text = $"Current: {galleryZoneFlow.CurrentZoneDisplayName}";
        }

        if (nextZoneText != null)
        {
            nextZoneText.text = nextIndex >= 0
                ? $"Next: {galleryZoneFlow.NextZoneDisplayName}"
                : "Next: Route Complete";
        }

        if (destinationText != null)
        {
            destinationText.text = $"Destination: {galleryZoneFlow.FinalDestinationDisplayName}";
        }

        float remainingDistance = galleryZoneFlow.RemainingRouteDistanceMeters;
        int remainingStops = galleryZoneFlow.RemainingZoneCount;
        string routeSummary = remainingStops > 0
            ? $"{remainingStops} stops left | {Mathf.CeilToInt(remainingDistance)}m"
            : "Destination reached";

        if (summaryText != null)
        {
            summaryText.text = routeSummary;
        }

        if (routeHintText != null)
        {
            routeHintText.text = $"{galleryZoneFlow.FinalDestinationDisplayName} | {routeSummary}";
        }

        UpdateMarkerVisuals(currentIndex, nextIndex);
    }

    /// <summary>
    /// Highlights each route marker according to the current and next ordered zone state.
    /// </summary>
    private void UpdateMarkerVisuals(int currentIndex, int nextIndex)
    {
        if (zoneMarkers == null || galleryZoneFlow == null)
        {
            return;
        }

        string[] zoneDisplayNames = galleryZoneFlow.GetZoneDisplayNames();

        for (int index = 0; index < zoneMarkers.Length; index++)
        {
            GameObject marker = zoneMarkers[index];
            if (marker == null)
            {
                continue;
            }

            Image markerImage = marker.GetComponent<Image>();
            TMP_Text markerText = marker.GetComponentInChildren<TMP_Text>();

            bool isCurrent = index == currentIndex;
            bool isNext = index == nextIndex;

            if (markerImage != null)
            {
                markerImage.color = isCurrent ? activeColor : isNext ? nextColor : idleColor;
            }

            if (markerText != null)
            {
                if (index < zoneDisplayNames.Length)
                {
                    markerText.text = zoneDisplayNames[index];
                }

                markerText.color = isCurrent ? Color.black : Color.white;
            }

            marker.transform.localScale = isCurrent
                ? Vector3.one * 1.08f
                : isNext ? Vector3.one : Vector3.one * 0.94f;
        }
    }

    /// <summary>
    /// Searches the route panel hierarchy for an optional TMP text label by name.
    /// </summary>
    private TMP_Text FindLabel(string labelName)
    {
        Transform routePanel = transform.Find("RoutePanel");
        if (routePanel == null)
        {
            return null;
        }

        Transform labelTransform = routePanel.Find(labelName);
        return labelTransform != null ? labelTransform.GetComponent<TMP_Text>() : null;
    }
}
