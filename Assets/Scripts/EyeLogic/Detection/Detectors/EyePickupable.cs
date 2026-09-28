using System.Collections;
using UnityEngine;

public class EyePickupable : MonoBehaviour, IEyeDetect
{
    [SerializeField] float interactRange = 7f;

    [Header("Drop")]
    [SerializeField] float liftHeight = 1f;
    [SerializeField] float liftTime = 0.25f;

    Rigidbody rb;
    Coroutine dropRoutine;

    EyeControllerManager manager;


    public float GazeRange => interactRange;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (!rb) rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = true; // immovable by default
        rb.constraints = RigidbodyConstraints.FreezePositionX
                       | RigidbodyConstraints.FreezePositionZ
                       | RigidbodyConstraints.FreezeRotation;
    }

    public void LiftAndDrop()
    {
        if (dropRoutine != null) StopCoroutine(dropRoutine);
        dropRoutine = StartCoroutine(LiftAndDropRoutine());
    }

    IEnumerator LiftAndDropRoutine()
    {
        rb.isKinematic = true;

        // Lift
        Vector3 start = transform.position;
        Vector3 end = start + Vector3.up * liftHeight;
        for (float t = 0f; t < liftTime; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(start, end, t / liftTime);
            yield return null;
        }
        transform.position = end;

        // Fall
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero; // use rb.velocity on Unity 2022 or older

        yield return new WaitForSeconds(0.1f); // let it start moving before checking for rest
        while (rb.linearVelocity.sqrMagnitude > 0.01f)
            yield return new WaitForFixedUpdate();

        // Landed: lock it again
        rb.isKinematic = true;
        dropRoutine = null;
    }

    void Register(EyeDetection d)
    {
        manager = FindAnyObjectByType<EyeControllerManager>();
        Debug.Log(manager);
        if (manager) manager.AddCandidate(this, d);
    }

    void Unregister(EyeDetection d)
    {
        if (manager) manager.RemoveCandidate(this, d);
    }

    public void OnLeftEnter(EyeDetection d) 
    {
        Register(d);

       }
    public void OnLeftExit(EyeDetection d) => Unregister(d);
    public void OnRightEnter(EyeDetection d) => Register(d);
    public void OnRightExit(EyeDetection d) => Unregister(d);
}