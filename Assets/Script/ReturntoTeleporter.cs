using UnityEngine;

public class ReturntoTeleporter : MonoBehaviour
{
    [SerializeField] Transform player;      // drag your player object here
    [SerializeField] Transform destination; // the teleporter (or a spot next to it)

    private void OnTriggerEnter(Collider other)
    {
        if (other.transform == player || other.transform.IsChildOf(player))
        {
            player.position = destination.position;
        }
    }
}