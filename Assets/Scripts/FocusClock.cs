using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

[ExecuteAlways]
[RequireComponent(typeof(FocusTimer))]
public class FocusClockPrefab : MonoBehaviour
{
    [Header("Canvas Position in World")]
    public Vector3 worldPosition  = new Vector3(0f, 1.5f, 2f);
    public float   clockWorldSize = 0.6f;

    [Header("Default Session Duration")]
    [Tooltip("Duration in seconds (10 = 10s, 1500 = 25 min). Minimum 10 seconds.")]
    public float defaultSeconds = 1500f;

    [Header("Reticle / Gaze Settings")]
    [Tooltip("If true, a reticle dot is rendered where camera forward hits the canvas")]
    public bool  showReticleDot = true;
    [Tooltip("Controller button used to click (default: JoystickButton1 = Button 2). Also accepts screen tap.")]
    public KeyCode clickButton = KeyCode.JoystickButton1;

    /// <summary>
    /// Other scripts can read this to know when to ignore keyboard input.
    /// True while the setup panel is visible (timer not running).
    /// </summary>
    public bool IsKeyboardLocked { get; private set; }

    // ─── Private Fields ───────────────────────────────────────────────────────

    private FocusTimer _timer;
    private Canvas     _canvas;
    private Camera     _cachedCam;      // cached camera reference

    private Image    _skyBg, _glowRing, _progressRing, _innerMask, _clockFace;
    private Image    _sunIcon, _sunGlow, _moonIcon;
    private TMP_Text _timeText, _statusText, _durationLabel;
    private GameObject _setupPanel, _sessionPanel;
    private RectTransform _handPivot;
    private Image[]  _stars;
    private Image    _reticleDot;

    private float _chosenSeconds;

    // Reticle state
    private GameObject _gazeTarget;    // button currently under gaze (for highlight)

    // Cached gaze hit (used by both raycasting and reticle positioning)
    private bool    _gazeHitsCanvas;
    private Vector2 _gazeCanvasLocal;   // local position on canvas where gaze lands
    private Vector2 _gazeScreenPoint;   // screen-space point of gaze on canvas

    private const int   STAR_COUNT   = 36;
    private const float DURATION_MIN = 10f;
    private const float DURATION_MAX = 3600f;

    private static readonly Color[] SKY_COLORS = {
        new Color(0.98f, 0.75f, 0.45f),
        new Color(0.50f, 0.75f, 0.95f),
        new Color(0.95f, 0.52f, 0.22f),
        new Color(0.28f, 0.18f, 0.42f),
        new Color(0.04f, 0.05f, 0.12f),
    };
    private static readonly float[] SKY_STOPS = { 0f, 0.25f, 0.55f, 0.75f, 1.0f };

    private static readonly Color FACE_DAY   = new Color(0.15f, 0.12f, 0.08f, 0.92f);
    private static readonly Color FACE_NIGHT = new Color(0.06f, 0.06f, 0.10f, 0.96f);
    private static readonly Color RING_DAY   = new Color(1.00f, 0.85f, 0.30f, 1.00f);
    private static readonly Color RING_WARN  = new Color(1.00f, 0.32f, 0.15f, 1.00f);
    private static readonly Color TEXT_DAY   = new Color(1.00f, 0.92f, 0.65f);
    private static readonly Color TEXT_NIGHT = new Color(0.80f, 0.85f, 1.00f);

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    void OnEnable()
    {
        _timer = GetComponent<FocusTimer>();
        _chosenSeconds = Mathf.Clamp(defaultSeconds, DURATION_MIN, DURATION_MAX);

        var existing = transform.Find("ClockCanvas");
        if (existing != null) DestroyImmediate(existing.gameObject);

        BuildClockHierarchy();

        if (Application.isPlaying)
        {
            _timer.sessionMinutes = _chosenSeconds / 60f;
            _timer.OnTimerStart.AddListener(OnSessionStart);
            _timer.OnTimerEnd.AddListener(OnSessionEnd);
            _timer.OnProgressChanged.AddListener(OnProgress);
            _timer.OnFocusModeExit.AddListener(OnSessionEnd);
        }
    }

