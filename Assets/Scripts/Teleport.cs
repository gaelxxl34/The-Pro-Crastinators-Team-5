using UnityEngine;

public class Teleport : MonoBehaviour
{
    public Camera cam;
    public GameObject player;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetKeyDown(KeyCode.JoystickButton10))
        {
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                if (hit.collider.CompareTag("Floor"))
                {
                    Vector3 pos = hit.point;
                    pos.y = player.transform.position.y;

                    CharacterController cc = player.GetComponent<CharacterController>();
                    cc.enabled = false;
                    player.transform.position = pos;
                    cc.enabled = true;
                }
            }
        }
    }
}
