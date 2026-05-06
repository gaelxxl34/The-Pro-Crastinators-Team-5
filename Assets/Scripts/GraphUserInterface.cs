using UnityEngine;

public class GraphUserInterface : MonoBehaviour
{
    public GameObject graph;
    private bool activated = false;
    public void Activate()
    {
        if (activated)
        {
            activated = false;
            graph.SetActive(false);
        }
        else
        {
            activated= true;
            graph.SetActive(true);
        }
    }
}
