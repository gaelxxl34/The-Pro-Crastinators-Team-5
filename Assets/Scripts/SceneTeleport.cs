using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTeleport : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !string.IsNullOrEmpty(TeleportMenuController.ChosenScene))
        {
            TeleportMenuController.TeleportNow();
        }
    }
}
