using UnityEngine;

public class DestroyAndSpawn : MonoBehaviour
{
    public Camera cam;
    GameObject savedObject;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.U) || Input.GetKeyDown(KeyCode.JoystickButton3))
        {
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                if (hit.collider.CompareTag("Interactable"))
                {
                    savedObject = hit.collider.gameObject;
                    savedObject.SetActive(false);
                }
                else if (hit.collider.CompareTag("Floor") && savedObject != null)
                {
                    savedObject.transform.position = hit.point + Vector3.up;
                    savedObject.SetActive(true);
                    savedObject = null;
                }
            }
        }
    }
}
