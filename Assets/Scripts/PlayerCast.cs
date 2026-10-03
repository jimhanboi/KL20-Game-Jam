using UnityEngine;

public class PlayerCast : MonoBehaviour
{
    [SerializeField] Camera PlayerCamera;
    public float interactDistance = 3f;
    [SerializeField] LayerMask shardLayer;
    LevelSelect _currentLookSelection;


    [SerializeField] GameObject leftEyeScreen, rightEyeScreen;

    // Update is called once per frame
    void Update()
    {
        LevelSelect target = null;

        if (!leftEyeScreen.activeSelf && !rightEyeScreen.activeSelf)
        {
            Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, shardLayer))
                target = hit.collider.GetComponentInParent<LevelSelect>();
        }

        if (target == _currentLookSelection) return;

        if (_currentLookSelection != null) _currentLookSelection.OnStopLook();
        if (target != null) target.OnLook();
        _currentLookSelection = target;
    }
}
