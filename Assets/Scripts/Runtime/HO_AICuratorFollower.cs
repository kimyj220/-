using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Keeps the prototype AI curator on the NavMesh while maintaining a readable follow distance from the player.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Rigidbody))]
public class HO_AICuratorFollower : MonoBehaviour
{
    [SerializeField] private Transform followTarget;
    [SerializeField] private float followDistance = 3.0f;
    [SerializeField] private float distanceTolerance = 0.25f;
    [SerializeField] private float repathInterval = 0.15f;
    [SerializeField] private float navMeshSampleRadius = 1.75f;
    [SerializeField] private float moveSpeed = 3.2f;
    [SerializeField] private float acceleration = 8.0f;
    [SerializeField] private float turnSpeed = 540.0f;
    [SerializeField] private float agentRadius = 0.38f;
    [SerializeField] private float agentHeight = 1.8f;
    [SerializeField] private LayerMask blockingLayerMask = ~0;
    [SerializeField] private float collisionSkinWidth = 0.08f;
    [SerializeField] private float maxManualStepDistance = 0.22f;

    private NavMeshAgent navMeshAgent;
    private CapsuleCollider bodyCollider;
    private Rigidbody bodyRigidbody;
    private float nextRepathTime;
    private Vector3 lastSafePosition;
    private bool hasSafePosition;
    private readonly Collider[] overlapBuffer = new Collider[24];

    /// <summary>
    /// Configures the navigation and collision defaults when the component is first added in the editor.
    /// </summary>
    private void Reset()
    {
        CacheComponents();
        ConfigureAgentAndCollisionBody();
    }

    /// <summary>
    /// Caches required components and applies prototype-safe movement settings before play begins.
    /// </summary>
    private void Awake()
    {
        CacheComponents();
        ConfigureAgentAndCollisionBody();
    }

    /// <summary>
    /// Snaps the curator to the nearest valid NavMesh position when the component becomes active.
    /// </summary>
    private void OnEnable()
    {
        if (EnsureAgentOnNavMesh())
        {
            RememberSafePosition(transform.position);
        }
    }

    /// <summary>
    /// Refreshes the player target and updates the curator destination at a throttled interval.
    /// </summary>
    private void Update()
    {
        CacheFollowTarget();

        if (!EnsureAgentOnNavMesh())
        {
            return;
        }

        UpdateFollowDestination();
        MoveAlongPathWithCollisionGuard();
        RotateTowardMovementOrTarget();
        RememberSafePosition(transform.position);
    }

