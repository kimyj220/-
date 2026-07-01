using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Tracks the current first-person artwork focus and opens the shared artwork popup for explicit selections.
/// </summary>
public class HO_ArtworkSelector : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask artworkLayerMask = 1 << 6;
    [SerializeField] private float maxSelectDistance = 6.0f;
    [SerializeField] private HO_ArtworkPopup artworkPopup;
    [SerializeField] private string selectableTag = string.Empty;
    [SerializeField] private bool useViewportCenterWhenCursorLocked = true;

    private GalleryInteractable selectedInteractable;
    private GalleryInteractable focusedInteractable;

    /// <summary>
    /// Exposes the current highlighted artwork so linked presentation systems can react to selection state.
    /// </summary>
    public GalleryInteractable SelectedInteractable => selectedInteractable;

    /// <summary>
    /// Exposes the artwork currently under the first-person focus ray without requiring a click.
    /// </summary>
    public GalleryInteractable FocusedInteractable => focusedInteractable;

    /// <summary>
    /// Broadcasts the latest selection whenever the highlighted artwork changes.
    /// </summary>
    public event Action<GalleryInteractable> SelectionChanged;

    /// <summary>
    /// Clears the current highlighted artwork so view-return systems can restore the default gallery state.
    /// </summary>
    public void ClearSelection()
    {
        ApplyHighlight(null);
    }

    /// <summary>
    /// Caches default scene references when the component is first added.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
    }

    /// <summary>
    /// Ensures the main camera and popup references are ready before input starts.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
    }

    /// <summary>
    /// Removes highlight state when the selector is disabled.
    /// </summary>
    private void OnDisable()
    {
        ApplyHighlight(null);
    }

    /// <summary>
    /// Refreshes the focused artwork every frame and turns explicit click input into a popup selection.
    /// </summary>
    private void Update()
    {
        CacheReferences();
        UpdateFocusedArtwork();

        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        TrySelectArtwork();
    }

    /// <summary>
    /// Finds default camera and popup references from the current scene.
    /// </summary>
    private void CacheReferences()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (artworkPopup == null)
        {
            artworkPopup = FindAnyObjectByType<HO_ArtworkPopup>();
        }
    }

    /// <summary>
    /// Refreshes the current first-person focus target so other systems can react to E-key prompts.
    /// </summary>
    private void UpdateFocusedArtwork()
    {
        focusedInteractable = TryFindSelectableArtwork();
    }

    /// <summary>
    /// Casts an interaction ray and selects the nearest artwork that matches the layer and type filters.
    /// </summary>
    private void TrySelectArtwork()
    {
        if (targetCamera == null || artworkPopup == null)
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        GalleryInteractable nearestInteractable = TryFindSelectableArtwork();
        if (nearestInteractable == null)
        {
            return;
        }

        ApplySelection(nearestInteractable);
    }

    /// <summary>
    /// Finds the closest selectable artwork from the current interaction ray.
    /// Uses the viewport center while the cursor is locked so first-person E/click behavior matches the old gallery flow.
    /// </summary>
    private GalleryInteractable TryFindSelectableArtwork()
    {
        if (targetCamera == null)
        {
            return null;
        }

        Ray ray = GetInteractionRay();
        RaycastHit[] hits = Physics.RaycastAll(ray, maxSelectDistance, artworkLayerMask, QueryTriggerInteraction.Collide);

        GalleryInteractable nearestInteractable = null;
        float nearestDistance = float.MaxValue;

        for (int index = 0; index < hits.Length; index++)
        {
            GalleryInteractable interactable = hits[index].collider.GetComponentInParent<GalleryInteractable>();
            if (!IsSelectableArtwork(interactable))
            {
                continue;
            }

            if (interactable.interactionRange > 0.0f && hits[index].distance > interactable.interactionRange)
            {
                continue;
            }

            if (hits[index].distance < nearestDistance)
            {
                nearestDistance = hits[index].distance;
                nearestInteractable = interactable;
            }
        }

        return nearestInteractable;
    }

    /// <summary>
    /// Builds the ray source for gallery interactions.
    /// Mouse selection uses the cursor position when free, and the viewport center while the cursor is locked in first-person mode.
    /// </summary>
    private Ray GetInteractionRay()
    {
        if (targetCamera == null)
        {
            return default;
        }

        if (useViewportCenterWhenCursorLocked && Cursor.lockState == CursorLockMode.Locked)
        {
            return targetCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0.0f));
        }

        return targetCamera.ScreenPointToRay(Input.mousePosition);
    }

    /// <summary>
    /// Filters out non-artwork targets and optional tag mismatches.
    /// </summary>
    private bool IsSelectableArtwork(GalleryInteractable interactable)
    {
        if (interactable == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selectableTag) && !interactable.CompareTag(selectableTag))
        {
            return false;
        }

        switch (interactable.interactableType)
        {
            case GalleryInteractableType.Artwork:
            case GalleryInteractableType.Sculpture:
            case GalleryInteractableType.VideoWall:
            case GalleryInteractableType.AudioPoint:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Highlights the selected target, triggers its interaction event, and opens the popup.
    /// </summary>
    private void ApplySelection(GalleryInteractable interactable)
    {
        ApplyHighlight(interactable);
        interactable.Interact(gameObject);

        PopupContent popupContent = BuildPopupContent(interactable);
        artworkPopup.ShowArtwork(
            popupContent.Title,
            popupContent.Meta,
            popupContent.Keywords,
            popupContent.Description);
    }

    /// <summary>
    /// Updates highlight state so only the current selection remains emphasized.
    /// </summary>
    private void ApplyHighlight(GalleryInteractable nextInteractable)
    {
        if (selectedInteractable == nextInteractable)
        {
            return;
        }

        if (selectedInteractable != null)
        {
            selectedInteractable.SetHighlighted(false);
        }

        selectedInteractable = nextInteractable;

        if (selectedInteractable != null)
        {
            selectedInteractable.SetHighlighted(true);
        }

        SelectionChanged?.Invoke(selectedInteractable);
    }

    /// <summary>
    /// Builds simple prototype popup content from the selected interactable and its room context.
    /// </summary>
    private static PopupContent BuildPopupContent(GalleryInteractable interactable)
    {
        string artworkId = ExtractArtworkId(interactable);
        string mediumLabel = GetMediumLabel(interactable.interactableType);
        string roomLabel = GetRoomLabel(interactable.transform);
        string keywordLabel = GetKeywordLabel(interactable);

        return new PopupContent(
            interactable.title,
            $"{artworkId} / {mediumLabel} / {roomLabel}",
            keywordLabel,
            interactable.description);
    }

    /// <summary>
    /// Extracts a readable prototype artwork id from the title or object name.
    /// </summary>
    private static string ExtractArtworkId(GalleryInteractable interactable)
    {
        if (interactable.title.StartsWith("ART-", System.StringComparison.OrdinalIgnoreCase))
        {
            int separatorIndex = interactable.title.IndexOf(' ');
            return separatorIndex > 0 ? interactable.title.Substring(0, separatorIndex) : interactable.title;
        }

        string objectName = interactable.name.Replace('_', '-');
        return objectName.StartsWith("ART-", System.StringComparison.OrdinalIgnoreCase) ? objectName : "ART-PROTOTYPE";
    }

    /// <summary>
    /// Maps each selectable interactable type to a popup medium label.
    /// </summary>
    private static string GetMediumLabel(GalleryInteractableType interactableType)
    {
        switch (interactableType)
        {
            case GalleryInteractableType.Artwork:
                return "Wall Artwork";
            case GalleryInteractableType.Sculpture:
                return "3D Sculpture";
            case GalleryInteractableType.VideoWall:
                return "Video Installation";
            case GalleryInteractableType.AudioPoint:
                return "Audio Installation";
            default:
                return "Prototype Object";
        }
    }

    /// <summary>
    /// Maps the current room root name to a readable popup location label.
    /// </summary>
    private static string GetRoomLabel(Transform artworkTransform)
    {
        Transform current = artworkTransform;
        while (current != null)
        {
            switch (current.name)
            {
                case "Room_02_Time_Corridor":
                    return "Time Corridor";
                case "Room_03_Abstract_Color_Field":
                    return "Abstract Color Field";
                case "Room_04_Media_Extension":
                    return "Media Extension";
                case "Room_05_Curator_Focus":
                    return "Curator Focus";
                case "Room_06_Reflection_Lounge":
                    return "Reflection Lounge";
            }

            current = current.parent;
        }

        return "Gallery Route";
    }

    /// <summary>
    /// Provides short prototype keywords from the selected artwork identity.
    /// </summary>
    private static string GetKeywordLabel(GalleryInteractable interactable)
    {
        string lowerName = interactable.name.ToLowerInvariant();
        if (lowerName.Contains("001"))
        {
            return "surface, structure, tonal field";
        }

        if (lowerName.Contains("002"))
        {
            return "fragmentation, multiple viewpoints, construction";
        }

        if (lowerName.Contains("003"))
        {
            return "color field, scale, atmosphere";
        }

        if (lowerName.Contains("004"))
        {
            return "rotation, silhouette, balance";
        }

        if (lowerName.Contains("005"))
        {
            return "video, light, immersion";
        }

        if (lowerName.Contains("006"))
        {
            return "audio, spatial listening, rhythm";
        }

        if (lowerName.Contains("007"))
        {
            return "memory, layering, reflection";
        }

        return "prototype, gallery, interaction";
    }

    /// <summary>
    /// Simple immutable popup payload used to pass formatted selector output into the popup.
    /// </summary>
    private readonly struct PopupContent
    {
        public PopupContent(string title, string meta, string keywords, string description)
        {
            Title = title;
            Meta = meta;
            Keywords = keywords;
            Description = description;
        }

        public string Title { get; }

        public string Meta { get; }

        public string Keywords { get; }

        public string Description { get; }
    }
}
