using UnityEngine;

public class ButtonDebug : MonoBehaviour
{
    string msg = "Press any button";

    void Update()
    {
        if (Input.anyKeyDown)
        {
            foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (Input.GetKeyDown(key))
                {
                    msg = key.ToString();
                }
            }
        }
    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 60;
        style.normal.textColor = Color.red;
        GUI.Label(new Rect(50, 50, 800, 100), msg, style);
    }
}