    /// <summary>
    /// Allows the scene builder or another system to explicitly assign the player transform to follow.
    /// </summary>
    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
    }

    /// <summary>
    /// Retrieves the required Unity navigation and physics components from the same GameObject.
    /// </summary>
    private void CacheComponents()
    {
        if (navMeshAgent == null)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }

        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<CapsuleCollider>();
        }

        if (bodyRigidbody == null)
        {
            bodyRigidbody = GetComponent<Rigidbody>();
        }
    }

    /// <summary>
    /// Applies NavMeshAgent and capsule collider values that keep the curator from clipping through gallery geometry.
    /// </summary>
    private void ConfigureAgentAndCollisionBody()
    {
        if (navMeshAgent != null)
        {
            navMeshAgent.speed = moveSpeed;
            navMeshAgent.acceleration = acceleration;
            navMeshAgent.angularSpeed = turnSpeed;
            navMeshAgent.radius = agentRadius;
            navMeshAgent.height = agentHeight;
            navMeshAgent.baseOffset = 0.0f;
            navMeshAgent.stoppingDistance = Mathf.Max(0.35f, followDistance * 0.35f);
            navMeshAgent.autoBraking = true;
            navMeshAgent.updatePosition = true;
            navMeshAgent.updateRotation = false;
            navMeshAgent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            navMeshAgent.avoidancePriority = 45;
        }

        if (bodyCollider != null)
        {
            bodyCollider.isTrigger = true;
            bodyCollider.direction = 1;
            bodyCollider.radius = Mathf.Max(0.18f, agentRadius * 0.75f);
            bodyCollider.height = agentHeight;
            bodyCollider.center = new Vector3(0.0f, agentHeight * 0.5f, 0.0f);
        }

        if (bodyRigidbody != null)
        {
            bodyRigidbody.isKinematic = true;
            bodyRigidbody.useGravity = false;
            bodyRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            bodyRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        }
    }

    /// <summary>
    /// Finds the player object lazily so generated scenes still work when the builder has not assigned a reference.
    /// </summary>
    private void CacheFollowTarget()
    {
        if (followTarget != null)
        {
            return;
        }

        HO_PlayerMovement playerMovement = FindAnyObjectByType<HO_PlayerMovement>();
        if (playerMovement != null)
        {
            followTarget = playerMovement.transform;
            return;
        }

        GameObject playerObject = GameObject.Find("Player");
        if (playerObject != null)
        {
            followTarget = playerObject.transform;
            return;
        }

        playerObject = GameObject.Find("Player_FirstPerson_Gallery");
        if (playerObject != null)
        {
            followTarget = playerObject.transform;
        }
    }

    /// <summary>
    /// Warps the agent onto nearby baked navigation data before destination updates are attempted.
    /// </summary>
    private bool EnsureAgentOnNavMesh()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled)
        {
            return false;
        }

        if (navMeshAgent.isOnNavMesh)
        {
            return true;
        }

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
        {
            bool didWarp = navMeshAgent.Warp(hit.position);
            if (didWarp)
            {
                transform.position = hit.position;
                SyncAgentToTransform();
                RememberSafePosition(hit.position);
            }

            return didWarp;
        }

        return false;
    }

    /// <summary>
    /// Chooses the nearest point on a three-meter ring around the player and sends the agent there.
    /// </summary>
    private void UpdateFollowDestination()
    {
        if (followTarget == null)
        {
            StopAgent();
            return;
        }

        Vector3 flatOffset = transform.position - followTarget.position;
        flatOffset.y = 0.0f;
        float currentDistance = flatOffset.magnitude;
        Vector3 followDirection = ResolveFollowDirection(flatOffset);

        if (Mathf.Abs(currentDistance - followDistance) <= distanceTolerance)
        {
            StopAgent();
            return;
        }

        if (Time.time < nextRepathTime && navMeshAgent.hasPath)
        {
            return;
        }

        Vector3 desiredPosition = followTarget.position + followDirection * followDistance;
        if (!NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
        {
            StopAgent();
            return;
        }

        navMeshAgent.isStopped = false;
        navMeshAgent.SetDestination(hit.position);
        nextRepathTime = Time.time + repathInterval;
    }

    /// <summary>
    /// Manually advances the NavMeshAgent while a capsule sweep blocks movement into walls or solid props.
    /// </summary>
    private void MoveAlongPathWithCollisionGuard()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return;
        }

        if (navMeshAgent.updatePosition)
        {
            return;
        }

        Vector3 desiredDelta = navMeshAgent.desiredVelocity * Time.deltaTime;
        desiredDelta.y = 0.0f;

        if (desiredDelta.sqrMagnitude <= 0.000001f)
        {
            SyncAgentToTransform();
            return;
        }

        float maxStep = Mathf.Max(0.01f, maxManualStepDistance);
        desiredDelta = Vector3.ClampMagnitude(desiredDelta, maxStep);

        Vector3 direction = desiredDelta.normalized;
        float distance = desiredDelta.magnitude;

        if (HasBlockingGeometry(transform.position, direction, distance + collisionSkinWidth))
        {
            RecoverToLastSafePosition();
            StopAgent();
            return;
        }

        Vector3 candidatePosition = transform.position + desiredDelta;
        float sampleRadius = Mathf.Min(navMeshSampleRadius, Mathf.Max(0.25f, agentRadius + collisionSkinWidth));
        if (!NavMesh.SamplePosition(candidatePosition, out NavMeshHit navMeshHit, sampleRadius, NavMesh.AllAreas))
        {
            StopAgent();
            SyncAgentToTransform();
            return;
        }

        Vector3 sampledDelta = navMeshHit.position - transform.position;
        sampledDelta.y = 0.0f;
        if (sampledDelta.sqrMagnitude > 0.000001f &&
            HasBlockingGeometry(transform.position, sampledDelta.normalized, sampledDelta.magnitude + collisionSkinWidth))
        {
            RecoverToLastSafePosition();
            StopAgent();
            return;
        }

        if (IsCapsuleBlockedAt(navMeshHit.position))
        {
            RecoverToLastSafePosition();
            StopAgent();
            return;
        }

        transform.position = navMeshHit.position;
        SyncAgentToTransform();
        RememberSafePosition(transform.position);
    }

    /// <summary>
    /// Casts the curator body forward and reports true when a solid wall or prop would be crossed.
    /// </summary>
    private bool HasBlockingGeometry(Vector3 basePosition, Vector3 direction, float distance)
    {
        GetCapsulePoints(basePosition, out Vector3 bottom, out Vector3 top, out float radius);
        RaycastHit[] hits = Physics.CapsuleCastAll(
            bottom,
            top,
            radius,
            direction,
            distance,
            blockingLayerMask,
            QueryTriggerInteraction.Ignore);

        for (int index = 0; index < hits.Length; index++)
        {
            RaycastHit hit = hits[index];
            if (IsBlockingCollider(hit.collider) && hit.normal.y < 0.65f)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether the capsule would already overlap a blocking collider at the target position.
    /// </summary>
    private bool IsCapsuleBlockedAt(Vector3 basePosition)
    {
        GetCapsulePoints(basePosition, out Vector3 bottom, out Vector3 top, out float radius);
        int hitCount = Physics.OverlapCapsuleNonAlloc(
            bottom,
            top,
            radius,
            overlapBuffer,
            blockingLayerMask,
            QueryTriggerInteraction.Ignore);

        for (int index = 0; index < hitCount; index++)
        {
            if (IsBlockingCollider(overlapBuffer[index]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Calculates the world-space capsule sweep points from the curator's body collider values.
    /// </summary>
    private void GetCapsulePoints(Vector3 basePosition, out Vector3 bottom, out Vector3 top, out float radius)
    {
        Vector3 scale = transform.lossyScale;
        float horizontalScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float verticalScale = Mathf.Abs(scale.y);
        radius = (bodyCollider != null ? bodyCollider.radius : agentRadius) * horizontalScale;
        float height = (bodyCollider != null ? bodyCollider.height : agentHeight) * verticalScale;
        height = Mathf.Max(height, radius * 2.0f);

        float centerY = bodyCollider != null
            ? bodyCollider.center.y * verticalScale
            : height * 0.5f;
        Vector3 center = basePosition + Vector3.up * centerY;
        float halfSegment = Mathf.Max(0.0f, height * 0.5f - radius);
        bottom = center - Vector3.up * halfSegment;
        top = center + Vector3.up * halfSegment;
    }

    /// <summary>
    /// Filters out floors, stairs, triggers, and this curator's own generated colliders from wall checks.
    /// </summary>
    private bool IsBlockingCollider(Collider candidate)
    {
        if (candidate == null || candidate.isTrigger)
        {
            return false;
        }

        Transform candidateTransform = candidate.transform;
        if (candidateTransform == transform || candidateTransform.IsChildOf(transform))
        {
            return false;
        }

        string candidateName = candidate.gameObject.name;
        if (IsWalkableSurfaceName(candidateName))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Identifies generated floor and stair colliders that should support movement instead of blocking it.
    /// </summary>
    private static bool IsWalkableSurfaceName(string objectName)
    {
        return objectName.StartsWith("FLOOR", System.StringComparison.Ordinal) ||
            objectName.StartsWith("STAIR", System.StringComparison.Ordinal) ||
            objectName.StartsWith("DECK_MAIN", System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Stores a position known to be valid so the curator can roll back instead of clipping through a wall.
    /// </summary>
    private void RememberSafePosition(Vector3 position)
    {
        if (IsCapsuleBlockedAt(position))
        {
            return;
        }

        lastSafePosition = position;
        hasSafePosition = true;
    }

    /// <summary>
    /// Moves the curator back to the last valid body position when a wall sweep fails.
    /// </summary>
    private void RecoverToLastSafePosition()
    {
        if (hasSafePosition && !IsCapsuleBlockedAt(lastSafePosition))
        {
            transform.position = lastSafePosition;
        }

        SyncAgentToTransform();
    }

    /// <summary>
    /// Keeps the NavMeshAgent simulation position aligned with the manually moved transform.
    /// </summary>
    private void SyncAgentToTransform()
    {
        if (navMeshAgent != null && navMeshAgent.enabled)
        {
            navMeshAgent.nextPosition = transform.position;
        }
    }

    /// <summary>
    /// Resolves a stable horizontal direction so the curator keeps distance without orbiting unpredictably.
    /// </summary>
    private Vector3 ResolveFollowDirection(Vector3 flatOffset)
    {
        if (flatOffset.sqrMagnitude > 0.0001f)
        {
            return flatOffset.normalized;
        }

        Vector3 fallbackDirection = -followTarget.forward;
        fallbackDirection.y = 0.0f;
        if (fallbackDirection.sqrMagnitude > 0.0001f)
        {
            return fallbackDirection.normalized;
        }

        return -transform.forward;
    }

    /// <summary>
    /// Stops the agent cleanly while preserving its current position on the NavMesh.
    /// </summary>
    private void StopAgent()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return;
        }

        navMeshAgent.isStopped = true;
        if (navMeshAgent.hasPath)
        {
            navMeshAgent.ResetPath();
        }

        SyncAgentToTransform();
    }

    /// <summary>
    /// Faces the curator toward movement while walking, then back toward the player when it is waiting.
    /// </summary>
    private void RotateTowardMovementOrTarget()
    {
        Vector3 facingDirection = Vector3.zero;

        if (navMeshAgent != null && navMeshAgent.desiredVelocity.sqrMagnitude > 0.01f)
        {
            facingDirection = navMeshAgent.desiredVelocity;
        }
        else if (followTarget != null)
        {
            facingDirection = followTarget.position - transform.position;
        }

        facingDirection.y = 0.0f;
        if (facingDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(facingDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }
}
