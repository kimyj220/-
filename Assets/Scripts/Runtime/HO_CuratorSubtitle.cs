using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Displays curator subtitle lines and shared interaction hints for the prototype AI guide flow.
/// </summary>
[DisallowMultipleComponent]
public class HO_CuratorSubtitle : MonoBehaviour
{
    [SerializeField] private GameObject subtitleRoot;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private TMP_Text interactionHintText;
    [SerializeField] private float subtitleDisplayTime = 4.5f;
    [SerializeField] private float interactionHintDistance = 2.0f;

    private string[] activeLines = Array.Empty<string>();
    private int activeLineIndex = -1;
    private float nextAdvanceTime;
    private float inputUnlockTime;
    private object interactionHintOwner;

    /// <summary>
    /// Returns whether the subtitle panel is currently visible.
    /// </summary>
    public bool IsSubtitleVisible => subtitleRoot != null && subtitleRoot.activeSelf;

    /// <summary>
    /// Returns whether a curator conversation is currently active.
    /// </summary>
    public bool HasActiveConversation => IsSubtitleVisible && activeLineIndex >= 0 && activeLineIndex < activeLines.Length;

    /// <summary>
    /// Returns whether the shared interaction hint is currently visible.
    /// </summary>
    public bool IsInteractionHintVisible => interactionHintText != null && interactionHintText.gameObject.activeSelf;

    /// <summary>
    /// Exposes the prototype artwork or curator hint distance so docent systems can stay aligned.
    /// </summary>
    public float InteractionHintDistance => interactionHintDistance;

    /// <summary>
    /// Caches generated subtitle and hint references when the component is first added.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
    }

    /// <summary>
    /// Prepares references and hides all subtitle UI before the gallery starts.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
        HideAllUi();
    }

    /// <summary>
    /// Ensures subtitle progression uses Old Input E-key stepping and timed fallback advance.
    /// </summary>
    private void Update()
    {
        HandleSubtitleProgression();
    }

    /// <summary>
    /// Clears the subtitle and hint state when the component is disabled.
    /// </summary>
    private void OnDisable()
    {
        HideAllUi();
    }

    /// <summary>
    /// Starts a new subtitle sequence from the provided line set.
    /// </summary>
    public void BeginSubtitle(string[] lines)
    {
        string[] sanitizedLines = SanitizeLines(lines);
        if (sanitizedLines.Length == 0)
        {
            EndSubtitle();
            return;
        }

        activeLines = sanitizedLines;
        interactionHintOwner = null;

        if (interactionHintText != null)
        {
            interactionHintText.text = string.Empty;
            interactionHintText.gameObject.SetActive(false);
        }

        ApplyLine(0);
    }

    /// <summary>
    /// Starts a one-line subtitle sequence.
    /// </summary>
    public void BeginSubtitle(string line)
    {
        BeginSubtitle(new[] { line });
    }

    /// <summary>
    /// Advances to the next subtitle line or closes the sequence when the last line has finished.
    /// </summary>
    public void AdvanceSubtitle()
    {
        if (!HasActiveConversation)
        {
            return;
        }

        int nextIndex = activeLineIndex + 1;
        if (nextIndex >= activeLines.Length)
        {
            EndSubtitle();
            return;
        }

        ApplyLine(nextIndex);
    }

    /// <summary>
    /// Ends the active subtitle sequence and hides the panel.
    /// </summary>
    public void EndSubtitle()
    {
        activeLines = Array.Empty<string>();
        activeLineIndex = -1;
        nextAdvanceTime = 0.0f;
        inputUnlockTime = 0.0f;

        if (subtitleText != null)
        {
            subtitleText.text = string.Empty;
        }

        if (subtitleRoot != null)
        {
            subtitleRoot.SetActive(false);
        }
    }

    /// <summary>
    /// Shows a shared interaction hint for the current docent owner when no subtitle is already active.
    /// </summary>
    public void ShowInteractionHint(object owner, string hintMessage)
    {
        if (owner == null || interactionHintText == null || HasActiveConversation || string.IsNullOrWhiteSpace(hintMessage))
        {
            return;
        }

        interactionHintOwner = owner;
        interactionHintText.text = hintMessage;
        interactionHintText.gameObject.SetActive(true);
    }

    /// <summary>
    /// Clears the shared interaction hint when the current owner or a global request asks for it.
    /// </summary>
    public void ClearInteractionHint(object owner)
    {
        if (interactionHintText == null)
        {
            return;
        }

        if (owner != null && interactionHintOwner != owner)
        {
            return;
        }

        interactionHintOwner = null;
        interactionHintText.text = string.Empty;
        interactionHintText.gameObject.SetActive(false);
    }

    /// <summary>
    /// Hides both the subtitle panel and the interaction hint in one reset call.
    /// </summary>
    public void HideAllUi()
    {
        EndSubtitle();
        ClearInteractionHint(null);
    }

    /// <summary>
    /// Finds generated child UI references from the builder hierarchy.
    /// </summary>
    private void CacheReferences()
    {
        if (subtitleRoot == null)
        {
            Transform subtitleTransform = transform.Find("SubtitlePanel");
            if (subtitleTransform != null)
            {
                subtitleRoot = subtitleTransform.gameObject;
            }
        }

        if (subtitleText == null)
        {
            Transform subtitleTextTransform = transform.Find("SubtitlePanel/SubtitleText");
            if (subtitleTextTransform != null)
            {
                subtitleText = subtitleTextTransform.GetComponent<TMP_Text>();
            }
        }

        if (interactionHintText == null)
        {
            Transform hintTransform = transform.Find("InteractionHintText");
            if (hintTransform != null)
            {
                interactionHintText = hintTransform.GetComponent<TMP_Text>();
            }
        }
    }

    /// <summary>
    /// Handles E-key subtitle progression and time-based fallback advance.
    /// </summary>
    private void HandleSubtitleProgression()
    {
        if (!HasActiveConversation)
        {
            return;
        }

        if (Time.unscaledTime >= inputUnlockTime && Input.GetKeyDown(KeyCode.E))
        {
            AdvanceSubtitle();
            return;
        }

        if (subtitleDisplayTime > 0.0f && Time.unscaledTime >= nextAdvanceTime)
        {
            AdvanceSubtitle();
        }
    }

    /// <summary>
    /// Applies one subtitle line to the panel and refreshes its next auto-advance time.
    /// </summary>
    private void ApplyLine(int lineIndex)
    {
        if (lineIndex < 0 || lineIndex >= activeLines.Length)
        {
            EndSubtitle();
            return;
        }

        activeLineIndex = lineIndex;
        inputUnlockTime = Time.unscaledTime + 0.15f;
        nextAdvanceTime = Time.unscaledTime + Mathf.Max(0.75f, subtitleDisplayTime);

        if (subtitleText != null)
        {
            subtitleText.text = activeLines[activeLineIndex];
        }

        if (subtitleRoot != null)
        {
            subtitleRoot.SetActive(true);
        }
    }

    /// <summary>
    /// Removes blank lines so the runtime subtitle flow stays predictable.
    /// </summary>
    private static string[] SanitizeLines(string[] lines)
    {
        if (lines == null || lines.Length == 0)
        {
            return Array.Empty<string>();
        }

        List<string> sanitizedLines = new List<string>(lines.Length);
        for (int index = 0; index < lines.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                continue;
            }

            sanitizedLines.Add(lines[index].Trim());
        }

        return sanitizedLines.ToArray();
    }
}
