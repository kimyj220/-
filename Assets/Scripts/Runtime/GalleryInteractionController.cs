using UnityEngine;
using UnityEngine.InputSystem;

public class GalleryInteractionController : MonoBehaviour
{
    public Camera raycastCamera;
    public float maxDistance = 5.0f;
    public LayerMask interactionMask = ~0;

    private GalleryInteractable currentTarget;
    private GalleryInteractable activePopup;
    private string popupTitle;
    private string popupDescription;

    private GUIStyle boxStyle;
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle promptStyle;
    private Texture2D popupBackground;

    private void Awake()
    {
        if (raycastCamera == null)
        {
            raycastCamera = GetComponentInChildren<Camera>();
        }

        if (raycastCamera == null)
        {
            raycastCamera = Camera.main;
        }
    }

    private void Update()
    {
        UpdateTarget();

        bool interactPressed =
            Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame ||
            Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        if (currentTarget != null && interactPressed)
        {
            currentTarget.Interact(gameObject);
            activePopup = currentTarget;
            popupTitle = currentTarget.title;
            popupDescription = currentTarget.description;
        }

        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
        {
            activePopup = null;
        }
    }

    private void UpdateTarget()
    {
        GalleryInteractable nextTarget = null;

        if (raycastCamera != null)
        {
            Ray ray = raycastCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0.0f));
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactionMask, QueryTriggerInteraction.Collide))
            {
                nextTarget = hit.collider.GetComponentInParent<GalleryInteractable>();
                if (nextTarget != null && hit.distance > nextTarget.interactionRange)
                {
                    nextTarget = null;
                }
            }
        }

        if (currentTarget == nextTarget)
        {
            return;
        }

        if (currentTarget != null)
        {
            currentTarget.SetHighlighted(false);
        }

        currentTarget = nextTarget;

        if (currentTarget != null)
        {
            currentTarget.SetHighlighted(true);
        }
    }

    private void OnGUI()
    {
        EnsureStyles();

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        GUI.Label(new Rect(cx - 5.0f, cy - 12.0f, 10.0f, 24.0f), "+", promptStyle);

        if (currentTarget != null)
        {
            string prompt = $"E / Click  {currentTarget.title}";
            GUI.Label(new Rect(cx - 170.0f, cy + 24.0f, 340.0f, 40.0f), prompt, promptStyle);
        }

        if (activePopup == null)
        {
            return;
        }

        float width = Mathf.Min(520.0f, Screen.width - 48.0f);
        Rect rect = new Rect(Screen.width - width - 24.0f, 24.0f, width, 210.0f);
        GUI.Box(rect, GUIContent.none, boxStyle);
        GUI.Label(new Rect(rect.x + 20.0f, rect.y + 16.0f, rect.width - 40.0f, 32.0f), popupTitle, titleStyle);
        GUI.Label(new Rect(rect.x + 20.0f, rect.y + 58.0f, rect.width - 40.0f, rect.height - 78.0f), popupDescription + "\n\nC: Close", bodyStyle);
    }

    private void EnsureStyles()
    {
        if (boxStyle != null)
        {
            return;
        }

        boxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(18, 18, 18, 18)
        };
        popupBackground = MakeTexture(new Color(0.03f, 0.035f, 0.04f, 0.88f));
        boxStyle.normal.background = popupBackground;
        boxStyle.normal.textColor = Color.white;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            wordWrap = true,
            normal = { textColor = Color.white }
        };

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            wordWrap = true,
            normal = { textColor = new Color(0.92f, 0.92f, 0.92f, 1.0f) }
        };

        promptStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
    }

    private static Texture2D MakeTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}
