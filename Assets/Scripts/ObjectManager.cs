using UnityEngine;

public class ObjectManager : MonoBehaviour
{
    public Camera cam;
    public GameObject cube1;
    public GameObject cube2;

    void Update()
    {
        if (cam == null || cube1 == null || cube2 == null)
            return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            if (hit.collider.CompareTag("Interactable"))
            {
                if (Input.GetKey(KeyCode.X) || Input.GetKey(KeyCode.Y) || Input.GetKey(KeyCode.JoystickButton2))
                {
                    if (hit.collider.gameObject.name == cube1.name)
                        hit.collider.GetComponent<MoveCube>().Move();

                    if (hit.collider.gameObject.name == cube2.name)
                        hit.collider.GetComponent<RotateCube>().Rotate();
                }
            }
        }
    }
}
