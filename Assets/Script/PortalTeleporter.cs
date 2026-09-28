using UnityEngine;

public class PortalTeleporter : MonoBehaviour
{
    [SerializeField] Transform player;      // drag your player object here
    [SerializeField] Transform destination; // drag the target object here

    private void OnTriggerEnter(Collider other)
    {
        if (other.name.Contains("PortalCollider"))
        {
            player.position = destination.position;
        }
    }
}