    void OnDisable()
    {
        if (_timer == null) return;
        _timer.OnTimerStart.RemoveListener(OnSessionStart);
        _timer.OnTimerEnd.RemoveListener(OnSessionEnd);
        _timer.OnProgressChanged.RemoveListener(OnProgress);
        _timer.OnFocusModeExit.RemoveListener(OnSessionEnd);
        IsKeyboardLocked = false;
    }

    void Update()
    {
        if (_canvas != null)
        {
            _canvas.transform.position   = worldPosition;
            _canvas.transform.localScale = Vector3.one * (clockWorldSize / 400f);
        }

        // Keep camera reference fresh
        _cachedCam = GetActiveCamera();
        if (_canvas != null && _canvas.worldCamera == null)
            _canvas.worldCamera = _cachedCam;

        if (!Application.isPlaying)
        {
            OnProgress(0f);
            return;
        }

        // ── Live countdown ──
        if (_timer != null && _timer.IsRunning && _timeText != null)
            _timeText.text = FormatTime(_timer.SecondsLeft);

        // ── Keyboard lock flag (other scripts check this) ──
        IsKeyboardLocked = _setupPanel != null && _setupPanel.activeSelf;

        // ── Gaze ray → canvas intersection (VR-safe) ──
        ComputeGazePoint();

        // ── Reticle / Dwell ──
        HandleReticle();
    }

    // ─── Camera Helper ────────────────────────────────────────────────────────

    Camera GetActiveCamera()
    {
        // 1. Camera.main (requires "MainCamera" tag)
        if (Camera.main != null) return Camera.main;

        // 2. Any camera tagged MainCamera
        var tagged = GameObject.FindGameObjectWithTag("MainCamera");
        if (tagged != null)
        {
            var cam = tagged.GetComponent<Camera>();
            if (cam != null && cam.isActiveAndEnabled) return cam;
        }

        // 3. Fallback: first active camera
        foreach (var cam in Camera.allCameras)
        {
            if (cam.isActiveAndEnabled) return cam;
        }
        return null;
    }

    // ─── Gaze Point Calculation (VR-safe) ─────────────────────────────────────

    /// <summary>
    /// Casts a ray from camera FORWARD (not screen centre) and finds where
    /// it hits the canvas plane. This works correctly in VR split-screen
    /// because it doesn't depend on screen pixel coordinates at all.
    /// </summary>
    void ComputeGazePoint()
    {
        _gazeHitsCanvas = false;

        if (_cachedCam == null || _canvas == null) return;

        RectTransform canvasRT = _canvas.GetComponent<RectTransform>();
        if (canvasRT == null) return;

        // Ray from camera position along camera forward
        Vector3 camPos = _cachedCam.transform.position;
        Vector3 camFwd = _cachedCam.transform.forward;

        // Canvas plane: defined by canvas position and its forward normal
        // (canvas faces -forward in world space, but we just need the plane)
        Vector3 canvasPos    = _canvas.transform.position;
        Vector3 canvasNormal = _canvas.transform.forward;

        float denom = Vector3.Dot(canvasNormal, camFwd);
        if (Mathf.Abs(denom) < 0.0001f) return;  // ray parallel to canvas

        float t = Vector3.Dot(canvasPos - camPos, canvasNormal) / denom;
        if (t < 0f) return;  // canvas is behind camera

        Vector3 worldHit = camPos + camFwd * t;

        // Convert world hit to canvas local coordinates
        Vector3 localHit3 = _canvas.transform.InverseTransformPoint(worldHit);
        _gazeCanvasLocal = new Vector2(localHit3.x, localHit3.y);

        // Check if hit is within canvas bounds
        Vector2 canvasSize = canvasRT.sizeDelta;
        Vector2 pivot      = canvasRT.pivot;
        float minX = -canvasSize.x * pivot.x;
        float maxX =  canvasSize.x * (1f - pivot.x);
        float minY = -canvasSize.y * pivot.y;
        float maxY =  canvasSize.y * (1f - pivot.y);

        if (_gazeCanvasLocal.x < minX || _gazeCanvasLocal.x > maxX ||
            _gazeCanvasLocal.y < minY || _gazeCanvasLocal.y > maxY)
            return;  // gaze is outside canvas

        // Convert world hit to screen-space for GraphicRaycaster
        _gazeScreenPoint = _cachedCam.WorldToScreenPoint(worldHit);
        _gazeHitsCanvas  = true;
    }

    // ─── Reticle / Dwell Input ────────────────────────────────────────────────

