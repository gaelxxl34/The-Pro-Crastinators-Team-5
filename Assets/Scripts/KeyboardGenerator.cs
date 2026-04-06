using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class KeyboardGenerator : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("Gap between keys in pixels")]
    public float keySpacing = 4f;

    [Tooltip("Subfolder inside Assets/Resources/ that holds the key sprites")]
    public string spritesPath = "KeyboardSprites";

    private readonly Dictionary<KeyCode, KeyButton> _keyMap =
        new Dictionary<KeyCode, KeyButton>();

    private struct KeyData
    {
        public string Label;
        public string SpriteName;
        public float Scale;
        public KeyCode Code;

        public KeyData(string label, string spriteName, KeyCode code, float scale = 1f)
        {
            Label = label;
            SpriteName = spriteName;
            Code = code;
            Scale = scale;
        }
    }

    private readonly KeyData[][] _layout = new KeyData[][]
    {
        // ── Row 0 · Number row ────────────────────────────────────────────────
        new KeyData[]
        {
            new KeyData("`",         "BackQuote",   KeyCode.BackQuote             ),
            new KeyData("1",         "Alpha1",      KeyCode.Alpha1                ),
            new KeyData("2",         "Alpha2",      KeyCode.Alpha2                ),
            new KeyData("3",         "Alpha3",      KeyCode.Alpha3                ),
            new KeyData("4",         "Alpha4",      KeyCode.Alpha4                ),
            new KeyData("5",         "Alpha5",      KeyCode.Alpha5                ),
            new KeyData("6",         "Alpha6",      KeyCode.Alpha6                ),
            new KeyData("7",         "Alpha7",      KeyCode.Alpha7                ),
            new KeyData("8",         "Alpha8",      KeyCode.Alpha8                ),
            new KeyData("9",         "Alpha9",      KeyCode.Alpha9                ),
            new KeyData("0",         "Alpha0",      KeyCode.Alpha0                ),
            new KeyData("-",         "Minus",       KeyCode.Minus                 ),
            new KeyData("=",         "Equals",      KeyCode.Equals                ),
            new KeyData("Backspace", "Backspace",   KeyCode.Backspace,        2f  ),
        },

        // ── Row 1 · QWERTY row ───────────────────────────────────────────────
        new KeyData[]
        {
            new KeyData("Tab",  "Tab",         KeyCode.Tab,              2f  ),
            new KeyData("Q",    "Q",           KeyCode.Q                     ),
            new KeyData("W",    "W",           KeyCode.W                     ),
            new KeyData("E",    "E",           KeyCode.E                     ),
            new KeyData("R",    "R",           KeyCode.R                     ),
            new KeyData("T",    "T",           KeyCode.T                     ),
            new KeyData("Y",    "Y",           KeyCode.Y                     ),
            new KeyData("U",    "U",           KeyCode.U                     ),
            new KeyData("I",    "I",           KeyCode.I                     ),
            new KeyData("O",    "O",           KeyCode.O                     ),
            new KeyData("P",    "P",           KeyCode.P                     ),
            new KeyData("[",    "LeftBracket", KeyCode.LeftBracket            ),
            new KeyData("]",    "RightBracket",KeyCode.RightBracket           ),
            new KeyData("\\",   "Backslash",   KeyCode.Backslash,        2f  ),
        },

        // ── Row 2 · ASDF row ─────────────────────────────────────────────────
        new KeyData[]
        {
            new KeyData("Caps Lock", "CapsLock",  KeyCode.CapsLock,      2f  ),
            new KeyData("A",         "A",          KeyCode.A                  ),
            new KeyData("S",         "S",          KeyCode.S                  ),
            new KeyData("D",         "D",          KeyCode.D                  ),
            new KeyData("F",         "F",          KeyCode.F                  ),
            new KeyData("G",         "G",          KeyCode.G                  ),
            new KeyData("H",         "H",          KeyCode.H                  ),
            new KeyData("J",         "J",          KeyCode.J                  ),
            new KeyData("K",         "K",          KeyCode.K                  ),
            new KeyData("L",         "L",          KeyCode.L                  ),
            new KeyData(";",         "Semicolon",  KeyCode.Semicolon          ),
            new KeyData("'",         "Quote",      KeyCode.Quote              ),
            new KeyData("Enter",     "Return",     KeyCode.Return,        2f  ),
        },

        // ── Row 3 · ZXCV row ─────────────────────────────────────────────────
        new KeyData[]
        {
            new KeyData("Left Shift",  "LeftShift",  KeyCode.LeftShift,   2f  ),
            new KeyData("Z",           "Z",           KeyCode.Z                ),
            new KeyData("X",           "X",           KeyCode.X                ),
            new KeyData("C",           "C",           KeyCode.C                ),
            new KeyData("V",           "V",           KeyCode.V                ),
            new KeyData("B",           "B",           KeyCode.B                ),
            new KeyData("N",           "N",           KeyCode.N                ),
            new KeyData("M",           "M",           KeyCode.M                ),
            new KeyData(",",           "Comma",       KeyCode.Comma            ),
            new KeyData(".",           "Period",      KeyCode.Period           ),
            new KeyData("/",           "Slash",       KeyCode.Slash            ),
            new KeyData("Right Shift", "RightShift",  KeyCode.RightShift,  2f  ),
        },

        // ── Row 4 · Space bar row ────────────────────────────────────────────
        new KeyData[]
        {
            new KeyData("Left Ctrl",  "LeftControl",  KeyCode.LeftControl      ),
            new KeyData("Left Win",   "LeftWindows",  KeyCode.LeftWindows      ),
            new KeyData("Left Alt",   "LeftAlt",      KeyCode.LeftAlt          ),
            new KeyData("Space",      "Space",        KeyCode.Space,       2f  ),
            new KeyData("Right Alt",  "RightAlt",     KeyCode.RightAlt         ),
            new KeyData("Right Win",  "RightWindows", KeyCode.RightWindows     ),
            new KeyData("Menu",       "Menu",         KeyCode.Menu             ),
            new KeyData("Right Ctrl", "RightControl", KeyCode.RightControl     ),
        },
    };

    private void Start()
    {
        GenerateKeyboard();
    }

    private void Update()
    {
        foreach (var pair in _keyMap)
            pair.Value.SetPressed(Input.GetKey(pair.Key));
    }

    private void GenerateKeyboard()
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null)
        {
            Debug.LogError("[KeyboardGenerator] No RectTransform — is this on a UI Panel?");
            return;
        }

        float panelWidth = rt.rect.width;
        float panelHeight = rt.rect.height;
        Debug.Log($"[KeyboardGenerator] Panel: {panelWidth} x {panelHeight}");

        if (panelWidth <= 0 || panelHeight <= 0)
        {
            Debug.LogError("[KeyboardGenerator] Panel size is zero. Try calling GenerateKeyboard from a coroutine after the first frame.");
            return;
        }

        int numRows = _layout.Length;
        float rowHeight = (panelHeight - (numRows - 1) * keySpacing) / numRows;

        float unitWidth = float.MaxValue;
        foreach (var row in _layout)
        {
            float units = 0f;
            foreach (var k in row) units += k.Scale;
            float candidate = (panelWidth - (row.Length - 1) * keySpacing) / units;
            if (candidate < unitWidth) unitWidth = candidate;
        }
        Debug.Log($"[KeyboardGenerator] unitWidth={unitWidth:F1}  rowHeight={rowHeight:F1}");

        int total = 0;
        for (int rowIndex = 0; rowIndex < numRows; rowIndex++)
        {
            float cursorX = 0f;
            float y = -(rowIndex * (rowHeight + keySpacing));

            foreach (var key in _layout[rowIndex])
            {
                float keyWidth = unitWidth * key.Scale;

                var sprite = Resources.Load<Sprite>($"{spritesPath}/{key.SpriteName}");
                if (sprite == null)
                    Debug.LogWarning($"[KeyboardGenerator] Missing sprite: Assets/Resources/{spritesPath}/{key.SpriteName}.png");

                var keyObj = new GameObject($"Key_{key.SpriteName}");
                keyObj.transform.SetParent(transform, false);

                var keyRt = keyObj.AddComponent<RectTransform>();
                keyRt.anchorMin = new Vector2(0f, 1f);
                keyRt.anchorMax = new Vector2(0f, 1f);
                keyRt.pivot = new Vector2(0f, 1f);
                keyRt.sizeDelta = new Vector2(keyWidth, rowHeight);
                keyRt.anchoredPosition = new Vector2(cursorX, y);

                var img = keyObj.AddComponent<Image>();
                img.sprite = sprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = false;

                var btn = keyObj.AddComponent<KeyButton>();
                btn.Init(key.Label, key.SpriteName);

                if (!_keyMap.ContainsKey(key.Code))
                    _keyMap[key.Code] = btn;
                else
                    Debug.LogWarning($"[KeyboardGenerator] Duplicate KeyCode {key.Code} for '{key.Label}' — skipping.");

                Debug.Log($"[KeyboardGenerator] Key '{key.Label}' → KeyCode.{key.Code} | pos ({cursorX:F1},{y:F1}) size ({keyWidth:F1}x{rowHeight:F1})");

                cursorX += keyWidth + keySpacing;
                total++;
            }
        }

        Debug.Log($"[KeyboardGenerator] Done — {total} keys, {_keyMap.Count} KeyCode mappings.");
    }
}