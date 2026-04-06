using UnityEngine;

public class HighlightObject : MonoBehaviour
{
    private Outline outline;

    void Awake()
    {
        outline = GetComponent<Outline>();
        if (outline == null)
            outline = gameObject.AddComponent<Outline>();

        // outline.OutlineColor = Color.yellow;
        outline.OutlineWidth = 5f;
        outline.enabled = false;
    }

    public void Highlight()
    {
        outline.enabled = true;
    }

    public void Unhighlight()
    {
        outline.enabled = false;
    }
}
