using UnityEngine;

public class HitDetection : MonoBehaviour
{

    [SerializeField] EyeFreeRoamNonVert freeRoam;
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log(other.name);
        PlayerKill kill;
        kill = other.GetComponentInParent<PlayerKill>();
        if (kill != null)
        {
            freeRoam.Die();
        }
    }
}
