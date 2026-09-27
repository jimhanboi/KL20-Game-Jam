using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class LevelSelect : MonoBehaviour
{
    public Camera PlayerCamera;

    public float interactDistance = 3f;
    public string sceneToLoad;
    private bool isLookingAtObject = false;

    // Update is called once per frame
    void Update()
    {
        CheckIfLookedAt();

        if (isLookingAtObject && Mouse.current.leftButton.wasPressedThisFrame)
        {
            SelectLevel();
        }
    }

    void CheckIfLookedAt()
    {
        Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactDistance))
        {
            if (hit.transform == transform)
            {
                isLookingAtObject = true;
                return;
            }
        }

        isLookingAtObject = false;
    }

    void SelectLevel()
    {
        SceneManager.LoadScene(sceneToLoad);
    }
}