    void HandleReticle()
    {
        // Position the reticle dot on the canvas
        if (_reticleDot != null)
        {
            if (_gazeHitsCanvas)
            {
                _reticleDot.rectTransform.anchoredPosition = _gazeCanvasLocal;
                _reticleDot.enabled = true;
            }
            else
            {
                _reticleDot.enabled = false;
            }

            // Gentle pulse so user knows reticle is alive
            if (_reticleDot.enabled)
            {
                float pulse = 0.6f + 0.2f * Mathf.Sin(Time.time * 3f);
                Color c = _reticleDot.color;
                c.a = pulse;
                _reticleDot.color = c;
            }
        }

        // Only do hit-testing if gaze is on the canvas
        if (!_gazeHitsCanvas || EventSystem.current == null) return;

        // Check for controller click (Button 2) or screen tap
        bool clicked = Input.GetKeyDown(clickButton)
                    || Input.GetMouseButtonDown(0);

        if (!clicked) return;

        // Raycast to find which button the reticle is on
        var eventData = new PointerEventData(EventSystem.current)
        {
            position = _gazeScreenPoint
        };

        var results = new System.Collections.Generic.List<RaycastResult>();
        var raycaster = _canvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
            raycaster.Raycast(eventData, results);

        foreach (var r in results)
        {
            var btn = r.gameObject.GetComponent<Button>()
                   ?? r.gameObject.GetComponentInParent<Button>();
            if (btn != null)
            {
                btn.onClick.Invoke();
                break;
            }
        }
    }

    // ─── Timer Events ─────────────────────────────────────────────────────────

    void OnSessionStart()
    {
        _setupPanel?.SetActive(false);
        _sessionPanel?.SetActive(true);
        if (_statusText != null) { _statusText.text = "FOCUS"; _statusText.color = TEXT_DAY; }
        if (_timeText   != null) _timeText.color = TEXT_DAY;
        ApplySkyColor(0f);
    }

    void OnSessionEnd()
    {
        if (_statusText != null)
            _statusText.text = _timer.SecondsLeft <= 0f ? "SESSION COMPLETE" : "ENDED";
        Invoke(nameof(ShowSetup), 3.5f);
    }

    void ShowSetup()
    {
        _setupPanel?.SetActive(true);
        _sessionPanel?.SetActive(false);
    }

    // ─── Duration Adjustment ──────────────────────────────────────────────────

    void AdjustDuration(float deltaSeconds)
    {
        _chosenSeconds = Mathf.Clamp(_chosenSeconds + deltaSeconds, DURATION_MIN, DURATION_MAX);
        _chosenSeconds = Mathf.Round(_chosenSeconds / 10f) * 10f;

        if (_timer != null) _timer.sessionMinutes = _chosenSeconds / 60f;
        if (_durationLabel != null) _durationLabel.text = FormatDurationLabel(Mathf.RoundToInt(_chosenSeconds));
        if (_timeText != null && (_timer == null || !_timer.IsRunning))
            _timeText.text = FormatTime(_chosenSeconds);
    }

    // ─── Time Formatting ──────────────────────────────────────────────────────

    string FormatTime(float seconds)
    {
        int totalSec = Mathf.CeilToInt(seconds);
        int mm = totalSec / 60;
        int ss = totalSec % 60;
        return $"{mm:00}:{ss:00}";
    }

    string FormatDurationLabel(int totalSec)
    {
        if (totalSec < 60)   return $"{totalSec} sec";
        int mm = totalSec / 60;
        int ss = totalSec % 60;
        return ss == 0 ? $"{mm} min" : $"{mm}m {ss}s";
    }

    // ─── Progress ─────────────────────────────────────────────────────────────

    void OnProgress(float p)
    {
        UpdateSky(p);
        UpdateProgressRing(p);
        UpdateClockHand(p);
        UpdateCelestialOrbit(p);
        UpdateStars(p);
        if (Application.isPlaying) UpdateWarningPulse(p);
    }

    void UpdateSky(float p)
    {
        ApplySkyColor(p);
        if (_clockFace != null) _clockFace.color = Color.Lerp(FACE_DAY, FACE_NIGHT, p);
        if (_innerMask != null) _innerMask.color  = _skyBg.color;
    }

