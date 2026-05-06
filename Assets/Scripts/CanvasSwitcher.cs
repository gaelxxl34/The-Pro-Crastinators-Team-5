using UnityEngine;

/// <summary>
/// Toggles between the writing Canvas (TwoLineDisplay) and the chatbot Canvas.
/// Both canvases share the same on-screen KeyboardFull panel.
///
/// Hook the public methods to any event you like — UI Button OnClick,
/// LeverController, a KeyButton, etc.
/// </summary>
public class CanvasSwitcher : MonoBehaviour
{
    [Tooltip("Canvas with the writing InputField (TwoLineDisplay).")]
    public GameObject writingCanvas;

    [Tooltip("Canvas with the chatbot UI (Chatbot.cs).")]
    public GameObject chatbotCanvas;

    [Tooltip("Which canvas is shown at scene start.")]
    public Mode startMode = Mode.Writing;

    public enum Mode { Writing, Chatbot }

    private void Start()
    {
        Show(startMode);
    }

    public void ShowWriting()  => Show(Mode.Writing);
    public void ShowChatbot()  => Show(Mode.Chatbot);
    public void Toggle()
    {
        bool writingActive = writingCanvas != null && writingCanvas.activeSelf;
        Show(writingActive ? Mode.Chatbot : Mode.Writing);
    }

    private void Show(Mode mode)
    {
        if (writingCanvas != null) writingCanvas.SetActive(mode == Mode.Writing);
        if (chatbotCanvas != null) chatbotCanvas.SetActive(mode == Mode.Chatbot);

        // Auto-focus the input field on the canvas we just showed,
        // so the on-screen keyboard's keystrokes go to the right place.
        var target = (mode == Mode.Writing) ? writingCanvas : chatbotCanvas;
        if (target != null)
        {
            var input = target.GetComponentInChildren<TMPro.TMP_InputField>(includeInactive: false);
            if (input != null) input.ActivateInputField();
        }
    }
}
