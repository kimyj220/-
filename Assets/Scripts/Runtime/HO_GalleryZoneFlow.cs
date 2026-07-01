using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks the ordered gallery route, creates zone triggers, and exposes route state to UI and exhibit systems.
/// </summary>
[DisallowMultipleComponent]
public class HO_GalleryZoneFlow : MonoBehaviour
{
    /// <summary>
    /// Stores one named navigation point used by route markers and remaining-distance estimates.
    /// </summary>
    [Serializable]
    public sealed class RouteWaypoint
    {
        public string name;
        public Vector3 position;

        /// <summary>
        /// Allows Unity serialization to create waypoint entries in the Inspector.
        /// </summary>
        public RouteWaypoint()
        {
        }

        /// <summary>
        /// Initializes a named route waypoint for the generated prototype path.
        /// </summary>
        public RouteWaypoint(string name, Vector3 position)
        {
            this.name = name;
            this.position = position;
        }
    }

    /// <summary>
    /// Defines one ordered gallery zone, its trigger bounds, and its internal route waypoints.
    /// </summary>
    [Serializable]
    public sealed class ZoneDefinition
    {
        public string id;
        public string displayName;
        public Vector3 triggerCenter;
        public Vector3 triggerSize;
        public RouteWaypoint[] routeWaypoints;
    }

    [SerializeField] private ZoneDefinition[] zoneDefinitions = CreateDefaultZoneDefinitions();
    [SerializeField] private Collider[] zoneTriggers;
    [SerializeField] private string[] zoneIds;
    [SerializeField] private string defaultZoneId = "LOBBY";
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform zoneTriggerRoot;

    private int currentZoneIndex = -1;
    private string currentZoneId = string.Empty;
    private string nextZoneId = string.Empty;

    /// <summary>
    /// Fired when the current zone changes so UI, lighting, media, and curator systems can refresh.
    /// </summary>
    public event Action<string, string> ZoneChanged;

    public ZoneDefinition[] ZoneDefinitions => zoneDefinitions;
    public string CurrentZoneId => currentZoneId;
    public string NextZoneId => nextZoneId;
    public int CurrentZoneIndex => currentZoneIndex;
    public int NextZoneIndex => ResolveNextZoneIndex(currentZoneIndex);
    public int ZoneCount => zoneDefinitions != null ? zoneDefinitions.Length : 0;
    public int RemainingZoneCount => currentZoneIndex < 0 ? Mathf.Max(0, ZoneCount - 1) : Mathf.Max(0, ZoneCount - currentZoneIndex - 1);
    public string CurrentZoneDisplayName => GetZoneDisplayName(currentZoneIndex);
    public string NextZoneDisplayName => GetZoneDisplayName(NextZoneIndex);
    public string FinalDestinationDisplayName => ZoneCount > 0 ? GetZoneDisplayName(ZoneCount - 1) : string.Empty;
    public string CurrentRouteHint => GetRouteHint(currentZoneIndex);
    public float RemainingRouteDistanceMeters => CalculateRemainingRouteDistance();

    /// <summary>
    /// Seeds default route metadata when the component is added in the editor.
    /// </summary>
    private void Reset()
    {
        ApplyDefaultZoneDefinitionsIfNeeded();
        EnsureZoneIds();
        EnsureZoneTriggerRoot();
        EnsureZoneTriggers();
    }

    /// <summary>
    /// Prepares route data, trigger volumes, and the initial active zone before play begins.
    /// </summary>
    private void Awake()
    {
        ApplyDefaultZoneDefinitionsIfNeeded();
        EnsureZoneIds();
        CachePlayerTransform();
        EnsureZoneTriggerRoot();
        EnsureZoneTriggers();
        InitializeCurrentZone();
    }

    /// <summary>
    /// Updates the current zone by checking which ordered trigger contains the player's body sample points.
    /// </summary>
    private void Update()
    {
        if (playerTransform == null)
        {
            CachePlayerTransform();
        }

        if (playerTransform == null || zoneTriggers == null || zoneTriggers.Length == 0)
        {
            return;
        }

        int detectedZoneIndex = DetectCurrentZoneIndex(playerTransform.position);
        if (detectedZoneIndex >= 0 && detectedZoneIndex != currentZoneIndex)
        {
            ApplyZoneState(detectedZoneIndex);
        }
    }