    void ApplySkyColor(float p)
    {
        if (_skyBg == null) return;
        for (int i = 0; i < SKY_STOPS.Length - 1; i++)
        {
            if (p >= SKY_STOPS[i] && p <= SKY_STOPS[i + 1])
            {
                float t = Mathf.InverseLerp(SKY_STOPS[i], SKY_STOPS[i + 1], p);
                _skyBg.color = Color.Lerp(SKY_COLORS[i], SKY_COLORS[i + 1], t);
                return;
            }
        }
        _skyBg.color = SKY_COLORS[SKY_COLORS.Length - 1];
    }

    void UpdateProgressRing(float p)
    {
        if (_progressRing == null) return;
        _progressRing.fillAmount = 1f - p;
        float w = Mathf.Clamp01((p - 0.70f) / 0.30f);
        Color c = Color.Lerp(RING_DAY, RING_WARN, w);
        if (_glowRing != null) _glowRing.color = c;
        _progressRing.color = c;
    }

    void UpdateClockHand(float p)
    {
        if (_handPivot != null)
            _handPivot.localRotation = Quaternion.Euler(0f, 0f, -360f * p);
    }

    void UpdateCelestialOrbit(float p)
    {
        float sunP   = Mathf.Clamp01(p / 0.70f);
        float sunAng = Mathf.Lerp(180f, 0f, sunP) * Mathf.Deg2Rad;
        float sunX   = Mathf.Cos(sunAng) * 110f;
        float sunY   = Mathf.Abs(Mathf.Sin(sunAng)) * 110f + 10f;
        float sunA   = Mathf.Lerp(1f, 0f, Mathf.Clamp01((p - 0.60f) / 0.15f));
        float sunS   = Mathf.Lerp(1f, 0.1f, Mathf.Clamp01((p - 0.55f) / 0.20f));

        if (_sunIcon != null)
        {
            _sunIcon.rectTransform.anchoredPosition = new Vector2(sunX, sunY);
            _sunIcon.rectTransform.localScale       = Vector3.one * sunS;
            Color sc = _sunIcon.color; sc.a = sunA; _sunIcon.color = sc;
        }
        if (_sunGlow != null)
        {
            _sunGlow.rectTransform.anchoredPosition = new Vector2(sunX, sunY);
            Color sg = _sunGlow.color; sg.a = sunA * 0.35f; _sunGlow.color = sg;
        }

        float moonApp = Mathf.Clamp01((p - 0.50f) / 0.20f);
        float moonP   = Mathf.Clamp01((p - 0.50f) / 0.50f);
        float moonAng = Mathf.Lerp(0f, 180f, moonP) * Mathf.Deg2Rad;
        float moonX   = Mathf.Cos(moonAng) * 105f;
        float moonY   = Mathf.Abs(Mathf.Sin(moonAng)) * 105f + 10f;

        if (_moonIcon != null)
        {
            float sway = Application.isPlaying ? Mathf.Sin(Time.time * 0.6f + 1f) * 1.5f : 0f;
            _moonIcon.rectTransform.anchoredPosition = new Vector2(moonX + sway * 0.3f, moonY + sway);
            _moonIcon.rectTransform.localScale       = Vector3.one * Mathf.Lerp(0.1f, 1f, moonApp);
            Color mc = _moonIcon.color; mc.a = moonApp; _moonIcon.color = mc;
        }

        float nb = Mathf.Clamp01((p - 0.65f) / 0.25f);
        if (_timeText   != null && (_timer == null || !_timer.IsRunning))
            _timeText.color = Color.Lerp(TEXT_DAY, TEXT_NIGHT, nb);
        if (_statusText != null)
            _statusText.color = Color.Lerp(TEXT_DAY, TEXT_NIGHT, nb);
    }

    void UpdateStars(float p)
    {
        if (_stars == null) return;

        float baseAlpha = Application.isPlaying
            ? Mathf.Clamp01((p - 0.50f) / 0.15f)
            : 0.92f;

        for (int i = 0; i < _stars.Length; i++)
        {
            float twinkle = Application.isPlaying
                ? 0.80f + 0.20f * Mathf.Sin(Time.time * (1.3f + i * 0.25f) + i)
                : 1.0f;
            Color c = _stars[i].color;
            c.a = baseAlpha * twinkle;
            _stars[i].color = c;
        }
    }

