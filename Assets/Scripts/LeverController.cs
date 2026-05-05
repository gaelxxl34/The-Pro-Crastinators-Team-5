using UnityEngine;
using System.Collections;

public class LeverController : MonoBehaviour
{
    [Tooltip("Assign the TeleportMenuController from the scene")]
    public TeleportMenuController teleportMenu;

    private Animator animator;
    private bool activated = false;

    void Start()
    {
        animator = GetComponent<Animator>();
        animator.speed = 0;
    }

    public void Activate()
    {
        if (activated) return;
        activated = true;
        animator.speed = 1;
        animator.Rebind();
        StartCoroutine(WaitForAnimationToFinish());
    }

    IEnumerator WaitForAnimationToFinish()
    {
        // Wait two frames for the animator to start playing
        yield return null;
        yield return null;

        // Wait until the clip finishes (normalizedTime reaches 1.0), with 5s timeout
        float elapsed = 0f;
        while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f && elapsed < 5f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Freeze on the last frame
        animator.speed = 0;

        // Open the destination selection menu
        if (teleportMenu != null)
            teleportMenu.OpenMenu();
    }
}
