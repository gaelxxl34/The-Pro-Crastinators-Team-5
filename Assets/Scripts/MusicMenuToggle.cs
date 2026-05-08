using UnityEngine;

public class MusicMenuToggle : MonoBehaviour
{
    [Tooltip("The MusicCanvas GameObject to show/hide.")]
    public GameObject menuCanvas;

    [Tooltip("If true, the menu starts hidden when the scene loads.")]
    public bool startHidden = true;

    void Start()
    {
        if (menuCanvas != null)
            menuCanvas.SetActive(!startHidden);
    }

    public void ToggleMenu()
    {
        if (menuCanvas == null)
        {
            Debug.LogWarning("MusicMenuToggle: menuCanvas not assigned");
            return;
        }
        menuCanvas.SetActive(!menuCanvas.activeSelf);
        Debug.Log($"Music menu is now {(menuCanvas.activeSelf ? "open" : "closed")}");
    }
}