    void UpdateWarningPulse(float p)
    {
        if (_timer == null || !_timer.IsInWarningZone(0.25f) || _glowRing == null) return;
        float pulse = 0.70f + 0.30f * Mathf.Sin(Time.time * 3.5f);
        Color gc = _glowRing.color; gc.a = pulse; _glowRing.color = gc;
    }

    // ─── Builder ──────────────────────────────────────────────────────────────

    void BuildClockHierarchy()
    {
        var canvasGO       = new GameObject("ClockCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvasGO.hideFlags = HideFlags.DontSave;

        _canvas            = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.worldCamera = GetActiveCamera();
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var crt            = canvasGO.GetComponent<RectTransform>();
        crt.sizeDelta      = new Vector2(400f, 400f);
        canvasGO.transform.position   = worldPosition;
        canvasGO.transform.localScale = Vector3.one * (clockWorldSize / 400f);

        // Only create EventSystem if none exists — don't conflict with VR SDK
        if (Application.isPlaying && FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        // ── Sky ──
        _skyBg = MakeImage(canvasGO, "SkyBg", MakeCircleTex(200, SKY_COLORS[0]));
        _skyBg.rectTransform.sizeDelta        = new Vector2(380f, 380f);
        _skyBg.rectTransform.anchoredPosition = Vector2.zero;
        _skyBg.color = SKY_COLORS[0];

        // ── Stars ──
        _stars = new Image[STAR_COUNT];
        var starTex = MakeCircleTex(5, Color.white);
        var rng = new System.Random(42);
        for (int i = 0; i < STAR_COUNT; i++)
        {
            float angle  = (float)rng.NextDouble() * 360f;
            float radius = 50f + (float)rng.NextDouble() * 130f;
            float size   = 3.5f + (float)rng.NextDouble() * 4.5f;

            _stars[i] = MakeImage(canvasGO, $"Star{i}", starTex);
            _stars[i].rectTransform.sizeDelta = Vector2.one * size;
            _stars[i].rectTransform.anchoredPosition = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                Mathf.Sin(angle * Mathf.Deg2Rad) * radius);
            _stars[i].color = new Color(1f, 1f, 1f, 0.92f);
        }

        // ── Outer glow ring ──
        _glowRing = MakeImage(canvasGO, "GlowRing", MakeRingTex(192, 176, RING_DAY));
        _glowRing.rectTransform.sizeDelta        = new Vector2(384f, 384f);
        _glowRing.rectTransform.anchoredPosition = Vector2.zero;
        _glowRing.color = RING_DAY;

        // ── Progress ring ──
        _progressRing = MakeImage(canvasGO, "ProgressRing", MakeCircleTex(170, RING_DAY));
        _progressRing.type          = Image.Type.Filled;
        _progressRing.fillMethod    = Image.FillMethod.Radial360;
        _progressRing.fillOrigin    = (int)Image.Origin360.Top;
        _progressRing.fillClockwise = true;
        _progressRing.fillAmount    = 1f;
        _progressRing.color         = RING_DAY;
        _progressRing.rectTransform.sizeDelta        = new Vector2(340f, 340f);
        _progressRing.rectTransform.anchoredPosition = Vector2.zero;

        // ── Inner mask ──
        _innerMask = MakeImage(canvasGO, "InnerMask", MakeCircleTex(152, SKY_COLORS[0]));
        _innerMask.color = SKY_COLORS[0];
        _innerMask.rectTransform.sizeDelta        = new Vector2(304f, 304f);
        _innerMask.rectTransform.anchoredPosition = Vector2.zero;

        // ── Clock face ──
        _clockFace = MakeImage(canvasGO, "ClockFace", MakeCircleTex(148, FACE_DAY));
        _clockFace.color = FACE_DAY;
        _clockFace.rectTransform.sizeDelta        = new Vector2(296f, 296f);
        _clockFace.rectTransform.anchoredPosition = Vector2.zero;

        var deco = MakeImage(_clockFace.gameObject, "InnerDeco",
            MakeRingTex(110, 105, new Color(1f, 0.85f, 0.3f, 0.22f)));
        deco.rectTransform.sizeDelta        = new Vector2(220f, 220f);
        deco.rectTransform.anchoredPosition = Vector2.zero;

        for (int i = 0; i < 12; i++)
        {
            float deg  = i * 30f;
            float rad  = (deg - 90f) * Mathf.Deg2Rad;
            bool  card = i % 3 == 0;
            var   tick = MakeImage(_clockFace.gameObject, $"Tick{i}",
                MakeRectTex(card ? 5 : 3, card ? 22 : 14, new Color(1f, 0.85f, 0.4f)));
            tick.rectTransform.sizeDelta        = card ? new Vector2(5f, 22f) : new Vector2(3f, 14f);
            tick.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(rad)*122f, Mathf.Sin(rad)*122f);
            tick.rectTransform.localRotation    = Quaternion.Euler(0f, 0f, -deg);
            tick.color = card
                ? new Color(1f, 0.85f, 0.30f, 0.95f)
                : new Color(1f, 0.85f, 0.30f, 0.45f);
        }

        // ── Hand ──
        var pGO    = new GameObject("HandPivot");
        pGO.transform.SetParent(_clockFace.transform, false);
        _handPivot = pGO.AddComponent<RectTransform>();
        _handPivot.sizeDelta = _handPivot.anchoredPosition = Vector2.zero;

        var hand  = MakeImage(pGO, "Hand", MakeRectTex(5, 88, new Color(1f, 0.88f, 0.45f)));
        hand.color = new Color(1f, 0.88f, 0.45f);
        hand.rectTransform.sizeDelta        = new Vector2(6f, 88f);
        hand.rectTransform.anchoredPosition = new Vector2(0f, 44f);
        hand.rectTransform.pivot            = new Vector2(0.5f, 0f);

        var tail  = MakeImage(pGO, "Tail", MakeRectTex(3, 26, new Color(1f, 0.45f, 0.15f)));
        tail.color = new Color(1f, 0.45f, 0.15f);
        tail.rectTransform.sizeDelta        = new Vector2(3f, 26f);
        tail.rectTransform.anchoredPosition = new Vector2(0f, -26f);
        tail.rectTransform.pivot            = new Vector2(0.5f, 1f);

        var cap   = MakeImage(_clockFace.gameObject, "Cap", MakeCircleTex(7, new Color(1f, 0.88f, 0.45f)));
        cap.color = new Color(1f, 0.88f, 0.45f);
        cap.rectTransform.sizeDelta        = new Vector2(14f, 14f);
        cap.rectTransform.anchoredPosition = Vector2.zero;

        // ── Sun ──
        _sunIcon = MakeImage(canvasGO, "Sun", MakeCircleTex(14, new Color(1f, 0.88f, 0.2f)));
        _sunIcon.color = new Color(1f, 0.88f, 0.2f);
        _sunIcon.rectTransform.sizeDelta        = new Vector2(28f, 28f);
        _sunIcon.rectTransform.anchoredPosition = new Vector2(-110f, 10f);

        _sunGlow = MakeImage(canvasGO, "SunGlow", MakeCircleTex(24, new Color(1f, 0.8f, 0.1f, 0.22f)));
        _sunGlow.color = new Color(1f, 0.8f, 0.1f, 0.22f);
        _sunGlow.rectTransform.sizeDelta        = new Vector2(48f, 48f);
        _sunGlow.rectTransform.anchoredPosition = new Vector2(-110f, 10f);

        // ── Moon ──
        _moonIcon = MakeImage(canvasGO, "Moon", MakeCircleTex(14, new Color(0.88f, 0.92f, 1f)));
        _moonIcon.color = new Color(0.88f, 0.92f, 1f, 0f);
        _moonIcon.rectTransform.sizeDelta        = new Vector2(28f, 28f);
        _moonIcon.rectTransform.anchoredPosition = new Vector2(110f, 10f);
        _moonIcon.rectTransform.localScale       = Vector3.one * 0.1f;

        // ── Time text ──
        _timeText = MakeText(canvasGO, "TimeText", FormatTime(_chosenSeconds), 40);
        _timeText.rectTransform.anchoredPosition = new Vector2(0f, -38f);
        _timeText.color    = TEXT_DAY;
        _timeText.fontStyle = FontStyles.Bold;

        _statusText = MakeText(canvasGO, "StatusText", "SET FOCUS TIME", 10);
        _statusText.rectTransform.anchoredPosition = new Vector2(0f, -65f);
        _statusText.color = new Color(1f, 0.9f, 0.6f, 0.65f);
        _statusText.characterSpacing = 2.5f;

        // ══════════════════════════════════════════════════════════════════════
        //  SETUP PANEL — increment/decrement buttons instead of slider
        // ══════════════════════════════════════════════════════════════════════
        _setupPanel = new GameObject("SetupPanel");
        _setupPanel.transform.SetParent(canvasGO.transform, false);
        var spRT = _setupPanel.AddComponent<RectTransform>();
        spRT.sizeDelta        = new Vector2(280f, 120f);
        spRT.anchoredPosition = new Vector2(0f, -150f);

        _durationLabel = MakeText(_setupPanel, "DurationLabel",
            FormatDurationLabel(Mathf.RoundToInt(_chosenSeconds)), 17);
        _durationLabel.rectTransform.anchoredPosition = new Vector2(0f, 42f);
        _durationLabel.color     = new Color(1f, 0.88f, 0.55f);
        _durationLabel.fontStyle = FontStyles.Bold;

        // ── Increment / Decrement Buttons Row ──
        //    Layout:  [ -5m ] [ -1m ] [ +1m ] [ +5m ]
        float btnY     = 14f;
        float btnW     = 52f;
        float btnH     = 28f;   // slightly taller for easier dwell targeting
        float gap      = 6f;
        float totalW   = btnW * 4f + gap * 3f;
        float startX   = -totalW * 0.5f + btnW * 0.5f;

        Color btnBgColor   = new Color(1f, 0.85f, 0.3f, 0.25f);
        Color btnTextColor = new Color(1f, 0.88f, 0.55f);

        string[] labels = { "-5m", "-1m", "+1m", "+5m" };
        float[]  deltas = { -300f,  -60f,  60f,  300f };

        for (int i = 0; i < 4; i++)
        {
            float xPos = startX + i * (btnW + gap);
            CreateAdjustButton(_setupPanel, labels[i], deltas[i],
                xPos, btnY, btnW, btnH, btnBgColor, btnTextColor);
        }

        // ── Start Button ──
        var startBtnImg = MakeImage(_setupPanel, "StartBtn",
            MakeRoundRectTex(150, 36, 8, new Color(1f, 0.75f, 0.15f)));
        startBtnImg.color = new Color(1f, 0.75f, 0.15f);
        startBtnImg.rectTransform.sizeDelta        = new Vector2(150f, 36f);
        startBtnImg.rectTransform.anchoredPosition = new Vector2(0f, -22f);

        var startBtnText = MakeText(startBtnImg.gameObject, "BtnText", "START FOCUS", 13);
        startBtnText.color     = new Color(0.10f, 0.08f, 0.02f);
        startBtnText.fontStyle = FontStyles.Bold;
        startBtnText.rectTransform.anchoredPosition = Vector2.zero;

        if (Application.isPlaying)
        {
            var btn = startBtnImg.gameObject.AddComponent<Button>();
            btn.targetGraphic = startBtnImg;
            btn.onClick.AddListener(() => _timer.StartSession());
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SESSION PANEL — visible while timer is running
        // ══════════════════════════════════════════════════════════════════════
        _sessionPanel = new GameObject("SessionPanel");
        _sessionPanel.transform.SetParent(canvasGO.transform, false);
        var sesRT = _sessionPanel.AddComponent<RectTransform>();
        sesRT.sizeDelta        = new Vector2(130f, 30f);
        sesRT.anchoredPosition = new Vector2(0f, -158f);
        _sessionPanel.SetActive(false);

        var exitImg = MakeImage(_sessionPanel, "ExitBtn",
            MakeRoundRectTex(120, 28, 6, new Color(1f, 1f, 1f, 0.08f)));
        exitImg.color = new Color(1f, 1f, 1f, 0.10f);
        exitImg.rectTransform.sizeDelta        = new Vector2(122f, 28f);
        exitImg.rectTransform.anchoredPosition = Vector2.zero;

        var exitText = MakeText(exitImg.gameObject, "ExitText", "END SESSION", 9);
        exitText.color            = new Color(1f, 0.85f, 0.5f, 0.55f);
        exitText.characterSpacing = 1.5f;
        exitText.rectTransform.anchoredPosition = Vector2.zero;

        if (Application.isPlaying)
        {
            var exitBtn = exitImg.gameObject.AddComponent<Button>();
            exitBtn.targetGraphic = exitImg;
            exitBtn.onClick.AddListener(() => _timer.ExitFocusMode());
        }

        // ── Reticle (gaze indicator — follows camera forward on canvas) ──
        if (showReticleDot)
        {
            // Outer ring — larger for phone visibility
            _reticleDot = MakeImage(canvasGO, "ReticleDot",
                MakeRingTex(14, 10, new Color(1f, 1f, 1f, 0.90f)));
            _reticleDot.color = new Color(1f, 1f, 1f, 0.65f);
            _reticleDot.rectTransform.sizeDelta        = new Vector2(28f, 28f);
            _reticleDot.rectTransform.anchoredPosition = Vector2.zero;

            // Centre dot for visibility
            var centreDot = MakeImage(_reticleDot.gameObject, "CentreDot",
                MakeCircleTex(4, new Color(1f, 1f, 1f, 0.90f)));
            centreDot.color = new Color(1f, 1f, 1f, 0.80f);
            centreDot.rectTransform.sizeDelta        = new Vector2(6f, 6f);
            centreDot.rectTransform.anchoredPosition = Vector2.zero;
        }
    }

    // ─── Adjust Button Factory ────────────────────────────────────────────────

    void CreateAdjustButton(GameObject parent, string label, float deltaSec,
        float x, float y, float w, float h, Color bgColor, Color textColor)
    {
        var bg = MakeImage(parent, $"Btn{label}",
            MakeRoundRectTex((int)w, (int)h, 6, bgColor));
        bg.color = bgColor;
        bg.rectTransform.sizeDelta        = new Vector2(w, h);
        bg.rectTransform.anchoredPosition = new Vector2(x, y);

        var txt = MakeText(bg.gameObject, "Label", label, 11);
        txt.color     = textColor;
        txt.fontStyle = FontStyles.Bold;
        txt.rectTransform.anchoredPosition = Vector2.zero;
        txt.rectTransform.sizeDelta        = new Vector2(w, h);

        if (Application.isPlaying)
        {
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            float delta = deltaSec;
            btn.onClick.AddListener(() => AdjustDuration(delta));
        }
    }

    // ─── Texture Factories ────────────────────────────────────────────────────

    Texture2D MakeCircleTex(int radius, Color color)
    {
        int size = radius * 2;
        var tex  = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px   = new Color[size * size];
        float cx = radius - 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Mathf.Sqrt((x-cx)*(x-cx)+(y-cx)*(y-cx));
            float a = Mathf.Clamp01(cx - d + 1f);
            px[y*size+x] = new Color(color.r, color.g, color.b, color.a * a);
        }
        tex.SetPixels(px); tex.Apply(); return tex;
    }

