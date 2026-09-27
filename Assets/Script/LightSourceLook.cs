using UnityEngine;

public class LightSourceLook : MonoBehaviour
{
    public Camera playerCamera;
    public FlashDaze flashDaze;
    public float lookDistance = 15f;
    public float triggerCooldown = 3f; // prevents re-triggering every single frame while staring

    private float cooldownTimer = 0f;

    void Update()
    {
        cooldownTimer -= Time.deltaTime;
        CheckIfLookingAtLight();
    }

    void CheckIfLookingAtLight()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, lookDistance))
        {
            if (hit.transform == transform && cooldownTimer <= 0f)
            {
                flashDaze.TriggerDaze();    
                cooldownTimer = triggerCooldown;
            }
        }
    }
}