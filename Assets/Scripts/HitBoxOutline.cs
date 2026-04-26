using UnityEngine;

public class HitBoxOutline : MonoBehaviour
{
    [Tooltip("The HitBox collider to raycast against")]
    public Collider hitBox;

    [Tooltip("The 'default' GameObject that carries the Unity Outline component")]
    public Outline outline;

    [Tooltip("Interface Panel to turn on and off")]
    public Canvas canvas;

    private Camera _cam;
    private bool _isHighlighted;

    private void Start()
    {
        //canvas.enabled = false;
    }

    private void Update()
    {

    }
}