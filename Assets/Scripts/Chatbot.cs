using System;
using System.Collections;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Simple OpenAI-powered chatbot.
/// Two text fields: one shows the current question, one shows the current answer.
/// Submitting a new question (Enter) replaces both — no history is shown on screen.
///
/// Setup:
///   1. Get an API key from https://platform.openai.com/api-keys
///   2. Create the file  Assets/StreamingAssets/openai_key.txt
///      and paste ONLY the key inside (no quotes, no spaces).
///      (Or set the OPENAI_API_KEY environment variable.)
///   3. Attach this script to any GameObject and wire up:
///        - inputField    : TMP_InputField the user types into
///        - questionField : TMP_Text that shows the submitted question
///        - answerField   : TMP_Text that shows the assistant's answer
///
/// Tip: set the InputField's Line Type to "Single Line" or "Multi Line Submit"
/// so pressing Enter triggers submission.
///
/// IMPORTANT: never commit your API key. Add to .gitignore:
///   Assets/StreamingAssets/openai_key.txt
/// </summary>
public class Chatbot : MonoBehaviour
{
    [Header("UI")]
    public TMP_InputField inputField;
    public TMP_Text questionField;
    public TMP_Text answerField;

    [Header("Model")]
    [Tooltip("OpenAI chat model. gpt-4o-mini is cheap and fast; gpt-4o is higher quality.")]
    public string model = "gpt-4o-mini";

    [Tooltip("System prompt — sets the assistant's persona and rules.")]
    [TextArea(3, 8)]
    public string systemPrompt =
        "You are a helpful writing assistant inside a VR app for a novelist. " +
        "Answer questions clearly and concisely.";

    [Tooltip("Sampling temperature (0 = deterministic, 1 = creative).")]
    [Range(0f, 2f)] public float temperature = 0.7f;

    [Tooltip("Max tokens in the assistant's reply.")]
    public int maxTokens = 400;

    [Tooltip("Text shown in the answer field while waiting for the API.")]
    public string thinkingText = "Thinking...";

    private const string Endpoint = "https://api.openai.com/v1/chat/completions";

    private string _apiKey;
    private bool _busy;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        _apiKey = LoadApiKey();
    }

    private void Start()
    {
        if (questionField != null) questionField.text = string.Empty;
        if (answerField   != null) answerField.text   = string.Empty;

        if (inputField != null)
        {
            inputField.onSubmit.AddListener(OnSubmit);
            inputField.onValueChanged.AddListener(OnInputChanged);
        }

        if (string.IsNullOrEmpty(_apiKey))
        {
            SetAnswer("[No API key found. Create Assets/StreamingAssets/openai_key.txt with your key.]");
            if (inputField != null) inputField.interactable = false;
        }
    }

    // If the user is typing into a Multi-Line input field, Enter is inserted as a newline
    // character and onSubmit never fires. Detect that newline and treat it as submit.
    private void OnInputChanged(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (text.IndexOf('\n') < 0 && text.IndexOf('\r') < 0) return;

        // Allow Shift+Enter to keep newlines.
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return;

        string clean = text.Replace("\r", "").Replace("\n", " ").Trim();
        OnSubmit(clean);
    }

    // ── Send flow ─────────────────────────────────────────────────────────────
    private void OnSubmit(string text)
    {
        if (_busy) return;
        string userText = text?.Trim();
        if (string.IsNullOrEmpty(userText)) return;

        // Replace previous Q/A with the new question.
        SetQuestion(userText);
        SetAnswer(thinkingText);

        // Clear and re-focus the input field so the user can keep typing.
        if (inputField != null)
        {
            inputField.text = string.Empty;
            inputField.ActivateInputField();
        }

        StartCoroutine(SendRequest(userText));
    }

    private IEnumerator SendRequest(string userText)
    {
        _busy = true;

        string body = BuildRequestJson(userText);
        byte[] payload = Encoding.UTF8.GetBytes(body);

        using var req = new UnityWebRequest(Endpoint, "POST");
        req.uploadHandler = new UploadHandlerRaw(payload);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", "Bearer " + _apiKey);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            SetAnswer($"[Error] {req.error}\n{req.downloadHandler.text}");
        }
        else
        {
            string reply = ParseReply(req.downloadHandler.text);
            SetAnswer(string.IsNullOrEmpty(reply) ? "[Error] Empty response from API." : reply);
        }

        _busy = false;
    }

    // ── UI helpers ────────────────────────────────────────────────────────────
    private void SetQuestion(string s) { if (questionField != null) questionField.text = s; }
    private void SetAnswer  (string s) { if (answerField   != null) answerField.text   = s; }

    // ── JSON build / parse (manual, no extra dependencies) ───────────────────
    private string BuildRequestJson(string userText)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append("\"model\":").Append(JsonString(model)).Append(',');
        sb.Append("\"temperature\":").Append(temperature.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
        sb.Append("\"max_tokens\":").Append(maxTokens).Append(',');
        sb.Append("\"messages\":[")
          .Append('{').Append("\"role\":\"system\",\"content\":").Append(JsonString(systemPrompt)).Append("},")
          .Append('{').Append("\"role\":\"user\",\"content\":").Append(JsonString(userText)).Append('}')
          .Append("]}");
        return sb.ToString();
    }

    private static string ParseReply(string json)
    {
        const string key = "\"content\":";
        int idx = json.IndexOf(key, StringComparison.Ordinal);
        if (idx < 0) return null;
        int q = json.IndexOf('"', idx + key.Length);
        if (q < 0) return null;
        var sb = new StringBuilder();
        for (int i = q + 1; i < json.Length; i++)
        {
            char c = json[i];
            if (c == '\\' && i + 1 < json.Length)
            {
                char n = json[++i];
                switch (n)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case '"': sb.Append('"');  break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/');  break;
                    case 'u':
                        if (i + 4 < json.Length &&
                            int.TryParse(json.Substring(i + 1, 4),
                                System.Globalization.NumberStyles.HexNumber,
                                System.Globalization.CultureInfo.InvariantCulture, out int code))
                        {
                            sb.Append((char)code);
                            i += 4;
                        }
                        break;
                    default: sb.Append(n); break;
                }
            }
            else if (c == '"') break;
            else sb.Append(c);
        }
        return sb.ToString();
    }

    private static string JsonString(string s)
    {
        if (s == null) return "\"\"";
        var sb = new StringBuilder(s.Length + 2);
        sb.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '"':  sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n");  break;
                case '\r': sb.Append("\\r");  break;
                case '\t': sb.Append("\\t");  break;
                default:
                    if (c < 0x20) sb.AppendFormat("\\u{0:X4}", (int)c);
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    // ── API key loading ───────────────────────────────────────────────────────
    private static string LoadApiKey()
    {
        string envKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(envKey)) return envKey.Trim();

        string path = Path.Combine(Application.streamingAssetsPath, "openai_key.txt");
        if (File.Exists(path))
        {
            string key = File.ReadAllText(path).Trim();
            if (!string.IsNullOrEmpty(key)) return key;
        }
        return null;
    }
}
