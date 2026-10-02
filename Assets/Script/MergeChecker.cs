using UnityEngine;

[System.Serializable]
public class TargetPair
{
    public Transform[] rightTargets;
    public Transform[] leftTargets;
}



public class MergeChecker : MonoBehaviour
{
    public EyeAlignmentCheck Righteye;
    public EyeAlignmentCheck Lefteye;
    public TargetPair[] puzzles;
    public AudioSource CompletePuzzle;


    public float holdTime = 0.5f;
    public bool printPercent = false;   // tick this to also see live percentages every frame

    private int current = 0;
    private float timer = 0f;
    private bool allDone = false;

    private bool rightWasOk = false;
    private bool leftWasOk = false;

    void Start()
    {
        LoadPuzzle(0);
    }

    void Update()
    {
        if (allDone) return;

        // call both every frame so both percentages always update
        bool rightOk = Righteye.IsAligned();
        bool leftOk = Lefteye.IsAligned();

        // print once when an eye reaches its threshold
        if (rightOk && !rightWasOk)
        {
            Debug.Log("Right eye reached " + (Righteye.currentPercent * 100f).ToString("F0") + "% (aligned!)");
        }
        if (leftOk && !leftWasOk)
        {
            Debug.Log("Left eye reached " + (Lefteye.currentPercent * 100f).ToString("F0") + "% (aligned!)");
        }

        // print once when an eye falls back below it
        if (!rightOk && rightWasOk)
        {
            Debug.Log("Right eye lost alignment");
        }
        if (!leftOk && leftWasOk)
        {
            Debug.Log("Left eye lost alignment");
        }

        rightWasOk = rightOk;
        leftWasOk = leftOk;

        if (printPercent)
        {
            Debug.Log("Right eye: " + (Righteye.currentPercent * 100f).ToString("F0") + "%" +
                      " | Left eye: " + (Lefteye.currentPercent * 100f).ToString("F0") + "%");
        }

        if (rightOk && leftOk)
        {
            timer += Time.deltaTime;
            if (timer >= holdTime)
            {
                PuzzleSolved();
            }
        }
        else
        {
            timer = 0f;
        }
    }

    void LoadPuzzle(int index)
    {
        current = index;
        timer = 0f;
        rightWasOk = false;
        leftWasOk = false;
        Righteye.targets = puzzles[index].rightTargets;
        Lefteye.targets = puzzles[index].leftTargets;
        Debug.Log("Puzzle " + (index + 1) + " started");
    }

    void PuzzleSolved()
    {
        Debug.Log("Puzzle " + (current + 1) + " solved!");
        CompletePuzzle.Play();

        if (current + 1 >= puzzles.Length)
        {
            allDone = true;
            Debug.Log("You merged the puzzle!");
            CompletePuzzle.Play();
        }
        else
        {
            LoadPuzzle(current + 1);
        }
    }
}