    /// <summary>
    /// Creates the canonical ordered zone metadata shared by runtime logic and editor scene building.
    /// </summary>
    public static ZoneDefinition[] CreateDefaultZoneDefinitions()
    {
        return new[]
        {
            new ZoneDefinition
            {
                id = "LOBBY",
                displayName = "Lobby",
                triggerCenter = new Vector3(0.0f, 1.55f, 0.0f),
                triggerSize = new Vector3(10.6f, 3.1f, 7.0f),
                routeWaypoints = new[]
                {
                    new RouteWaypoint("N_START", new Vector3(-2.8f, 0.02f, -3.9f)),
                    new RouteWaypoint("N_LOBBY_CURATOR", new Vector3(-0.6f, 0.02f, 2.1f))
                }
            },
            new ZoneDefinition
            {
                id = "HALLWAY",
                displayName = "Time Corridor",
                triggerCenter = new Vector3(-5.8f, 1.75f, 8.6f),
                triggerSize = new Vector3(15.0f, 3.5f, 5.5f),
                routeWaypoints = new[]
                {
                    new RouteWaypoint("N_CORRIDOR_ENTRY", new Vector3(-4.0f, 0.62f, 4.3f)),
                    new RouteWaypoint("N_CORRIDOR_MID", new Vector3(-8.5f, 0.62f, 8.6f))
                }
            },
            new ZoneDefinition
            {
                id = "ABSTRACT",
                displayName = "Abstract Room",
                triggerCenter = new Vector3(5.0f, 1.75f, 13.8f),
                triggerSize = new Vector3(13.0f, 3.5f, 9.6f),
                routeWaypoints = new[]
                {
                    new RouteWaypoint("N_ABSTRACT_ENTRY", new Vector3(0.0f, 0.62f, 11.7f)),
                    new RouteWaypoint("N_SCULPTURE", new Vector3(5.0f, 0.62f, 11.5f))
                }
            },
            new ZoneDefinition
            {
                id = "MEDIA",
                displayName = "Media Room",
                triggerCenter = new Vector3(17.0f, 1.75f, 13.4f),
                triggerSize = new Vector3(9.5f, 3.5f, 7.6f),
                routeWaypoints = new[]
                {
                    new RouteWaypoint("N_MEDIA_ENTRY", new Vector3(12.6f, 0.62f, 13.6f)),
                    new RouteWaypoint("N_VIDEO_VIEW", new Vector3(17.0f, 0.62f, 11.4f))
                }
            },
            new ZoneDefinition
            {
                id = "CURATOR",
                displayName = "Curator Focus",
                triggerCenter = new Vector3(14.0f, 1.75f, 6.3f),
                triggerSize = new Vector3(8.5f, 3.5f, 5.6f),
                routeWaypoints = new[]
                {
                    new RouteWaypoint("N_CURATOR", new Vector3(14.5f, 0.62f, 7.2f))
                }
            },
            new ZoneDefinition
            {
                id = "LOUNGE",
                displayName = "Reflection Lounge",
                triggerCenter = new Vector3(15.2f, 1.75f, 0.2f),
                triggerSize = new Vector3(8.5f, 3.5f, 6.6f),
                routeWaypoints = new[]
                {
                    new RouteWaypoint("N_LOUNGE_SEAT", new Vector3(15.1f, 0.62f, -0.6f)),
                    new RouteWaypoint("N_EXIT", new Vector3(19.7f, 0.62f, -1.2f))
                }
            }
        };
    }

