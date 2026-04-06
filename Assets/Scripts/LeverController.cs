using UnityEngine;
using System.Collections;

public class LeverController : MonoBehaviour
{
    public GameObject teleportObject; // Assign the teleport object in Inspector
    private Animator animator;
    private bool activated = false;

    void Start()
    {
        animator = GetComponent<Animator>();
        animator.speed = 0;

        // Hide the teleport object until the lever is pulled
        if (teleportObject != null)
            teleportObject.SetActive(false);
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

        // Wait until the clip finishes (normalizedTime reaches 1.0)
        while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            yield return null;

        // Freeze on the last frame
        animator.speed = 0;

        // Show the teleport object
        if (teleportObject != null)
            teleportObject.SetActive(true);
    }
}
