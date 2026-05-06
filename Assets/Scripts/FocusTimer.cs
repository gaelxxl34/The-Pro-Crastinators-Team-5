using UnityEngine;
using UnityEngine.Events;

public class FocusTimer : MonoBehaviour
{
    [Header("Session Duration")]
    [Tooltip("Default session length in minutes")]
    public float sessionMinutes = 25f;

    [Header("Events — subscribe from other scripts")]
    public UnityEvent         OnTimerStart;
    public UnityEvent         OnTimerEnd;
    public UnityEvent         OnFocusModeExit;
    public UnityEvent<float>  OnProgressChanged;  // fires every frame: 0.0 → 1.0

    [Header("Directional Light (Day → Night)")]
    [Tooltip("Drag the scene's Directional Light here. Leave empty to disable lighting changes.")]
    public Light sunLight;

    [Tooltip("Sun rotation at the start of the session (day).")]
    public Vector3 dayEulerAngles = new Vector3(50f, -30f, 0f);
    [Tooltip("Sun rotation when the timer ends (night).")]
    public Vector3 nightEulerAngles = new Vector3(-20f, -30f, 0f);

    [Min(0f)] public float dayIntensity   = 1.2f;
    [Min(0f)] public float nightIntensity = 0.05f;

    public Color dayColor   = new Color(1.0f, 0.95f, 0.85f);
    public Color nightColor = new Color(0.25f, 0.30f, 0.55f);

    [Tooltip("Smoothing curve applied to the 0→1 progress before lerping the light.")]
    public AnimationCurve lightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    // Read-only public state
    public bool  IsRunning   { get; private set; }
    public bool  IsFocusMode { get; private set; }
    public float Progress    { get; private set; }
    public float SecondsLeft { get; private set; }

    private float _totalSeconds;

    void Start()
    {
        _totalSeconds = sessionMinutes * 60f;
        SecondsLeft   = _totalSeconds;
        ApplySunState(0f);
    }

    void Update()
    {
        if (!IsRunning) return;

        SecondsLeft -= Time.deltaTime;
        SecondsLeft  = Mathf.Max(SecondsLeft, 0f);
        Progress     = 1f - (SecondsLeft / _totalSeconds);

        OnProgressChanged?.Invoke(Progress);
        ApplySunState(Progress);

        if (SecondsLeft <= 0f)
        {
            IsRunning   = false;
            IsFocusMode = false;
            ApplySunState(1f);
            OnTimerEnd?.Invoke();
        }
    }

    public void StartSession()
    {
        _totalSeconds = sessionMinutes * 60f;
        SecondsLeft   = _totalSeconds;
        Progress      = 0f;
        IsRunning     = true;
        IsFocusMode   = true;
        ApplySunState(0f);
        OnTimerStart?.Invoke();
    }

    public void StartSession(float minutes)
    {
        sessionMinutes = minutes;
        StartSession();
    }

    public void ExitFocusMode()
    {
        IsRunning   = false;
        IsFocusMode = false;
        OnFocusModeExit?.Invoke();
    }

    public string GetFormattedTime()
    {
        int m = Mathf.FloorToInt(SecondsLeft / 60f);
        int s = Mathf.FloorToInt(SecondsLeft % 60f);
        return $"{m:00}:{s:00}";
    }

    public bool IsInWarningZone(float threshold = 0.25f) => Progress >= (1f - threshold);

    // ── Directional light: day → night based on progress ─────────────────────
    private void ApplySunState(float progress01)
    {
        if (sunLight == null) return;

        float t = Mathf.Clamp01(lightCurve.Evaluate(Mathf.Clamp01(progress01)));

        sunLight.transform.rotation =
            Quaternion.Euler(Vector3.Lerp(dayEulerAngles, nightEulerAngles, t));
        sunLight.intensity = Mathf.Lerp(dayIntensity, nightIntensity, t);
        sunLight.color     = Color.Lerp(dayColor, nightColor, t);
    }
}
