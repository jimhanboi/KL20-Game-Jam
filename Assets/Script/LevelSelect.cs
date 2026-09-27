using UnityEngine;

public class levelSelect : MonoBehaviour
{
    [Header("Bob Settings")]
    public float bobHeight = 0.2f;
    public float bobSpeed = 4f;

    [Header("Hover Detection")]
    public bool onlyWhenHovered = true;

    private Vector3 startPos;
    private bool isHovering = false;

    void Start()
    {
        startPos = transform.localPosition;
    }

    void Update()
    {
        if (!onlyWhenHovered || isHovering)
        {
            float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.localPosition = new Vector3(startPos.x, newY, startPos.z);
        }
        else
        {
            // smoothly return to rest position when not hovering
            transform.localPosition = Vector3.Lerp(transform.localPosition, startPos, Time.deltaTime * 8f);
        }
    }

    void OnMouseEnter()
    {
        isHovering = true;
    }

    void OnMouseExit()
    {
        isHovering = false;
    }
}