using UnityEngine;
using UnityEngine.Events;


public class ClickableObject : MonoBehaviour
{
    [Tooltip("Fires when the user gazes at this object and presses the trigger button.")]
    public UnityEvent onClick;


    public void TriggerClick()
    {
        onClick?.Invoke();
    }
}