using UnityEngine;

public class RotateCube : MonoBehaviour
{
    public float speed = 50f;

    public void Rotate()
    {
        transform.Rotate(Vector3.up, speed * Time.deltaTime);
    }
}
