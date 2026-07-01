using UnityEngine;

/// <summary>
/// Gives the prototype AI curator avatar a visible in-world presence and a light idle response.
/// </summary>
[DisallowMultipleComponent]
public class HO_AICuratorAvatar : MonoBehaviour
{
    [SerializeField] private Transform lookTarget;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Renderer[] emissiveRenderers;
    [SerializeField] private Light statusLight;
    [SerializeField] private float lookRadius = 6.0f;
    [SerializeField] private float turnSpeed = 5.0f;
    [SerializeField] private float idleBobHeight = 0.025f;
    [SerializeField] private float idleBobSpeed = 1.2f;
    [SerializeField] private Color idleEmission = new Color(0.02f, 0.28f, 1.0f, 1.0f);
    [SerializeField] private Color engagedEmission = new Color(0.25f, 0.78f, 1.0f, 1.0f);

    private Material[] emissiveMaterials;
    private Vector3 visualStartLocalPosition;

    /// <summary>
    /// Finds default child references when the component is first added in the editor.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
    }

    /// <summary>
    /// Prepares runtime references and material instances before the avatar starts animating.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
        CacheEmissiveMaterials();

        if (visualRoot != null)
        {
            visualStartLocalPosition = visualRoot.localPosition;
        }
    }

    /// <summary>
    /// Updates the curator's lightweight idle motion and player-facing response.
    /// </summary>
    private void LateUpdate()
    {
        CacheLookTarget();
        ApplyIdleMotion();
        ApplyFacing();
        ApplyStatusPulse();
    }

    /// <summary>
    /// Caches optional visual child references from the generated avatar hierarchy.
    /// </summary>
    private void CacheReferences()
    {
        if (visualRoot == null)
        {
            Transform foundVisualRoot = transform.Find($"{name}_VISUAL");
            visualRoot = foundVisualRoot != null ? foundVisualRoot : transform;
        }

        if (statusLight == null)
        {
            statusLight = GetComponentInChildren<Light>(true);
        }

        if (emissiveRenderers == null || emissiveRenderers.Length == 0)
        {
            emissiveRenderers = GetComponentsInChildren<Renderer>(true);
        }
    }

    /// <summary>
    /// Finds the player's camera or movement transform so the curator can acknowledge nearby visitors.
    /// </summary>
    private void CacheLookTarget()
    {
        if (lookTarget != null)
        {
            return;
        }

        if (Camera.main != null)
        {
            lookTarget = Camera.main.transform;
            return;
        }

        HO_PlayerMovement playerMovement = FindFirstObjectByType<HO_PlayerMovement>();
        if (playerMovement != null)
        {
            lookTarget = playerMovement.transform;
        }
    }

    /// <summary>
    /// Stores material instances for emissive parts so the pulse does not edit shared project assets.
    /// </summary>
    private void CacheEmissiveMaterials()
    {
        if (emissiveRenderers == null)
        {
            emissiveMaterials = System.Array.Empty<Material>();
            return;
        }

        emissiveMaterials = new Material[emissiveRenderers.Length];
        for (int index = 0; index < emissiveRenderers.Length; index++)
        {
            Renderer targetRenderer = emissiveRenderers[index];
            emissiveMaterials[index] = targetRenderer != null ? targetRenderer.material : null;
        }
    }

    /// <summary>
    /// Applies a subtle vertical hover so the AI object reads as active without becoming distracting.
    /// </summary>
    private void ApplyIdleMotion()
    {
        if (visualRoot == null)
        {
            return;
        }

        float offset = Mathf.Sin(Time.time * idleBobSpeed) * idleBobHeight;
        visualRoot.localPosition = visualStartLocalPosition + Vector3.up * offset;
    }

    /// <summary>
    /// Rotates the avatar toward the visitor on the horizontal plane while they are in conversation range.
    /// </summary>
    private void ApplyFacing()
    {
        if (lookTarget == null)
        {
            return;
        }

        Vector3 direction = lookTarget.position - transform.position;
        direction.y = 0.0f;

        if (direction.sqrMagnitude > lookRadius * lookRadius || direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Pulses the visor, core, and status light to mark the curator as the active AI guide.
    /// </summary>
    private void ApplyStatusPulse()
    {
        float pulse = 0.5f + Mathf.Sin(Time.time * 2.4f) * 0.5f;
        bool engaged = IsLookTargetInRange();
        Color baseColor = Color.Lerp(idleEmission, engagedEmission, engaged ? 0.85f : 0.2f);
        Color finalEmission = baseColor * Mathf.Lerp(1.2f, 2.6f, pulse);

        if (emissiveMaterials != null)
        {
            for (int index = 0; index < emissiveMaterials.Length; index++)
            {
                Material material = emissiveMaterials[index];
                if (material == null || !material.HasProperty("_EmissionColor"))
                {
                    continue;
                }

                material.SetColor("_EmissionColor", finalEmission);
            }
        }

        if (statusLight != null)
        {
            statusLight.color = baseColor;
            statusLight.intensity = Mathf.Lerp(45.0f, engaged ? 130.0f : 80.0f, pulse);
        }
    }

    /// <summary>
    /// Checks whether the player is near enough for the curator to appear engaged.
    /// </summary>
    private bool IsLookTargetInRange()
    {
        if (lookTarget == null)
        {
            return false;
        }

        Vector3 offset = lookTarget.position - transform.position;
        offset.y = 0.0f;
        return offset.sqrMagnitude <= lookRadius * lookRadius;
    }
}
