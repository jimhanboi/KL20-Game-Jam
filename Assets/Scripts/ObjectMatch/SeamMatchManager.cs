using System;
using UnityEngine;

public class SeamMatchManager : MonoBehaviour
{
    [SerializeField] PaneView leftPane;
    [SerializeField] PaneView rightPane;

    [SerializeField] float yTolerance = 0.08f;    // how close the two seams must be vertically
    [SerializeField] float minSizeRatio = 0.8f;   // 1 = identical apparent size, lower = more forgiving
    [SerializeField] float holdTime = 0.5f;       // seconds the match must stay valid

    [SerializeField] SeamPair[] pairs;

    public static event Action<SeamPair,float> OnMatched;

    [SerializeField] float transitionPeriod;

    bool activated;

    float holdTimer;

    void Update()
    {
        SeamPair matchedPair;
        bool foundMatch = TryFindMatch(out matchedPair);

        if (foundMatch)
        {
            holdTimer += Time.deltaTime;

            if (holdTimer >= holdTime)
            {
                if (!activated)
                {
                    OnMatched?.Invoke(matchedPair, transitionPeriod);
                    activated = true;
                }
            }
        }
        else
        {
            holdTimer = 0f;
        }
    }
    // Tries each pair in both orientations (A in the left pane with B in the right, or the reverse)
    // and returns true if any pair is framed by both panes with similar size and vertical alignment.
    bool TryFindMatch(out SeamPair matchedPair)
    {
        matchedPair = null;

        for (int i = 0; i < pairs.Length; i++)
        {
            SeamPair pair = pairs[i];

            bool aLeftBRight = TryOrientation(pair.pieceA, pair.pieceB);
            bool bLeftARight = TryOrientation(pair.pieceB, pair.pieceA);

            if (aLeftBRight || bLeftARight)
            {
                matchedPair = pair;
                return true;
            }
        }

        return false;
    }

    // Checks one orientation: onLeft is seen by the left pane, onRight by the right pane.
    // Both must be framed correctly, look a similar size, and sit at a similar height.
    bool TryOrientation(SeamPiece onLeft, SeamPiece onRight)
    {
        float sizeLeft;
        float yLeft;
        if (!leftPane.Frames(onLeft, out sizeLeft, out yLeft))
        {
            return false;
        }

        float sizeRight;
        float yRight;
        if (!rightPane.Frames(onRight, out sizeRight, out yRight))
        {
            return false;
        }

        float smaller = Mathf.Min(sizeLeft, sizeRight);
        float larger = Mathf.Max(sizeLeft, sizeRight);
        if (smaller / larger < minSizeRatio)
        {
            return false;
        }

        if (Mathf.Abs(yLeft - yRight) > yTolerance)
        {
            return false;
        }

        return true;
    }


}