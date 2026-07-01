using System;
using UnityEngine;

/// <summary>
/// Selects zone or artwork-specific curator lines and forwards them into the shared subtitle UI.
/// </summary>
[DisallowMultipleComponent]
public class HO_CuratorDocent : MonoBehaviour
{
    /// <summary>
    /// Stores a script key and up to two short docent lines for prototype Inspector authoring.
    /// </summary>
    [Serializable]
    private struct ScriptEntry
    {
        public string key;
        [TextArea(2, 4)] public string primaryLine;
        [TextArea(2, 4)] public string secondaryLine;
    }

    [SerializeField] private HO_GalleryZoneFlow galleryZoneFlow;
    [SerializeField] private HO_CuratorSubtitle curatorSubtitle;
    [SerializeField] private string defaultDocentLine = "이 공간의 핵심 흐름을 짧게 정리해 드릴게요.";
    [SerializeField] private ScriptEntry[] zoneScriptEntries;
    [SerializeField] private ScriptEntry[] artworkScriptEntries;
    [SerializeField] private HO_ArtworkSelector artworkSelector;
    [SerializeField] private GalleryInteractable curatorInteractable;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private float curatorInteractionDistance = 2.4f;
    [SerializeField] private string curatorHintMessage = "E - AI 큐레이터와 대화";
    [SerializeField] private string artworkHintMessage = "E - 선택 작품 해설 듣기";

    /// <summary>
    /// Returns whether the player is close enough to trigger the curator directly.
    /// </summary>
    public bool IsCuratorInRange => playerTransform != null && IsTransformWithinDistance(transform, curatorInteractionDistance);

