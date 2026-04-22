using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class RaycastController : MonoBehaviour
{
    [Header("Core")]
    public Camera cam;
    public float raycastLength = 10f;
    // public LineRenderer lineRenderer; // Commented out - using reticle instead of laser line
    public GameObject player;

    [Header("Settings Menu")]
    public SettingsMenuController settingsMenuController;

    [Header("Object Menu")]
    public Canvas objectMenuCanvas;
    public Button destroyButton;
    public Button storeButton;
    public Button exitButton;

    private HighlightObject currentHighlighted;
    private GameObject currentMenuTarget;
    private bool menuOpen = false;
    private string hoveredButton = "";
    private List<GameObject> inventory = new List<GameObject>();
    private const int maxInventory = 3;
    private GameObject lastDestroyedObject;
    private Vector3 lastDestroyedPosition;
    private CharacterMovement characterMovement;
    private bool raycastEnabled = true;
    private GameObject carriedObject = null;
    private int carriedOriginalLayer;
    private bool carriedWasKinematic;

    void Start()
    {
        if (objectMenuCanvas != null) objectMenuCanvas.gameObject.SetActive(false);
        if (player != null) characterMovement = player.GetComponent<CharacterMovement>();

        if (destroyButton != null) AddColliderToButton(destroyButton);
        if (storeButton != null) AddColliderToButton(storeButton);
        if (exitButton != null) AddColliderToButton(exitButton);

        // Force-enable the reticle dot (it's inside VRGroup which may start inactive)
        if (cam != null)
        {
            XRCardboardReticle reticle = cam.GetComponentInChildren<XRCardboardReticle>(true);
            if (reticle != null && reticle.transform.parent != null)
                reticle.transform.parent.gameObject.SetActive(true);
        }
    }

    void AddColliderToButton(Button btn)
    {
        if (btn == null) return;
        BoxCollider col = btn.GetComponent<BoxCollider>();
        if (col == null)
            col = btn.gameObject.AddComponent<BoxCollider>();

        RectTransform rt = btn.GetComponent<RectTransform>();
        if (rt != null)
        {
            col.size = new Vector3(rt.rect.width, rt.rect.height, 1f);
            col.center = Vector3.zero;
        }
    }

    void Update()
    {
        // O key / gamepad Y (U) toggles settings menu (checked before raycastEnabled guard)
        if (Input.GetKeyDown(KeyCode.O) || Input.GetKeyDown(KeyCode.U))
        {
            if (settingsMenuController != null)
            {
                if (settingsMenuController.IsGrabbing)
                    return; // Can't open menu while carrying object

                if (settingsMenuController.IsOpen)
                    settingsMenuController.CloseSettingsMenu();
                else
                    settingsMenuController.OpenSettingsMenu();
            }
            return;
        }

        if (!raycastEnabled)
        {
            // Forward inventory/settings input when raycast is disabled
            if (settingsMenuController != null)
                settingsMenuController.HandleInput();
            return;
        }

        Vector3 origin = cam.transform.position;
        Vector3 direction = cam.transform.forward;
        Ray ray = new Ray(origin, direction);
        RaycastHit hit;
        Vector3 endPoint;

        if (Physics.Raycast(ray, out hit, raycastLength))
        {
            endPoint = hit.point;

            if (carriedObject != null)
            {
                HandleCarry(endPoint);
            }
            else if (menuOpen)
            {
                HandleMenuInteraction(hit);
            }
            else
            {
                HandleHighlight(hit);
                HandleActions(hit);
            }
        }
        else
        {
            endPoint = origin + direction * raycastLength;

            if (carriedObject != null)
            {
                HandleCarry(endPoint);
            }
            else if (!menuOpen)
                ClearHighlight();
            else if (menuOpen)
                ClearMenuHighlight();
        }

        // Commented out - using reticle instead of laser line
        // Vector3 lineStart = origin + cam.transform.up * -0.1f;
        // lineRenderer.SetPosition(0, lineStart);
        // lineRenderer.SetPosition(1, endPoint);
    }

    // ===== PUBLIC METHODS FOR SETTINGS MENU =====

    public void SetRaycastEnabled(bool enabled)
    {
        raycastEnabled = enabled;
        // lineRenderer.enabled = enabled; // Commented out - using reticle instead of laser line
    }

    public void CloseAllMenus()
    {
        if (menuOpen)
            CloseObjectMenu(false);
        ClearHighlight();
    }

    public List<GameObject> GetInventory() { return inventory; }

    public void StartCarrying(GameObject obj)
    {
        carriedObject = obj;
        carriedOriginalLayer = obj.layer;
        ClearHighlight();
        // Move to Ignore Raycast layer so ray doesn't hit it, but colliders still work
        SetLayerRecursive(obj, LayerMask.NameToLayer("Ignore Raycast"));

        // Make kinematic so gravity doesn't pull the object down while carrying
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            carriedWasKinematic = rb.isKinematic;
            rb.isKinematic = true;
        }
    }

    public bool IsCarrying() { return carriedObject != null; }

    void HandleCarry(Vector3 rayEnd)
    {
        // Position object 3m in front of camera, slightly above ground
        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 pos = player.transform.position + forward * 3f;
        pos.y = player.transform.position.y + 0.5f;
        carriedObject.transform.position = pos;
        carriedObject.transform.rotation = Quaternion.identity;

        if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Y))
        {
            GameObject released = carriedObject;
            carriedObject = null;
            inventory.Remove(released);

            // Restore layer
            SetLayerRecursive(released, carriedOriginalLayer);

            // Release at current carry position (no height adjustment)
            released.transform.position = pos;

            // Restore original kinematic state and zero out velocity
            Rigidbody rb = released.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = carriedWasKinematic;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Make sure object is visible
            released.SetActive(true);
            Debug.Log("Released object at: " + pos);
        }
    }

    void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    // ===== ACTIONS =====

    void HandleActions(RaycastHit hit)
    {
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.L))
        {
            if (hit.collider.CompareTag("Floor"))
            {
                Vector3 pos = hit.point;
                pos.y = player.transform.position.y;

                CharacterController cc = player.GetComponent<CharacterController>();
                cc.enabled = false;
                player.transform.position = pos;
                cc.enabled = true;
            }
        }

        if (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.JoystickButton5))
        {
            // Check for lever activation first
            LeverController lever = hit.collider.GetComponent<LeverController>();
            if (lever == null)
                lever = hit.collider.GetComponentInParent<LeverController>();

            // Check for mannequin display toggle
            MannequinDisplayController mannequin = hit.collider.GetComponent<MannequinDisplayController>();
            if (mannequin == null)
                mannequin = hit.collider.GetComponentInParent<MannequinDisplayController>();

            if (lever != null)
            {
                lever.Activate();
            }
            else if (mannequin != null)
            {
                mannequin.Toggle();
            }
            else if (hit.collider.CompareTag("Interactable"))
            {
                OpenObjectMenu(hit.collider.gameObject);
            }
        }

        if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Y))
        {
            if (settingsMenuController != null && settingsMenuController.IsGrabbing) return;

            if (hit.collider.CompareTag("Floor") && lastDestroyedObject != null)
            {
                lastDestroyedObject.transform.position = hit.point + Vector3.up * 0.5f;
                lastDestroyedObject.SetActive(true);
                lastDestroyedObject = null;
            }
        }
    }

    // ===== OBJECT MENU =====

    void OpenObjectMenu(GameObject target)
    {
        if (menuOpen)
            CloseObjectMenu(false);

        currentMenuTarget = target;
        menuOpen = true;

        Vector3 menuPos = target.transform.position + (cam.transform.position - target.transform.position).normalized * 0.5f + Vector3.up * 0.5f;
        objectMenuCanvas.transform.position = menuPos;
        objectMenuCanvas.transform.rotation = cam.transform.rotation;
        objectMenuCanvas.gameObject.SetActive(true);

        characterMovement.enabled = false;
        ResetMenuColors();
    }

    void CloseObjectMenu(bool reEnableMovement)
    {
        objectMenuCanvas.gameObject.SetActive(false);
        menuOpen = false;
        currentMenuTarget = null;
        hoveredButton = "";

        if (reEnableMovement)
            characterMovement.enabled = true;
    }

    void HandleMenuInteraction(RaycastHit hit)
    {
        GameObject hitObj = hit.collider.gameObject;

        Button hitButton = hitObj.GetComponent<Button>();
        if (hitButton == null)
            hitButton = hitObj.GetComponentInParent<Button>();

        if (hitButton == destroyButton)
            SetMenuHighlight("Destroy");
        else if (hitButton == storeButton)
            SetMenuHighlight("Store");
        else if (hitButton == exitButton)
            SetMenuHighlight("Exit");
        else
            ClearMenuHighlight();

        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.L))
        {
            if (hoveredButton == "Destroy" && currentMenuTarget != null)
            {
                lastDestroyedObject = currentMenuTarget;
                lastDestroyedPosition = currentMenuTarget.transform.position;
                currentMenuTarget.SetActive(false);
                CloseObjectMenu(true);
            }
            else if (hoveredButton == "Store" && currentMenuTarget != null)
            {
                if (inventory.Count < maxInventory)
                {
                    currentMenuTarget.SetActive(false);
                    inventory.Add(currentMenuTarget);
                    CloseObjectMenu(true);
                }
                else
                {
                    Debug.Log("Inventory is full!");
                }
            }
            else if (hoveredButton == "Exit")
            {
                CloseObjectMenu(true);
            }
        }
    }

    void SetMenuHighlight(string button)
    {
        hoveredButton = button;
        SetButtonColor(destroyButton, button == "Destroy" ? Color.yellow : Color.white);
        SetButtonColor(storeButton, button == "Store" ? Color.yellow : Color.white);
        SetButtonColor(exitButton, button == "Exit" ? Color.yellow : Color.white);
    }

    void SetButtonColor(Button btn, Color color)
    {
        Image img = btn.GetComponent<Image>();
        if (img != null)
            img.color = color;
    }

    void ClearMenuHighlight()
    {
        hoveredButton = "";
        ResetMenuColors();
    }

    void ResetMenuColors()
    {
        SetButtonColor(destroyButton, Color.white);
        SetButtonColor(storeButton, Color.white);
        SetButtonColor(exitButton, Color.white);
    }

    // ===== HIGHLIGHTING =====

    void HandleHighlight(RaycastHit hit)
    {
        HighlightObject highlightObj = hit.collider.GetComponentInParent<HighlightObject>();

        if (highlightObj != null)
        {
            if (currentHighlighted != highlightObj)
            {
                ClearHighlight();
                currentHighlighted = highlightObj;
                currentHighlighted.Highlight();
            }
        }
        else
        {
            ClearHighlight();
        }
    }

    void ClearHighlight()
    {
        if (currentHighlighted != null)
        {
            currentHighlighted.Unhighlight();
            currentHighlighted = null;
        }
    }
}
