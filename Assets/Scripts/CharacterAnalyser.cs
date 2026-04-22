using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using Debug = UnityEngine.Debug;

public class CharacterAnalyser : MonoBehaviour
{
    [Header("Scroll View")]
    public RectTransform scrollContent;
    public GameObject entryPrefab;

    [Header("Server")]
    public float serverStartTimeout = 10f;

    // Fired when analysis completes — GraphController listens to this
    public static event Action<CharacterData[]> OnCharactersAnalysed;

    private readonly string _serverUrl = "http://localhost:5001/extract_characters";
    private readonly string _healthUrl = "http://localhost:5001/health";
    private Process _serverProcess;

    private string PaperPath =>
        Path.Combine(Application.dataPath, "Papers", "paper.txt");

    private string ServerScriptPath =>
        Path.Combine(Application.streamingAssetsPath, "character_server.py");

    private string PythonExecutable
    {
        get
        {
            string venvRoot = Path.Combine(Application.streamingAssetsPath, "nlp_env");
            string winPath = Path.Combine(venvRoot, "Scripts", "python.exe");
            string unixPath = Path.Combine(venvRoot, "bin", "python");
            if (File.Exists(winPath)) return winPath;
            if (File.Exists(unixPath)) return unixPath;
            // macOS: File.Exists can fail on venv symlinks; check the bin dir instead
            if (Directory.Exists(Path.Combine(venvRoot, "bin"))) return unixPath;
            return winPath;
        }
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Start()
    {
#if UNITY_IOS
        // Python/Flask server cannot run on iOS — skip entirely
        Debug.Log("[CharacterAnalyser] Skipped on iOS (Python not supported).");
#else
        StartCoroutine(LaunchAndAnalyse());
#endif
    }

    private void OnDestroy() => KillServer();
    private void OnApplicationQuit() => KillServer();

    // ── Main flow ─────────────────────────────────────────────────────────────
    private IEnumerator LaunchAndAnalyse()
    {
        if (!LaunchServer()) yield break;

        yield return StartCoroutine(WaitForServer());

        if (!File.Exists(PaperPath))
        {
            Debug.LogError($"[CharacterAnalyser] File not found: {PaperPath}");
            yield break;
        }

        string text = File.ReadAllText(PaperPath);
        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.LogWarning("[CharacterAnalyser] paper.txt is empty.");
            yield break;
        }

        string json = JsonUtility.ToJson(new TextPayload { text = text });
        byte[] body = Encoding.UTF8.GetBytes(json);

        using var req = new UnityWebRequest(_serverUrl, "POST");
        req.uploadHandler = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[CharacterAnalyser] Request failed: {req.error}");
            yield break;
        }

        var response = JsonUtility.FromJson<CharacterResponse>(req.downloadHandler.text);
        Debug.Log($"[CharacterAnalyser] Got {response.characters.Length} character(s).");

        PopulateScrollView(response.characters);

        // Fire event so GraphController can draw the line chart
        OnCharactersAnalysed?.Invoke(response.characters);
    }

    // ── Server process ────────────────────────────────────────────────────────
    private bool LaunchServer()
    {
        if (!File.Exists(ServerScriptPath))
        {
            Debug.LogError($"[CharacterAnalyser] Server script not found: {ServerScriptPath}");
            return false;
        }

        if (!File.Exists(PythonExecutable))
        {
            Debug.LogError($"[CharacterAnalyser] venv Python not found: {PythonExecutable}\n" +
                           "Run setup_nlp.sh (Mac/Linux) or setup_nlp.bat (Windows) once to create it.");
            return false;
        }

        var info = new ProcessStartInfo
        {
            FileName = PythonExecutable,
            Arguments = $"\"{ServerScriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        _serverProcess = new Process { StartInfo = info };
        _serverProcess.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.Log($"[Python] {e.Data}"); };
        _serverProcess.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.LogWarning($"[Python] {e.Data}"); };
        _serverProcess.Start();
        _serverProcess.BeginOutputReadLine();
        _serverProcess.BeginErrorReadLine();

        Debug.Log($"[CharacterAnalyser] Python server launched (PID {_serverProcess.Id})");
        return true;
    }

    private IEnumerator WaitForServer()
    {
        float elapsed = 0f;
        Debug.Log("[CharacterAnalyser] Waiting for server...");

        while (elapsed < serverStartTimeout)
        {
            using var req = UnityWebRequest.Get(_healthUrl);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[CharacterAnalyser] Server ready.");
                yield break;
            }

            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
        }

        Debug.LogError("[CharacterAnalyser] Server timed out.");
    }

    private void KillServer()
    {
        if (_serverProcess == null || _serverProcess.HasExited) return;
        _serverProcess.Kill();
        _serverProcess.Dispose();
        _serverProcess = null;
        Debug.Log("[CharacterAnalyser] Python server killed.");
    }

    // ── Scroll view ───────────────────────────────────────────────────────────
    private void PopulateScrollView(CharacterData[] characters)
    {
        foreach (Transform child in scrollContent)
            Destroy(child.gameObject);

        if (characters.Length == 0) { SpawnEntry("(no characters found)"); return; }

        foreach (var c in characters) SpawnEntry(c.name);

        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent);
    }

    private void SpawnEntry(string label)
    {
        var obj = Instantiate(entryPrefab, scrollContent);
        var tmp = obj.GetComponentInChildren<TMP_Text>();
        if (tmp != null) tmp.text = label;
    }

    // ── JSON helpers ──────────────────────────────────────────────────────────
    [Serializable] public class TextPayload { public string text; }
    [Serializable] public class CharacterData { public string name; public int[] counts; }
    [Serializable] public class CharacterResponse { public CharacterData[] characters; public int segments; }
}