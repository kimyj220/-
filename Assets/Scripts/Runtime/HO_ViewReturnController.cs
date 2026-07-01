using UnityEngine;

/// <summary>
/// Closes the topmost gallery view layer and restores the default observation state with ESC or shared close actions.
/// </summary>
[DisallowMultipleComponent]
public class HO_ViewReturnController : MonoBehaviour
{
    [SerializeField] private HO_ArtworkPopup artworkPopup;
    [SerializeField] private HO_CuratorSubtitle curatorSubtitle;
    [SerializeField] private HO_ArtworkSelector artworkSelector;
    [SerializeField] private GameObject[] closablePanels;
    [SerializeField] private MonoBehaviour[] inputLockTargets;

    /// <summary>
    /// Caches scene references when the component is first added.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
    }

    /// <summary>
    /// Prepares linked view systems before ESC-based restore input starts.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
    }

    /// <summary>
    /// Reads Old Input Escape and closes the most recently prioritized active view.
    /// </summary>
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseTopmostView();
        }
    }

    /// <summary>
    /// Closes the highest-priority open UI layer and restores default viewing control state.
    /// </summary>
    public bool CloseTopmostView()
    {
        CacheReferences();

        if (artworkPopup != null && artworkPopup.IsOpen)
        {
            artworkPopup.HidePopup();
            RestoreViewState();
            return true;
        }

        if (curatorSubtitle != null && curatorSubtitle.IsSubtitleVisible)
        {
            curatorSubtitle.EndSubtitle();
            RestoreViewState();
            return true;
        }

        if (curatorSubtitle != null && curatorSubtitle.IsInteractionHintVisible)
        {
            curatorSubtitle.ClearInteractionHint(null);
            RestoreViewState();
            return true;
        }

        if (closablePanels != null)
        {
            for (int index = closablePanels.Length - 1; index >= 0; index--)
            {
                GameObject closablePanel = closablePanels[index];
                if (closablePanel == null || !closablePanel.activeSelf)
                {
                    continue;
                }

                closablePanel.SetActive(false);
                RestoreViewState();
                return true;
            }
        }

        if (ClearArtworkSelection())
        {
            RestoreViewState();
            return true;
        }

        RestoreViewState();
        return false;
    }

    /// <summary>
    /// Exposes a close-button friendly entry point that shares the same return logic as ESC.
    /// </summary>
    public void HandleCloseButtonPressed()
    {
        CloseTopmostView();
    }

    /// <summary>
    /// Finds scene references so the controller can work even when serialized links are not yet wired.
    /// </summary>
    private void CacheReferences()
    {
        if (artworkPopup == null)
        {
            artworkPopup = FindAnyObjectByType<HO_ArtworkPopup>();
        }

        if (curatorSubtitle == null)
        {
            curatorSubtitle = FindAnyObjectByType<HO_CuratorSubtitle>();
        }

        if (artworkSelector == null)
        {
            artworkSelector = FindAnyObjectByType<HO_ArtworkSelector>();
        }
    }

    /// <summary>
    /// Clears artwork highlight state and re-enables any temporarily disabled input targets.
    /// </summary>
    private void RestoreViewState()
    {
        ClearArtworkSelection();

        if (inputLockTargets == null)
        {
            return;
        }

        for (int index = 0; index < inputLockTargets.Length; index++)
        {
            MonoBehaviour inputTarget = inputLockTargets[index];
            if (inputTarget != null && !inputTarget.enabled)
            {
                inputTarget.enabled = true;
            }
        }
    }

    /// <summary>
    /// Clears the current artwork highlight so the gallery returns to its idle observation state.
    /// </summary>
    private bool ClearArtworkSelection()
    {
        if (artworkSelector == null || artworkSelector.SelectedInteractable == null)
        {
            return false;
        }

        artworkSelector.ClearSelection();
        return true;
    }
}
