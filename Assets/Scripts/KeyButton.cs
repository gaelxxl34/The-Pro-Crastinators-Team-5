using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

[RequireComponent(typeof(Image))]
public class KeyButton : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerExitHandler
{
    public static event Action<string> OnKeyPressed;
    public static event Action<string> OnKeyReleased;

    public string KeyLabel { get; private set; }
    public string SpriteName { get; private set; }

    [Tooltip("Tint applied while the key is held down")]
    public Color pressedTint = new Color(0.65f, 0.65f, 1f, 1f);

    private Image _image;
    private Color _defaultColor;
    private bool _isPressed;

    public void Init(string keyLabel, string spriteName)
    {
        KeyLabel = keyLabel;
        SpriteName = spriteName;
        _image = GetComponent<Image>();
        _defaultColor = _image.color;
        //Debug.Log($"[KeyButton] Initialized '{keyLabel}' ({spriteName}) | raycastTarget: {_image.raycastTarget}");
    }

    public void SetPressed(bool pressed)
    {
        if (_isPressed == pressed) return;
        _isPressed = pressed;

        _image.color = pressed ? pressedTint : _defaultColor;

        if (pressed)
        {
            //Debug.Log($"[KeyButton] PRESSED: {KeyLabel}");
            OnKeyPressed?.Invoke(KeyLabel);
        }
        else
        {
            //Debug.Log($"[KeyButton] RELEASED: {KeyLabel}");
            OnKeyReleased?.Invoke(KeyLabel);
        }
    }

    public void OnPointerDown(PointerEventData _) => SetPressed(true);
    public void OnPointerUp(PointerEventData _) => SetPressed(false);
    public void OnPointerExit(PointerEventData _) => SetPressed(false);
}