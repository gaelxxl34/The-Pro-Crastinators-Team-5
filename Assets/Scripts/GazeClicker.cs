using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class GazeClicker : MonoBehaviour
{
    [Tooltip("The button on the Cardboard / gamepad that fires a click.")]
    public KeyCode triggerKey = KeyCode.Joystick1Button1;

    [Tooltip("Max distance for 3D object gaze detection (in meters).")]
    public float maxDistance = 20f;

    private Button hoveredButton;
    private ClickableObject hoveredClickable;
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

    void Update()
    {
        UpdateHoveredTarget();

        if (Input.GetKeyDown(triggerKey))
        {
            if (hoveredButton != null)
            {
                Debug.Log($"GazeClicker: UI click on {hoveredButton.name}");
                hoveredButton.onClick.Invoke();
            }
            else if (hoveredClickable != null)
            {
                Debug.Log($"GazeClicker: 3D click on {hoveredClickable.name}");
                hoveredClickable.TriggerClick();
            }
        }
    }

    void UpdateHoveredTarget()
    {
        hoveredButton = null;
        hoveredClickable = null;

        if (EventSystem.current != null)
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current);
            pointerData.position = new Vector2(Screen.width / 2f, Screen.height / 2f);

            raycastResults.Clear();
            EventSystem.current.RaycastAll(pointerData, raycastResults);

            foreach (var result in raycastResults)
            {
                Button button = result.gameObject.GetComponent<Button>();
                if (button == null)
                    button = result.gameObject.GetComponentInParent<Button>();

                if (button != null && button.interactable)
                {
                    hoveredButton = button;
                    return; 
                }
            }
        }

        
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            ClickableObject clickable = hit.collider.GetComponent<ClickableObject>();
            if (clickable == null)
                clickable = hit.collider.GetComponentInParent<ClickableObject>();

            if (clickable != null)
            {
                hoveredClickable = clickable;
            }
        }
    }
}