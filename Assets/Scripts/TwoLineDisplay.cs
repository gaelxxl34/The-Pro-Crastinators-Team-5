using System.Collections;
using System.IO;
using UnityEngine;
using TMPro;

public class TwoLineDisplay : MonoBehaviour
{
    [Tooltip("Drag your TMP_InputField here")]
    public TMP_InputField inputField;

    private TMP_Text _textComponent;
    private bool _isTrimming;
    private string _logFilePath;

    private void Start()
    {
        if (inputField == null)
        {
            Debug.LogError("[TwoLineDisplay] No TMP_InputField assigned.");
            return;
        }

        _textComponent = inputField.textComponent;
        _logFilePath = Path.Combine(Application.dataPath, "Papers", "paper.txt");

        inputField.onValueChanged.AddListener(OnValueChanged);
        inputField.ActivateInputField();
    }

    private void OnDestroy()
    {
        if (inputField != null)
            inputField.onValueChanged.RemoveListener(OnValueChanged);
    }

    private void OnValueChanged(string _)
    {
        if (_isTrimming) return;
        StartCoroutine(TrimAfterLayout());
    }

    private IEnumerator TrimAfterLayout()
    {
        yield return null;

        _textComponent.ForceMeshUpdate();

        if (_textComponent.textInfo.lineCount <= 2) yield break;

        _isTrimming = true;

        while (_textComponent.textInfo.lineCount > 2)
        {
            int trimTo = _textComponent.textInfo.lineInfo[1].firstCharacterIndex;

            if (trimTo <= 0) break;

            string trimmedLine = inputField.text[..trimTo];
            AppendToFile(trimmedLine);

            inputField.text = inputField.text[trimTo..];
            _textComponent.ForceMeshUpdate();
        }

        inputField.caretPosition = inputField.text.Length;
        inputField.selectionFocusPosition = inputField.text.Length;
        inputField.ActivateInputField();

        _isTrimming = false;
    }

    private void AppendToFile(string line)
    {
        try
        {
            File.AppendAllText(_logFilePath, line);
            Debug.Log($"[TwoLineDisplay] Appended to file: '{line}'");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TwoLineDisplay] Failed to write to file: {e.Message}");
        }
    }
}