using UnityEngine;

public class MannequinDisplayController : MonoBehaviour
{
    [Tooltip("The Display child GameObject to show/hide")]
    public GameObject display;

    private bool _isOpen = false;

    void Start()
    {
        if (display != null)
            display.SetActive(false);
    }

    public void Toggle()
    {
        _isOpen = !_isOpen;
        if (display != null)
            display.SetActive(_isOpen);
    }

    public void Close()
    {
        _isOpen = false;
        if (display != null)
            display.SetActive(false);
    }
}
