using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TeleportMenuController : MonoBehaviour
{
    public static string ChosenScene = "";

    [Tooltip("Drag the Canvas GameObject of the teleport menu here")]
    public GameObject menuCanvas;

    [Tooltip("Drag the player GameObject here")]
    public GameObject player;

    [Tooltip("Drag the Camera here")]
    public Camera cam;

    [Header("Buttons")]
    public Button beachButton;
    public Button forestButton;
    public Button officeButton;
    public Button cancelButton;

    [Header("Teleport Zone")]
    public GameObject teleportObject;

    private static readonly string SCENE_BEACH  = "ProCrastinator Beach";
    private static readonly string SCENE_FOREST = "ProCrastinator Forest";
    private static readonly string SCENE_OFFICE = "ProCrastinator Main Office";

    private static readonly Color COLOR_CURRENT  = new Color(0.8f, 0.1f, 0.1f, 1f);
    private static readonly Color COLOR_SELECTED = Color.yellow;
    private static readonly Color COLOR_NORMAL   = Color.white;

    private Button[] _navButtons;
    private int _selectedIndex = 0;
    private bool _isOpen = false;
    private CharacterMovement _characterMovement;
    private XRCardboardReticle _reticle;
    private string _guiMessage = "";
    private float _guiHideTime = 0f;

    // Axis-based D-pad tracking to detect fresh tilt presses
    private bool _dpadUpHeld   = false;
    private bool _dpadDownHeld = false;

    void Start()
    {
        if (menuCanvas != null) menuCanvas.SetActive(false);
        if (teleportObject != null) teleportObject.SetActive(false);
        if (player != null)
        {
            _characterMovement = player.GetComponent<CharacterMovement>();
            if (_characterMovement != null) _characterMovement.enabled = true;
        }
        if (cam != null)
        {
            _reticle = cam.GetComponentInChildren<XRCardboardReticle>(true);
            if (_reticle != null) _reticle.transform.parent.gameObject.SetActive(true);
        }
        MarkCurrentScene(SceneManager.GetActiveScene().name);
    }

    void OnGUI()
    {
        if (string.IsNullOrEmpty(_guiMessage) || Time.time > _guiHideTime) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 52;
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.MiddleCenter;
        style.wordWrap = true;

        float w = Screen.width * 0.7f;
        float h = 160f;
        float x = (Screen.width - w) / 2f;
        float y = (Screen.height - h) / 2f;
        GUI.Label(new Rect(x, y, w, h), _guiMessage, style);
    }

    void Update()
    {
        if (!_isOpen || _navButtons == null || _navButtons.Length == 0) return;

        // Joystick left-stick / D-pad vertical — navigate up
        float vert = Input.GetAxisRaw("Vertical");
        bool axisUp = false;
        if (vert > 0.5f)  { if (!_dpadUpHeld)   { axisUp   = true; _dpadUpHeld   = true; } }
        else              { _dpadUpHeld   = false; }

        bool axisDown = false;
        if (vert < -0.5f) { if (!_dpadDownHeld) { axisDown = true; _dpadDownHeld = true; } }
        else              { _dpadDownHeld = false; }

        if (Input.GetKeyDown(KeyCode.UpArrow)   || Input.GetKeyDown(KeyCode.W) || axisUp)
            SetSelection((_selectedIndex - 1 + _navButtons.Length) % _navButtons.Length);
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) || axisDown)
            SetSelection((_selectedIndex + 1) % _navButtons.Length);
        else if (Input.GetKeyDown(KeyCode.B)
              || Input.GetKeyDown(KeyCode.K)
              || Input.GetKeyDown(KeyCode.JoystickButton5)
              || Input.GetKeyDown(KeyCode.Return))
            ConfirmSelection();
    }

    public void OpenMenu()
    {
        if (menuCanvas == null) { Debug.LogError("[TeleportMenu] menuCanvas not assigned!"); return; }

        _isOpen = true;
        SetButtonsVisible(true);
        menuCanvas.SetActive(true);

        string current = SceneManager.GetActiveScene().name;
        var nav = new System.Collections.Generic.List<Button>();
        if (current != SCENE_BEACH  && beachButton  != null) nav.Add(beachButton);
        if (current != SCENE_FOREST && forestButton != null) nav.Add(forestButton);
        if (current != SCENE_OFFICE && officeButton != null) nav.Add(officeButton);
        if (cancelButton != null) nav.Add(cancelButton);
        _navButtons = nav.ToArray();

        _selectedIndex = 0;
        if (_navButtons.Length > 0) SetSelection(0);

        // Stop player from moving while menu is open
        if (_characterMovement != null) _characterMovement.enabled = false;
    }

    public void GoToBeach()  { ConfirmDestination(SCENE_BEACH,  "Beach"); }
    public void GoToForest() { ConfirmDestination(SCENE_FOREST, "Forest"); }
    public void GoToOffice() { ConfirmDestination(SCENE_OFFICE, "Main Office"); }

    public void CloseMenu()
    {
        ChosenScene = "";
        _isOpen = false;
        if (menuCanvas != null) menuCanvas.SetActive(false);
        SetButtonsVisible(true);
        _guiMessage = "";
        if (_characterMovement != null) _characterMovement.enabled = true;
        ResetAllButtonColors();
    }

    public static void TeleportNow()
    {
        if (!string.IsNullOrEmpty(ChosenScene))
            SceneManager.LoadScene(ChosenScene);
    }

    private void ConfirmDestination(string sceneName, string displayName)
    {
        ChosenScene = sceneName;
        _isOpen = false;

        if (menuCanvas != null) menuCanvas.SetActive(false);
        if (teleportObject != null) teleportObject.SetActive(true);

        _guiMessage = "Head to the teleportation zone\nto travel to the " + displayName + "!";
        _guiHideTime = Time.time + 3f;

        if (_characterMovement != null) _characterMovement.enabled = true;
    }

    private void ConfirmSelection()
    {
        if (_navButtons == null || _navButtons.Length == 0) return;
        Button sel = _navButtons[_selectedIndex];
        if      (sel == beachButton)  GoToBeach();
        else if (sel == forestButton) GoToForest();
        else if (sel == officeButton) GoToOffice();
        else if (sel == cancelButton) CloseMenu();
    }

    private void SetButtonsVisible(bool visible)
    {
        if (beachButton  != null) beachButton.gameObject.SetActive(visible);
        if (forestButton != null) forestButton.gameObject.SetActive(visible);
        if (officeButton != null) officeButton.gameObject.SetActive(visible);
        if (cancelButton != null) cancelButton.gameObject.SetActive(visible);
    }

    private void SetSelection(int index)
    {
        if (_navButtons != null && _selectedIndex < _navButtons.Length)
            SetButtonColor(_navButtons[_selectedIndex], COLOR_NORMAL);
        _selectedIndex = index;
        if (_navButtons != null && _selectedIndex < _navButtons.Length)
            SetButtonColor(_navButtons[_selectedIndex], COLOR_SELECTED);
    }

    private void MarkCurrentScene(string current)
    {
        if (current == SCENE_BEACH  && beachButton  != null) SetButtonColor(beachButton,  COLOR_CURRENT);
        if (current == SCENE_FOREST && forestButton != null) SetButtonColor(forestButton, COLOR_CURRENT);
        if (current == SCENE_OFFICE && officeButton != null) SetButtonColor(officeButton, COLOR_CURRENT);
    }

    private void ResetAllButtonColors()
    {
        string current = SceneManager.GetActiveScene().name;
        SetButtonColor(beachButton,  current == SCENE_BEACH  ? COLOR_CURRENT : COLOR_NORMAL);
        SetButtonColor(forestButton, current == SCENE_FOREST ? COLOR_CURRENT : COLOR_NORMAL);
        SetButtonColor(officeButton, current == SCENE_OFFICE ? COLOR_CURRENT : COLOR_NORMAL);
        SetButtonColor(cancelButton, COLOR_NORMAL);
    }

    private void SetButtonColor(Button btn, Color color)
    {
        if (btn == null) return;
        Image img = btn.GetComponent<Image>();
        if (img != null) img.color = color;
    }
}