    /// <summary>
    /// Caches scene references and default script data when the component is first added.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
        EnsureDefaultScripts();
    }

    /// <summary>
    /// Prepares runtime references and default docent text before interaction checks begin.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
        EnsureDefaultScripts();
    }

    /// <summary>
    /// Hooks the curator interactable event when the component becomes active.
    /// </summary>
    private void OnEnable()
    {
        RegisterCuratorInteraction();
    }

    /// <summary>
    /// Unhooks the curator interactable event and clears owned hints when disabled.
    /// </summary>
    private void OnDisable()
    {
        UnregisterCuratorInteraction();

        if (curatorSubtitle != null)
        {
            curatorSubtitle.ClearInteractionHint(this);
        }
    }

    /// <summary>
    /// Evaluates artwork and curator hint contexts with Old Input E-key start triggers.
    /// </summary>
    private void Update()
    {
        CacheReferences();
        UpdateInteractionPrompt();
    }

    /// <summary>
    /// Starts a curator line sequence for the current context, preferring the selected nearby artwork when available.
    /// </summary>
    public void StartDocentForCurrentContext()
    {
        GalleryInteractable selectedArtwork = GetSelectedArtworkInRange();
        if (selectedArtwork != null)
        {
            StartDocentForInteractable(selectedArtwork);
            return;
        }

        StartDocentForZone(ResolveCurrentZoneId());
    }

    /// <summary>
    /// Starts an artwork-specific docent line sequence for the provided interactable.
    /// </summary>
    public void StartDocentForInteractable(GalleryInteractable interactable)
    {
        if (curatorSubtitle == null)
        {
            return;
        }

        string artworkKey = ExtractArtworkKey(interactable);
        string[] scriptLines = ResolveLinesFromEntries(artworkScriptEntries, artworkKey);
        if (scriptLines.Length == 0)
        {
            scriptLines = ResolveLinesFromEntries(zoneScriptEntries, ResolveCurrentZoneId());
        }

        if (scriptLines.Length == 0)
        {
            scriptLines = BuildFallbackLines(defaultDocentLine);
        }

        curatorSubtitle.BeginSubtitle(scriptLines);
        curatorSubtitle.ClearInteractionHint(this);
    }

    /// <summary>
    /// Starts a zone-specific docent line sequence from the current gallery flow state.
    /// </summary>
    public void StartDocentForZone(string zoneId)
    {
        if (curatorSubtitle == null)
        {
            return;
        }

        string[] scriptLines = ResolveLinesFromEntries(zoneScriptEntries, zoneId);
        if (scriptLines.Length == 0)
        {
            scriptLines = BuildFallbackLines(defaultDocentLine);
        }

        curatorSubtitle.BeginSubtitle(scriptLines);
        curatorSubtitle.ClearInteractionHint(this);
    }

    /// <summary>
    /// Finds linked scene systems and the local curator interactable without creating extra AI objects.
    /// </summary>
    private void CacheReferences()
    {
        if (galleryZoneFlow == null)
        {
            galleryZoneFlow = FindAnyObjectByType<HO_GalleryZoneFlow>();
        }

        if (curatorSubtitle == null)
        {
            curatorSubtitle = FindAnyObjectByType<HO_CuratorSubtitle>();
        }

        if (artworkSelector == null)
        {
            artworkSelector = FindAnyObjectByType<HO_ArtworkSelector>();
        }

        if (curatorInteractable == null)
        {
            curatorInteractable = GetComponent<GalleryInteractable>();
        }

        if (playerTransform == null)
        {
            HO_PlayerMovement playerMovement = FindAnyObjectByType<HO_PlayerMovement>();
            if (playerMovement != null)
            {
                playerTransform = playerMovement.transform;
            }
        }
    }

    /// <summary>
    /// Creates default prototype docent lines from the current design document guidance when Inspector data is empty.
    /// </summary>
    private void EnsureDefaultScripts()
    {
        if (zoneScriptEntries == null || zoneScriptEntries.Length == 0)
        {
            zoneScriptEntries = new[]
            {
                new ScriptEntry
                {
                    key = "LOBBY",
                    primaryLine = "이 전시는 한 방향으로만 흘러가지 않습니다. 먼저 바닥의 푸른 유도선을 따라 이동해 보세요.",
                    secondaryLine = "공간을 직접 걷고 멈추며, 근현대 미술이 어떻게 장면을 바꾸는지 몸으로 익히는 방식으로 설계되어 있습니다."
                },
                new ScriptEntry
                {
                    key = "HALLWAY",
                    primaryLine = "작품을 클릭하면 시대적 맥락과 조형 요소를 확대해서 볼 수 있습니다.",
                    secondaryLine = "이 복도는 근대 회화에서 현대 감각으로 넘어가는 리듬을 차례대로 읽는 공간입니다."
                },
                new ScriptEntry
                {
                    key = "ABSTRACT",
                    primaryLine = "여기서는 무엇을 그렸는지보다, 색과 면이 어떤 감각을 만드는지에 집중해 주세요.",
                    secondaryLine = "대형 색면과 중앙 조형물이 방의 공기를 먼저 바꾸고, 그 다음에 이미지가 따라옵니다."
                },
                new ScriptEntry
                {
                    key = "MEDIA",
                    primaryLine = "현대 미술은 화면을 넘어 소리, 빛, 움직임, 관람자의 참여로 확장됩니다.",
                    secondaryLine = "이 방은 비디오 월과 반응형 빛을 통해 매체 확장이 공간 전체를 어떻게 바꾸는지 보여줍니다."
                },
                new ScriptEntry
                {
                    key = "CURATOR",
                    primaryLine = "이 구역은 작품 해설과 팝업 정보를 한 번에 읽어 보는 포커스 존입니다.",
                    secondaryLine = "작품, 정보 패널, 큐레이터 설명이 삼각 구도로 맞물리도록 구성돼 있습니다."
                },
                new ScriptEntry
                {
                    key = "LOUNGE",
                    primaryLine = "마지막 공간에서는 방금 지나온 장면들을 다시 조합해 볼 수 있습니다.",
                    secondaryLine = "이제 관람 흐름을 정리하고, 어떤 매체 변화가 가장 또렷했는지 스스로 되짚어 보세요."
                }
            };
        }

        if (artworkScriptEntries == null || artworkScriptEntries.Length == 0)
        {
            artworkScriptEntries = new[]
            {
                new ScriptEntry
                {
                    key = "ART-001",
                    primaryLine = "ART-001은 초기 모더니즘의 구조감과 도시적 리듬을 회색과 청색의 단면으로 정리한 작품입니다.",
                    secondaryLine = "표면은 차분하지만 화면의 분할 방식은 이미 이후 공간 감각을 예고합니다."
                },
                new ScriptEntry
                {
                    key = "ART-002",
                    primaryLine = "ART-002는 형태를 분해해 하나의 시점을 여러 조각으로 다시 조합하는 실험에 가깝습니다.",
                    secondaryLine = "대상을 그대로 복제하기보다 인식의 구조를 다시 설계하는 단계라고 보시면 됩니다."
                },
                new ScriptEntry
                {
                    key = "ART-003",
                    primaryLine = "ART-003은 색면 추상의 몰입감을 방 전체 스케일로 확장한 장면입니다.",
                    secondaryLine = "이 작품에서는 무엇을 읽느냐보다, 얼마나 오래 색의 압력 속에 머무르느냐가 더 중요합니다."
                },
                new ScriptEntry
                {
                    key = "ART-004",
                    primaryLine = "ART-004는 회전하며 실루엣과 균형이 계속 달라지는 부유형 조형물입니다.",
                    secondaryLine = "평면에서 벗어난 순간, 감상은 앞을 보는 행위가 아니라 주변을 도는 행위로 바뀝니다."
                },
                new ScriptEntry
                {
                    key = "ART-005",
                    primaryLine = "ART-005는 추상 영상과 빛의 반응을 결합해 이미지가 공간 전체로 번지는 방식을 보여줍니다.",
                    secondaryLine = "이 방의 핵심은 화면 안의 내용만이 아니라, 재생 상태에 따라 주변 조명까지 달라지는 환경성입니다."
                },
                new ScriptEntry
                {
                    key = "ART-006",
                    primaryLine = "ART-006은 파동 아카이브처럼 배치된 오디오 포인트로, 가까이 갈수록 장면의 청각적 경계를 드러냅니다.",
                    secondaryLine = "보이지 않는 소리의 방향이 새로운 전시 벽처럼 작동하는 셈입니다."
                },
                new ScriptEntry
                {
                    key = "ART-007",
                    primaryLine = "ART-007은 기억의 층위를 투명 설치물과 해설 팝업의 조합으로 다루는 포커스 작품입니다.",
                    secondaryLine = "이 구역에서는 작품 자체와 설명 방식이 함께 하나의 교육형 연출이 됩니다."
                },
                new ScriptEntry
                {
                    key = "ART-008",
                    primaryLine = "ART-008은 여섯 장면을 다시 묶어 관람의 흐름을 되돌아보게 하는 요약 월입니다.",
                    secondaryLine = "작품 하나의 정보보다, 전시 전체의 순서와 변화를 다시 읽게 만드는 장치라고 볼 수 있습니다."
                }
            };
        }
    }

    /// <summary>
    /// Hooks the local curator interactable so clicking the AI guide can open the same docent flow as the E-key path.
    /// </summary>
    private void RegisterCuratorInteraction()
    {
        if (curatorInteractable == null)
        {
            return;
        }

        curatorInteractable.onInteract.RemoveListener(HandleCuratorInteract);
        curatorInteractable.onInteract.AddListener(HandleCuratorInteract);
    }

    /// <summary>
    /// Unhooks the local curator interactable listener during teardown.
    /// </summary>
    private void UnregisterCuratorInteraction()
    {
        if (curatorInteractable == null)
        {
            return;
        }

        curatorInteractable.onInteract.RemoveListener(HandleCuratorInteract);
    }

    /// <summary>
    /// Shows the best hint for the current context and starts the matching docent flow when E is pressed.
    /// </summary>
    private void UpdateInteractionPrompt()
    {
        if (curatorSubtitle == null || playerTransform == null)
        {
            return;
        }

        if (curatorSubtitle.HasActiveConversation)
        {
            curatorSubtitle.ClearInteractionHint(this);
            return;
        }

        GalleryInteractable selectedArtwork = GetSelectedArtworkInRange();
        if (selectedArtwork != null)
        {
            curatorSubtitle.ShowInteractionHint(this, artworkHintMessage);
            if (Input.GetKeyDown(KeyCode.E))
            {
                StartDocentForInteractable(selectedArtwork);
            }

            return;
        }

        if (IsCuratorInRange)
        {
            curatorSubtitle.ShowInteractionHint(this, curatorHintMessage);
            if (Input.GetKeyDown(KeyCode.E))
            {
                StartDocentForCurrentContext();
            }

            return;
        }

        curatorSubtitle.ClearInteractionHint(this);
    }

    /// <summary>
    /// Starts the current docent context when the local curator object itself is clicked.
    /// </summary>
    private void HandleCuratorInteract()
    {
        if (curatorSubtitle != null && curatorSubtitle.HasActiveConversation)
        {
            return;
        }

        StartDocentForCurrentContext();
    }

    /// <summary>
    /// Returns the currently selected artwork, or the currently focused artwork, when the player is near enough to justify a docent hint.
    /// </summary>
    private GalleryInteractable GetSelectedArtworkInRange()
    {
        if (artworkSelector == null || playerTransform == null || curatorSubtitle == null)
        {
            return null;
        }

        GalleryInteractable selectedInteractable = artworkSelector.SelectedInteractable;
        if (IsDocentArtworkType(selectedInteractable) &&
            IsTransformWithinDistance(selectedInteractable.transform, curatorSubtitle.InteractionHintDistance))
        {
            return selectedInteractable;
        }

        GalleryInteractable focusedInteractable = artworkSelector.FocusedInteractable;
        if (!IsDocentArtworkType(focusedInteractable))
        {
            return null;
        }

        return IsTransformWithinDistance(focusedInteractable.transform, curatorSubtitle.InteractionHintDistance)
            ? focusedInteractable
            : null;
    }

    /// <summary>
    /// Resolves the current gallery zone from the shared zone flow with a curator-local fallback.
    /// </summary>
    private string ResolveCurrentZoneId()
    {
        if (galleryZoneFlow != null && !string.IsNullOrWhiteSpace(galleryZoneFlow.CurrentZoneId))
        {
            return galleryZoneFlow.CurrentZoneId;
        }

        return GetZoneIdFromTransform(transform);
    }

    /// <summary>
    /// Returns the first matching script lines for a zone or artwork key.
    /// </summary>
    private string[] ResolveLinesFromEntries(ScriptEntry[] entries, string key)
    {
        if (entries == null || entries.Length == 0 || string.IsNullOrWhiteSpace(key))
        {
            return Array.Empty<string>();
        }

        for (int index = 0; index < entries.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(entries[index].key))
            {
                continue;
            }

            if (!string.Equals(entries[index].key.Trim(), key.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return BuildLines(entries[index]);
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// Builds the final line array from one Inspector-authored script entry.
    /// </summary>
    private string[] BuildLines(ScriptEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.primaryLine) && !string.IsNullOrWhiteSpace(entry.secondaryLine))
        {
            return new[] { entry.primaryLine.Trim(), entry.secondaryLine.Trim() };
        }

        if (!string.IsNullOrWhiteSpace(entry.primaryLine))
        {
            return new[] { entry.primaryLine.Trim() };
        }

        if (!string.IsNullOrWhiteSpace(entry.secondaryLine))
        {
            return new[] { entry.secondaryLine.Trim() };
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// Builds a one-line fallback array when no matching zone or artwork script exists yet.
    /// </summary>
    private string[] BuildFallbackLines(string fallbackLine)
    {
        return string.IsNullOrWhiteSpace(fallbackLine)
            ? new[] { "이 장면은 다음 업데이트에서 더 자세한 해설이 들어갈 예정입니다." }
            : new[] { fallbackLine.Trim() };
    }

    /// <summary>
    /// Extracts a stable artwork key such as ART-003 from the selected interactable.
    /// </summary>
    private string ExtractArtworkKey(GalleryInteractable interactable)
    {
        if (interactable == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(interactable.title) &&
            interactable.title.StartsWith("ART-", StringComparison.OrdinalIgnoreCase))
        {
            int separatorIndex = interactable.title.IndexOf(' ');
            return separatorIndex > 0
                ? interactable.title.Substring(0, separatorIndex).Trim()
                : interactable.title.Trim();
        }

        string objectName = interactable.name.Replace('_', '-');
        if (objectName.StartsWith("ART-", StringComparison.OrdinalIgnoreCase))
        {
            string[] objectParts = objectName.Split('-');
            if (objectParts.Length >= 2)
            {
                return $"{objectParts[0]}-{objectParts[1]}".ToUpperInvariant();
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Returns true when the selected interactable is a supported docent artwork type.
    /// </summary>
    private static bool IsDocentArtworkType(GalleryInteractable interactable)
    {
        if (interactable == null)
        {
            return false;
        }

        switch (interactable.interactableType)
        {
            case GalleryInteractableType.Artwork:
            case GalleryInteractableType.Sculpture:
            case GalleryInteractableType.VideoWall:
            case GalleryInteractableType.AudioPoint:
            case GalleryInteractableType.LoungePoint:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Checks horizontal distance from the player to a target transform.
    /// </summary>
    private bool IsTransformWithinDistance(Transform target, float distance)
    {
        if (target == null || playerTransform == null)
        {
            return false;
        }

        Vector3 offset = target.position - playerTransform.position;
        offset.y = 0.0f;
        return offset.sqrMagnitude <= distance * distance;
    }

    /// <summary>
    /// Maps the builder room roots back to the zone ids used by the gallery flow system.
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
}
