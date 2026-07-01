using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays a shared artwork popup with title, meta, keywords, and description text.
/// </summary>
public class HO_ArtworkPopup : MonoBehaviour
{
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text metaText;
    [SerializeField] private TMP_Text keywordText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Button closeButton;

    /// <summary>
    /// Returns whether the popup root is currently visible.
    /// </summary>
    public bool IsOpen => popupRoot != null && popupRoot.activeSelf;

    /// <summary>
    /// Caches default child references when the component is first added.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
    }

    /// <summary>
    /// Ensures references are ready, hides the popup by default, and wires the close button.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
        RegisterCloseButton();
        HidePopup();
    }

    /// <summary>
    /// Removes the runtime close-button subscription when the component is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(HidePopup);
        }
    }

    /// <summary>
    /// Updates every text slot and opens the popup panel.
    /// </summary>
    public void ShowArtwork(string artworkTitle, string artworkMeta, string artworkKeywords, string artworkDescription)
    {
        if (popupRoot == null)
        {
            return;
        }

        if (titleText != null)
        {
            titleText.text = string.IsNullOrWhiteSpace(artworkTitle) ? "Untitled Artwork" : artworkTitle;
        }

        if (metaText != null)
        {
            metaText.text = string.IsNullOrWhiteSpace(artworkMeta) ? "Meta information is not configured yet." : artworkMeta;
        }

        if (keywordText != null)
        {
            keywordText.text = string.IsNullOrWhiteSpace(artworkKeywords) ? "Keywords: pending" : $"Keywords: {artworkKeywords}";
        }

        if (descriptionText != null)
        {
            descriptionText.text = string.IsNullOrWhiteSpace(artworkDescription)
                ? "Artwork description is not configured yet."
                : artworkDescription;
        }

        popupRoot.SetActive(true);
    }

    /// <summary>
    /// Closes the popup panel without changing any cached content.
    /// </summary>
    public void HidePopup()
    {
        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
        }
    }

    /// <summary>
    /// Finds default child objects and components from the generated popup hierarchy.
    /// </summary>
    private void CacheReferences()
    {
        if (popupRoot == null)
        {
            Transform popupTransform = transform.Find("ArtworkPopupPanel");
            if (popupTransform != null)
            {
                popupRoot = popupTransform.gameObject;
            }
        }

        if (titleText == null)
        {
            titleText = FindText("ArtworkTitleText");
        }

        if (metaText == null)
        {
            metaText = FindText("ArtworkMetaText");
        }

        if (keywordText == null)
        {
            keywordText = FindText("ArtworkKeywordText");
        }

        if (descriptionText == null)
        {
            descriptionText = FindText("ArtworkDescriptionText");
        }

        if (closeButton == null)
        {
            Transform closeTransform = transform.Find("ArtworkPopupPanel/CloseButton");
            if (closeTransform != null)
            {
                closeButton = closeTransform.GetComponent<Button>();
            }
        }
    }

    /// <summary>
    /// Connects the close button to the shared close method at runtime.
    /// </summary>
    private void RegisterCloseButton()
    {
        if (closeButton == null)
        {
            return;
        }

        closeButton.onClick.RemoveListener(HidePopup);
        closeButton.onClick.AddListener(HidePopup);
    }

    /// <summary>
    /// Looks up a TMP text child by name inside the popup hierarchy.
    /// </summary>
    private TMP_Text FindText(string childName)
    {
        Transform child = transform.Find($"ArtworkPopupPanel/{childName}");
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }
}
