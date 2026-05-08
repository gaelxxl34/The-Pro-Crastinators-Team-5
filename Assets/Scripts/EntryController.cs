using UnityEngine;
using TMPro;

public class EntryController : MonoBehaviour
{
    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(0.2f, 0.6f, 1.0f, 1f);
    [SerializeField] private Color hoveredColor = Color.yellow;

    public string CharacterName { get; private set; }
    public bool IsSelected { get; private set; } = false;

    private CharacterLineChart _chart;
    private TextMeshProUGUI _label;

    public void Setup(string characterName, int count, CharacterLineChart chart)
    {
        CharacterName = characterName;
        _chart = chart;

        _label = GetComponentInChildren<TextMeshProUGUI>();

        if (_label != null)
        {
            _label.text = $"{characterName}: {count}";
            _label.color = normalColor;
        }
        else
            Debug.LogWarning($"[EntryController] No TMP label found on entry: {characterName}");

        StartCoroutine(SetupColliderNextFrame());
    }

    private System.Collections.IEnumerator SetupColliderNextFrame()
    {
        yield return null;
        yield return null;

        BoxCollider col = GetComponent<BoxCollider>();
        RectTransform rt = GetComponent<RectTransform>();

        Debug.Log($"[EntryController] {CharacterName} — rect:{rt?.rect} collider:{col?.size} position:{transform.position}");
    }

    public void OnHover()
    {
        if (!IsSelected && _label != null)
            _label.color = hoveredColor;
    }

    public void OnHoverExit()
    {
        if (_label != null)
            _label.color = IsSelected ? selectedColor : normalColor;
    }

    public void OnSelect()
    {
        IsSelected = true;
        if (_label != null) _label.color = selectedColor;
        _chart?.DisplayCharacter(CharacterName);
    }

    public void Deselect()
    {
        IsSelected = false;
        if (_label != null) _label.color = normalColor;
    }
}