using UnityEngine;

public class MoveCube : MonoBehaviour
{
    public float speed = 2f;

    public void Move()
    {
        transform.Translate(Vector3.right * speed * Time.deltaTime);
    }
}
