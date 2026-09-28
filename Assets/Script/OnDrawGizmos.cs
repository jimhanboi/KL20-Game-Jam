using UnityEngine;

public class TargetGizmo : MonoBehaviour
{
    public Transform target;   // drag TargetA (or leave empty to use itself)

    void OnDrawGizmos()
    {
        if (target == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(target.position, target.position + target.forward * 2f);
        Gizmos.DrawWireSphere(target.position, 0.2f);
    }
}