    /// <summary>
    /// Returns true when the requested zone id matches the player's active zone.
    /// </summary>
    public bool IsCurrentZone(string zoneId)
    {
        return string.Equals(currentZoneId, zoneId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns the display name for one ordered zone index.
    /// </summary>
    public string GetZoneDisplayName(int zoneIndex)
    {
        if (zoneDefinitions == null || zoneIndex < 0 || zoneIndex >= zoneDefinitions.Length)
        {
            return zoneIndex >= ZoneCount ? "Route Complete" : "Moving";
        }

        ZoneDefinition definition = zoneDefinitions[zoneIndex];
        return definition != null && !string.IsNullOrWhiteSpace(definition.displayName)
            ? definition.displayName
            : definition != null ? definition.id : $"ZONE_{zoneIndex + 1:00}";
    }

    /// <summary>
    /// Returns a concise route hint for the active zone UI.
    /// </summary>
    public string GetRouteHint(int zoneIndex)
    {
        if (zoneIndex < 0)
        {
            return "Scanning the route.";
        }

        int nextIndex = ResolveNextZoneIndex(zoneIndex);
        return nextIndex >= 0
            ? $"Move toward {GetZoneDisplayName(nextIndex)}."
            : "You are in the final gallery zone.";
    }

    /// <summary>
    /// Returns the ordered display names used by route marker chips.
    /// </summary>
    public string[] GetZoneDisplayNames()
    {
        if (zoneDefinitions == null || zoneDefinitions.Length == 0)
        {
            return Array.Empty<string>();
        }

        string[] displayNames = new string[zoneDefinitions.Length];
        for (int index = 0; index < zoneDefinitions.Length; index++)
        {
            displayNames[index] = GetZoneDisplayName(index);
        }

        return displayNames;
    }

    /// <summary>
    /// Flattens every zone's route points into one ordered world route with duplicate consecutive points removed.
    /// </summary>
    public static Vector3[] CreateOrderedRouteWaypoints(ZoneDefinition[] definitions = null)
    {
        ZoneDefinition[] source = definitions != null && definitions.Length > 0 ? definitions : CreateDefaultZoneDefinitions();
        List<Vector3> orderedWaypoints = new List<Vector3>();

        for (int zoneIndex = 0; zoneIndex < source.Length; zoneIndex++)
        {
            ZoneDefinition zone = source[zoneIndex];
            if (zone == null || zone.routeWaypoints == null)
            {
                continue;
            }

            for (int waypointIndex = 0; waypointIndex < zone.routeWaypoints.Length; waypointIndex++)
            {
                RouteWaypoint waypoint = zone.routeWaypoints[waypointIndex];
                if (waypoint == null)
                {
                    continue;
                }

                Vector3 point = waypoint.position;
                if (orderedWaypoints.Count == 0 || Vector3.Distance(orderedWaypoints[orderedWaypoints.Count - 1], point) > 0.01f)
                {
                    orderedWaypoints.Add(point);
                }
            }
        }

        return orderedWaypoints.ToArray();
    }

    /// <summary>
    /// Ensures the runtime component always has ordered zone metadata to work from.
    /// </summary>
    private void ApplyDefaultZoneDefinitionsIfNeeded()
    {
        if (zoneDefinitions == null || zoneDefinitions.Length == 0)
        {
            zoneDefinitions = CreateDefaultZoneDefinitions();
        }
    }

    /// <summary>
    /// Mirrors zone ids into a compatibility array used by older scene-builder assignments.
    /// </summary>
    private void EnsureZoneIds()
    {
        if (zoneDefinitions == null)
        {
            zoneIds = Array.Empty<string>();
            return;
        }

        zoneIds = new string[zoneDefinitions.Length];
        for (int index = 0; index < zoneDefinitions.Length; index++)
        {
            zoneIds[index] = zoneDefinitions[index] != null ? zoneDefinitions[index].id : string.Empty;
        }
    }

    /// <summary>
    /// Finds the player transform lazily so generated scenes can recover a missing inspector reference.
    /// </summary>
    private void CachePlayerTransform()
    {
        if (playerTransform != null)
        {
            return;
        }

        HO_PlayerMovement playerMovement = FindAnyObjectByType<HO_PlayerMovement>();
        if (playerMovement != null)
        {
            playerTransform = playerMovement.transform;
            return;
        }

        CharacterController characterController = FindAnyObjectByType<CharacterController>();
        if (characterController != null)
        {
            playerTransform = characterController.transform;
            return;
        }

        GameObject playerObject = GameObject.Find("Player");
        if (playerObject != null)
        {
            playerTransform = playerObject.transform;
            return;
        }

        playerObject = GameObject.Find("Player_FirstPerson_Gallery");
        if (playerObject != null)
        {
            playerTransform = playerObject.transform;
        }
    }

    /// <summary>
    /// Creates or reuses a child root that keeps all runtime-generated zone triggers grouped together.
    /// </summary>
    private void EnsureZoneTriggerRoot()
    {
        if (zoneTriggerRoot != null)
        {
            return;
        }

        Transform existingRoot = transform.Find("ZoneTriggers");
        if (existingRoot != null)
        {
            zoneTriggerRoot = existingRoot;
            return;
        }

        GameObject rootObject = new GameObject("ZoneTriggers");
        rootObject.transform.SetParent(transform, false);
        zoneTriggerRoot = rootObject.transform;
    }

    /// <summary>
    /// Creates or refreshes one trigger collider per ordered zone definition.
    /// </summary>
    private void EnsureZoneTriggers()
    {
        if (zoneDefinitions == null || zoneDefinitions.Length == 0)
        {
            zoneTriggers = Array.Empty<Collider>();
            return;
        }

        if (zoneTriggers == null || zoneTriggers.Length != zoneDefinitions.Length)
        {
            zoneTriggers = new Collider[zoneDefinitions.Length];
        }

        for (int index = 0; index < zoneDefinitions.Length; index++)
        {
            ZoneDefinition definition = zoneDefinitions[index];
            if (definition == null)
            {
                continue;
            }

            BoxCollider boxCollider = zoneTriggers[index] as BoxCollider;
            string triggerName = $"ZoneTrigger_{definition.id}";

            if (boxCollider == null)
            {
                Transform existingTrigger = zoneTriggerRoot != null ? zoneTriggerRoot.Find(triggerName) : null;
                if (existingTrigger == null)
                {
                    GameObject triggerObject = new GameObject(triggerName);
                    triggerObject.transform.SetParent(zoneTriggerRoot != null ? zoneTriggerRoot : transform, false);
                    boxCollider = triggerObject.AddComponent<BoxCollider>();
                }
                else
                {
                    boxCollider = existingTrigger.GetComponent<BoxCollider>();
                    if (boxCollider == null)
                    {
                        boxCollider = existingTrigger.gameObject.AddComponent<BoxCollider>();
                    }
                }
            }

            Transform triggerTransform = boxCollider.transform;
            triggerTransform.SetParent(zoneTriggerRoot != null ? zoneTriggerRoot : transform, false);
            triggerTransform.localPosition = definition.triggerCenter;
            triggerTransform.localRotation = Quaternion.identity;
            boxCollider.isTrigger = true;
            boxCollider.size = definition.triggerSize;
            zoneTriggers[index] = boxCollider;
        }
    }

    /// <summary>
    /// Chooses the starting zone from the player's position and falls back to the configured default zone id.
    /// </summary>
    private void InitializeCurrentZone()
    {
        int initialZoneIndex = playerTransform != null ? DetectCurrentZoneIndex(playerTransform.position) : -1;
        if (initialZoneIndex < 0)
        {
            initialZoneIndex = FindZoneIndexById(defaultZoneId);
        }

        if (initialZoneIndex >= 0)
        {
            ApplyZoneState(initialZoneIndex);
        }
    }

    /// <summary>
    /// Returns the ordered zone index whose trigger contains the player's body sample points.
    /// </summary>
    private int DetectCurrentZoneIndex(Vector3 playerPosition)
    {
        Vector3[] samplePoints =
        {
            playerPosition + Vector3.up * 0.15f,
            playerPosition + Vector3.up * 0.9f,
            playerPosition + Vector3.up * 1.55f
        };

        for (int index = 0; index < zoneTriggers.Length; index++)
        {
            Collider zoneTrigger = zoneTriggers[index];
            if (zoneTrigger == null)
            {
                continue;
            }

            for (int sampleIndex = 0; sampleIndex < samplePoints.Length; sampleIndex++)
            {
                Vector3 samplePoint = samplePoints[sampleIndex];
                if (zoneTrigger.bounds.Contains(samplePoint))
                {
                    return index;
                }

                Vector3 closestPoint = zoneTrigger.ClosestPoint(samplePoint);
                if ((closestPoint - samplePoint).sqrMagnitude <= 0.0004f)
                {
                    return index;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// Finds one ordered zone index from its stable zone id.
    /// </summary>
    private int FindZoneIndexById(string zoneId)
    {
        if (zoneDefinitions == null)
        {
            return -1;
        }

        for (int index = 0; index < zoneDefinitions.Length; index++)
        {
            ZoneDefinition definition = zoneDefinitions[index];
            if (definition != null && string.Equals(definition.id, zoneId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Resolves the next zone index in the fixed gallery route order.
    /// </summary>
    private int ResolveNextZoneIndex(int zoneIndex)
    {
        if (zoneDefinitions == null || zoneDefinitions.Length == 0)
        {
            return -1;
        }

        int nextIndex = zoneIndex + 1;
        return nextIndex >= 0 && nextIndex < zoneDefinitions.Length ? nextIndex : -1;
    }

    /// <summary>
    /// Updates current and next zone ids, then broadcasts the change to subscribed systems.
    /// </summary>
    private void ApplyZoneState(int zoneIndex)
    {
        currentZoneIndex = zoneIndex;
        currentZoneId = zoneDefinitions != null && zoneIndex >= 0 && zoneIndex < zoneDefinitions.Length && zoneDefinitions[zoneIndex] != null
            ? zoneDefinitions[zoneIndex].id
            : string.Empty;

        int nextIndex = ResolveNextZoneIndex(zoneIndex);
        nextZoneId = nextIndex >= 0 && zoneDefinitions[nextIndex] != null ? zoneDefinitions[nextIndex].id : string.Empty;

        ZoneChanged?.Invoke(currentZoneId, nextZoneId);
    }

    /// <summary>
    /// Estimates the remaining route length from the player to the end of the ordered navigation path.
    /// </summary>
    private float CalculateRemainingRouteDistance()
    {
        if (playerTransform == null)
        {
            return 0.0f;
        }

        Vector3[] orderedWaypoints = CreateOrderedRouteWaypoints(zoneDefinitions);
        if (orderedWaypoints.Length == 0)
        {
            return 0.0f;
        }

        int searchStartIndex = Mathf.Clamp(GetRouteStartIndexForZone(currentZoneIndex < 0 ? 0 : currentZoneIndex), 0, orderedWaypoints.Length - 1);
        int nearestRouteIndex = searchStartIndex;
        float bestDistance = float.MaxValue;

        for (int index = searchStartIndex; index < orderedWaypoints.Length; index++)
        {
            float candidateDistance = Vector3.Distance(playerTransform.position, orderedWaypoints[index]);
            if (candidateDistance < bestDistance)
            {
                bestDistance = candidateDistance;
                nearestRouteIndex = index;
            }
        }

        float remainingDistance = Vector3.Distance(playerTransform.position, orderedWaypoints[nearestRouteIndex]);
        for (int index = nearestRouteIndex + 1; index < orderedWaypoints.Length; index++)
        {
            remainingDistance += Vector3.Distance(orderedWaypoints[index - 1], orderedWaypoints[index]);
        }

        return remainingDistance;
    }

    /// <summary>
    /// Converts a zone index to the starting flat route waypoint index for distance calculations.
    /// </summary>
    private int GetRouteStartIndexForZone(int zoneIndex)
    {
        if (zoneDefinitions == null || zoneDefinitions.Length == 0)
        {
            return 0;
        }

        int clampedZoneIndex = Mathf.Clamp(zoneIndex, 0, zoneDefinitions.Length - 1);
        int routeIndex = 0;

        for (int index = 0; index < clampedZoneIndex; index++)
        {
            ZoneDefinition zone = zoneDefinitions[index];
            if (zone != null && zone.routeWaypoints != null)
            {
                routeIndex += zone.routeWaypoints.Length;
            }
        }

        return routeIndex;
    }
}