    Texture2D MakeRingTex(int outerR, int innerR, Color color)
    {
        int size = outerR * 2;
        var tex  = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px   = new Color[size * size];
        float cx = outerR - 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Mathf.Sqrt((x-cx)*(x-cx)+(y-cx)*(y-cx));
            float a = Mathf.Clamp01(outerR-d+1f) * Mathf.Clamp01(d-innerR+1f);
            px[y*size+x] = new Color(color.r, color.g, color.b, color.a * a);
        }
        tex.SetPixels(px); tex.Apply(); return tex;
    }

    Texture2D MakeRectTex(int w, int h, Color color)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px  = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = color;
        tex.SetPixels(px); tex.Apply(); return tex;
    }

    Texture2D MakeRoundRectTex(int w, int h, int corner, Color color)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px  = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int   qx = Mathf.Clamp(x, corner, w-corner-1);
            int   qy = Mathf.Clamp(y, corner, h-corner-1);
            float d  = Mathf.Sqrt((x-qx)*(x-qx)+(float)(y-qy)*(y-qy));
            bool  inC = x<corner||x>w-corner-1||y<corner||y>h-corner-1;
            float a  = inC ? Mathf.Clamp01(corner-d+1f) : 1f;
            px[y*w+x] = new Color(color.r, color.g, color.b, color.a * a);
        }
        tex.SetPixels(px); tex.Apply(); return tex;
    }

    Image MakeImage(GameObject parent, string name, Texture2D tex)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.sprite = Sprite.Create(tex,
            new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        img.color = Color.white;
        return img;
    }

    TMP_Text MakeText(GameObject parent, string name, string content, int fontSize)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = content;
        tmp.fontSize  = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = new Vector2(300f, 50f);
        tmp.color = Color.white;
        return tmp;
    }
}
