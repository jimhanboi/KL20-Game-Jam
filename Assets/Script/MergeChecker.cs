using UnityEngine;

public class MergeChecker : MonoBehaviour
{
    public EyeAlignmentCheck eye1;
    public EyeAlignmentCheck eye2;

    public float holdTime = 0.5f;
    private float timer = 0f;

    public bool hasWon = false;

    void Update()
    {
        if (hasWon) return;

        Debug.Log("Eye1: " + eye1.currentPercent + " | Eye2: " + eye2.currentPercent +
                   " | Eye1 aligned: " + eye1.IsAligned() + " | Eye2 aligned: " + eye2.IsAligned());

        if (eye1.IsAligned() && eye2.IsAligned())
        {
            timer += Time.deltaTime;
            if (timer >= holdTime)
            {
                TriggerMerge();
            }
        }
        else
        {
            timer = 0f;
        }
    }

    void TriggerMerge()
    {
        hasWon = true;
        Debug.Log("You merged the puzzle!");
    }
}