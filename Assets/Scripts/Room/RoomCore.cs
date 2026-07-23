using UnityEngine;

/// <summary>
/// Core interactable object in a room. When the player walks within interactRadius,
/// a 3-option choice UI pops up.
/// </summary>
public class RoomCore : MonoBehaviour
{
    [Tooltip("Distance at which the choice UI triggers.")]
    public float interactRadius = 3f;

    [Header("Optional Visual")]
    public GameObject highlightEffect; // optional glow/highlight

    private bool uiShown;
    private Transform playerTransform;

    void Start()
    {
        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    void Update()
    {
        if (uiShown || playerTransform == null) return;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist <= interactRadius)
        {
            ShowChoiceUI();
        }
    }

    void ShowChoiceUI()
    {
        uiShown = true;
        var ui = CoreChoiceUI.Instance;
        if (ui != null)
        {
            ui.Show(this);
        }
        else
        {
            Debug.LogWarning("[RoomCore] CoreChoiceUI.Instance is null — make sure a CoreChoiceUI is in the scene.");
        }
    }

    /// <summary>Called by CoreChoiceUI when the player confirms choices and closes the UI.</summary>
    public void OnChoicesConfirmed()
    {
        uiShown = false;
        // Core stays but interaction is done for this room
        enabled = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}
