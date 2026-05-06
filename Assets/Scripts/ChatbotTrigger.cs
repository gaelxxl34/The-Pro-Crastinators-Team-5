using UnityEngine;

/// <summary>
/// Marker component for an interactable object that opens the chatbot.
/// Put this on the assistant's table (alongside HighlightObject and a Collider).
///
/// When the player highlights it and presses B (or K / Joystick 5),
/// RaycastController will call Activate(), which uses the assigned
/// CanvasSwitcher to swap the writing canvas for the chatbot canvas.
/// </summary>
public class ChatbotTrigger : MonoBehaviour
{
    [Tooltip("CanvasSwitcher in the scene that owns the writing/chatbot canvases.")]
    public CanvasSwitcher canvasSwitcher;

    [Tooltip("If true, pressing B again toggles back to the writing canvas.")]
    public bool toggleMode = true;

    public void Activate()
    {
        if (canvasSwitcher == null)
        {
            canvasSwitcher = FindObjectOfType<CanvasSwitcher>();
            if (canvasSwitcher == null)
            {
                Debug.LogWarning("[ChatbotTrigger] No CanvasSwitcher found in the scene.");
                return;
            }
        }

        if (toggleMode) canvasSwitcher.Toggle();
        else            canvasSwitcher.ShowChatbot();
    }
}
