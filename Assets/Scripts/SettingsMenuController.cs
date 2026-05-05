using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class SettingsMenuController : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public Canvas settingsMenuCanvas;
    public GameObject player;
    public RaycastController raycastController;

    [Header("Inventory Panel")]
    public Canvas inventoryCanvas;
    public Image slot1;
    public Image slot2;
    public Image slot3;

    private bool settingsOpen = false;
    private int settingsIndex = 0;
    private CharacterMovement characterMovement;

    // Settings UI (auto-discovered from canvas)
    private TextMeshProUGUI[] optionTexts;
    private Image[] optionImages;
    private int optionCount = 5;

    // Raycast length toggle
    private float[] raycastLengths = { 5f, 10f, 15f };
    private int raycastLengthIndex = 1;

    // Speed toggle
    private float[] speeds = { 20f, 10f, 5f };
    private string[] speedLabels = { "High", "Medium", "Low" };
    private int speedIndex = 1;

    // Inventory state
    private bool inventoryOpen = false;
    private int inventoryIndex = 0;
    private Image[] slotImages;

    // Thumbnail capture
    private Camera thumbnailCam;
    private RenderTexture thumbnailRT;

    public bool IsOpen { get { return settingsOpen || inventoryOpen; } }
    public bool IsGrabbing { get { return raycastController != null && raycastController.IsCarrying(); } }

    void Start()
    {
        if (player != null)
            characterMovement = player.GetComponent<CharacterMovement>();

        if (settingsMenuCanvas != null)
            settingsMenuCanvas.gameObject.SetActive(false);

        if (inventoryCanvas != null)
            inventoryCanvas.gameObject.SetActive(false);

        slotImages = new Image[] { slot1, slot2, slot3 };

        SetupSettingsButtons();
        CreateThumbnailCamera();
    }

    void SetupSettingsButtons()
    {
        if (settingsMenuCanvas == null) return;

        string[] labels = { "Resume", "Raycast Length: 10m", "Inventory (0)", "Speed: Medium", "Quit" };
        Button[] buttons = settingsMenuCanvas.GetComponentsInChildren<Button>(true);
        optionCount = Mathf.Min(buttons.Length, labels.Length);
        optionTexts = new TextMeshProUGUI[optionCount];
        optionImages = new Image[optionCount];

        for (int i = 0; i < optionCount; i++)
        {
            optionTexts[i] = buttons[i].GetComponentInChildren<TextMeshProUGUI>();
            optionImages[i] = buttons[i].GetComponent<Image>();
            if (optionTexts[i] != null) optionTexts[i].text = labels[i];

            BoxCollider col = buttons[i].GetComponent<BoxCollider>();
            if (col == null) col = buttons[i].gameObject.AddComponent<BoxCollider>();
            RectTransform rt = buttons[i].GetComponent<RectTransform>();
            col.size = new Vector3(rt.rect.width, rt.rect.height, 1f);
            col.center = Vector3.zero;
        }
    }

    // ===== THUMBNAIL CAPTURE =====

    void CreateThumbnailCamera()
    {
        GameObject camObj = new GameObject("ThumbnailCamera");
        camObj.transform.position = new Vector3(500, 500, 500);
        thumbnailCam = camObj.AddComponent<Camera>();
        thumbnailCam.clearFlags = CameraClearFlags.SolidColor;
        thumbnailCam.backgroundColor = new Color(0f, 0.867f, 0.643f, 1f);
        thumbnailCam.orthographic = true;
        thumbnailCam.orthographicSize = 1f;
        thumbnailCam.nearClipPlane = 0.1f;
        thumbnailCam.farClipPlane = 20f;
        thumbnailCam.enabled = false;

        thumbnailRT = new RenderTexture(256, 256, 16);
        thumbnailCam.targetTexture = thumbnailRT;
    }

    Sprite CaptureObjectThumbnail(GameObject obj)
    {
        Vector3 origPos = obj.transform.position;
        Quaternion origRot = obj.transform.rotation;
        bool wasActive = obj.activeSelf;

        obj.SetActive(true);
        obj.transform.position = thumbnailCam.transform.position + thumbnailCam.transform.forward * 5f;
        obj.transform.rotation = Quaternion.Euler(15, -30, 0);

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            obj.transform.position = origPos;
            obj.transform.rotation = origRot;
            obj.SetActive(wasActive);
            return null;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float maxExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        thumbnailCam.orthographicSize = maxExtent * 1.5f;
        thumbnailCam.transform.LookAt(bounds.center);

        thumbnailCam.Render();

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = thumbnailRT;
        Texture2D tex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        obj.transform.position = origPos;
        obj.transform.rotation = origRot;
        obj.SetActive(wasActive);

        return Sprite.Create(tex, new Rect(0, 0, 256, 256), Vector2.one * 0.5f);
    }

    // ===== UPDATE =====

    // D-pad axis tracking for detecting fresh presses from joystick axes
    private bool dpadUpHeld = false;
    private bool dpadDownHeld = false;

    // Helper: detect navigate up (keyboard W/Up, gamepad D-pad up via axis or key)
    bool NavigateUpPressed()
    {
        // Axis-based D-pad detection (vertical axis > 0.5 = up)
        float vert = Input.GetAxisRaw("Vertical");
        bool axisUp = false;
        if (vert > 0.5f)
        {
            if (!dpadUpHeld) { axisUp = true; dpadUpHeld = true; }
        }
        else { dpadUpHeld = false; }

        return Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || axisUp;
    }

    // Helper: detect navigate down (keyboard S/Down, gamepad D-pad down via axis or key)
    bool NavigateDownPressed()
    {
        // Axis-based D-pad detection (vertical axis < -0.5 = down)
        float vert = Input.GetAxisRaw("Vertical");
        bool axisDown = false;
        if (vert < -0.5f)
        {
            if (!dpadDownHeld) { axisDown = true; dpadDownHeld = true; }
        }
        else { dpadDownHeld = false; }

        return Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.X) || axisDown;
    }

    // Helper: detect select/confirm press (keyboard B/Return or gamepad B = K, OK = H)
    bool SelectPressed()
    {
        return Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.L);
    }

    // Called by RaycastController when raycast is disabled
    public void HandleInput()
    {
        // Grabbed object is now handled by RaycastController directly

        // Handle inventory navigation
        if (inventoryOpen)
        {
            HandleInventoryInput();
            return;
        }

        // Handle settings navigation
        if (settingsOpen)
        {
            HandleSettingsInput();
        }
    }

    // Update is not used - HandleInput() is called by RaycastController

    // ===== SETTINGS MENU =====

    public void OpenSettingsMenu()
    {
        if (characterMovement == null && player != null)
            characterMovement = player.GetComponent<CharacterMovement>();

        raycastController.CloseAllMenus();
        raycastController.SetRaycastEnabled(false);

        settingsOpen = true;
        if (characterMovement != null) characterMovement.enabled = false;

        settingsMenuCanvas.transform.position = cam.transform.position + cam.transform.forward * 2f;
        settingsMenuCanvas.transform.rotation = cam.transform.rotation;
        settingsMenuCanvas.gameObject.SetActive(true);

        UpdateOptionText(1, "Raycast Length: " + raycastController.raycastLength + "m");
        UpdateOptionText(2, "Inventory (" + raycastController.GetInventory().Count + ")");
        UpdateOptionText(3, "Speed: " + speedLabels[speedIndex]);

        settingsIndex = 0;
        UpdateSettingsHighlight();
    }

    public void CloseSettingsMenu()
    {
        if (settingsOpen)
        {
            settingsMenuCanvas.gameObject.SetActive(false);
            settingsOpen = false;
        }
        if (inventoryOpen)
        {
            inventoryCanvas.gameObject.SetActive(false);
            inventoryOpen = false;
        }
        if (characterMovement != null) characterMovement.enabled = true;
        raycastController.SetRaycastEnabled(true);
    }

    void HandleSettingsInput()
    {
        if (NavigateUpPressed())
        {
            settingsIndex = (settingsIndex - 1 + optionCount) % optionCount;
            UpdateSettingsHighlight();
        }
        if (NavigateDownPressed())
        {
            settingsIndex = (settingsIndex + 1) % optionCount;
            UpdateSettingsHighlight();
        }
        if (SelectPressed())
        {
            ExecuteSettingsAction(settingsIndex);
        }
    }

    void ExecuteSettingsAction(int index)
    {
        switch (index)
        {
            case 0: // Resume
                CloseSettingsMenu();
                break;
            case 1: // Raycast Length
                raycastLengthIndex = (raycastLengthIndex + 1) % raycastLengths.Length;
                raycastController.raycastLength = raycastLengths[raycastLengthIndex];
                UpdateOptionText(1, "Raycast Length: " + raycastController.raycastLength + "m");
                break;
            case 2: // Inventory
                OpenInventoryPanel();
                break;
            case 3: // Speed
                speedIndex = (speedIndex + 1) % speeds.Length;
                if (characterMovement != null) characterMovement.speed = speeds[speedIndex];
                UpdateOptionText(3, "Speed: " + speedLabels[speedIndex]);
                break;
            case 4: // Quit
                Application.Quit();
                #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
                #endif
                break;
        }
    }

    void UpdateSettingsHighlight()
    {
        if (optionImages == null) return;
        for (int i = 0; i < optionCount; i++)
        {
            if (optionImages[i] != null)
                optionImages[i].color = (i == settingsIndex) ? Color.yellow : Color.white;
        }
    }

    void UpdateOptionText(int index, string text)
    {
        if (optionTexts != null && index < optionTexts.Length && optionTexts[index] != null)
            optionTexts[index].text = text;
    }

    // ===== INVENTORY PANEL =====

    void OpenInventoryPanel()
    {
        List<GameObject> inv = raycastController.GetInventory();
        if (inv.Count == 0) return;

        // Hide settings menu, show inventory
        settingsMenuCanvas.gameObject.SetActive(false);
        settingsOpen = false;

        // Position to the right side of view
        inventoryCanvas.transform.position = cam.transform.position + cam.transform.forward * 2f + cam.transform.right * 0.8f;
        inventoryCanvas.transform.rotation = cam.transform.rotation;

        // Populate slots with thumbnails
        for (int i = 0; i < 3; i++)
        {
            if (slotImages[i] == null) continue;

            if (i < inv.Count)
            {
                Sprite thumb = CaptureObjectThumbnail(inv[i]);
                slotImages[i].sprite = thumb;
                slotImages[i].color = Color.white;
                slotImages[i].preserveAspect = true;
                slotImages[i].gameObject.SetActive(true);
            }
            else
            {
                slotImages[i].sprite = null;
                slotImages[i].color = new Color(0.9f, 0.9f, 0.9f, 0.3f);
                slotImages[i].gameObject.SetActive(true);
            }
        }

        inventoryCanvas.gameObject.SetActive(true);
        inventoryOpen = true;
        inventoryIndex = 0;
        UpdateInventoryHighlight();
    }

    void HandleInventoryInput()
    {
        List<GameObject> inv = raycastController.GetInventory();
        int count = inv.Count;

        if (count == 0)
        {
            inventoryCanvas.gameObject.SetActive(false);
            inventoryOpen = false;
            if (characterMovement != null) characterMovement.enabled = true;
            raycastController.SetRaycastEnabled(true);
            return;
        }

        if (NavigateUpPressed())
        {
            inventoryIndex = (inventoryIndex - 1 + count) % count;
            UpdateInventoryHighlight();
        }

        if (NavigateDownPressed())
        {
            inventoryIndex = (inventoryIndex + 1) % count;
            UpdateInventoryHighlight();
        }

        if (SelectPressed())
        {
            if (inventoryIndex < count)
            {
                GameObject obj = inv[inventoryIndex];
                obj.SetActive(true);

                inventoryCanvas.gameObject.SetActive(false);
                inventoryOpen = false;
                if (characterMovement != null) characterMovement.enabled = true;
                // Hand off to RaycastController for carrying
                raycastController.StartCarrying(obj);
                raycastController.SetRaycastEnabled(true);
            }
        }
    }

    void UpdateInventoryHighlight()
    {
        List<GameObject> inv = raycastController.GetInventory();
        for (int i = 0; i < 3; i++)
        {
            if (slotImages[i] == null) continue;

            if (i < inv.Count)
                slotImages[i].color = (i == inventoryIndex) ? Color.yellow : Color.white;
        }
    }
}
