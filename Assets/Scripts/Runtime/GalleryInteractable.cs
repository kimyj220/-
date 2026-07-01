using UnityEngine;
using UnityEngine.Events;

public enum GalleryInteractableType
{
    Artwork,
    Sculpture,
    VideoWall,
    AudioPoint,
    Curator,
    InfoPanel,
    LoungePoint,
    Exit,
    Object
}

public class GalleryInteractable : MonoBehaviour
{
    [TextArea]
    public string title = "Untitled Exhibit";

    [TextArea(3, 8)]
    public string description = "Interaction details are not configured yet.";

    public GalleryInteractableType interactableType = GalleryInteractableType.Artwork;
    public Color highlightColor = new Color(1.0f, 0.78f, 0.28f, 1.0f);
    public float interactionRange = 4.0f;
    public UnityEvent onInteract = new UnityEvent();

    private Renderer[] renderers;
    private Color[] originalColors;
    private bool highlighted;

    private void Awake()
    {
        CacheRenderers();
    }

    private void OnDisable()
    {
        SetHighlighted(false);
    }

    public void SetHighlighted(bool value)
    {
        if (highlighted == value)
        {
            return;
        }

        highlighted = value;
        CacheRenderers();

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            Material material = renderers[i].material;
            if (material == null || !material.HasProperty("_BaseColor"))
            {
                continue;
            }

            Color baseColor = i < originalColors.Length ? originalColors[i] : Color.white;
            material.SetColor("_BaseColor", value ? Color.Lerp(baseColor, highlightColor, 0.45f) : baseColor);
        }
    }

    public void Interact(GameObject interactor)
    {
        Debug.Log($"Gallery interaction: {title} ({interactableType})", this);
        onInteract?.Invoke();
    }

    private void CacheRenderers()
    {
        if (renderers != null && renderers.Length > 0)
        {
            return;
        }

        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            Material material = renderers[i].sharedMaterial;
            originalColors[i] = material != null && material.HasProperty("_BaseColor")
                ? material.GetColor("_BaseColor")
                : Color.white;
        }
    }
}
