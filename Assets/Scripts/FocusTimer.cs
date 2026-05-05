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
    }

    void Update()
    {
        if (!IsRunning) return;

        SecondsLeft -= Time.deltaTime;
        SecondsLeft  = Mathf.Max(SecondsLeft, 0f);
        Progress     = 1f - (SecondsLeft / _totalSeconds);

        OnProgressChanged?.Invoke(Progress);

        if (SecondsLeft <= 0f)
        {
            IsRunning   = false;
            IsFocusMode = false;
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
}
