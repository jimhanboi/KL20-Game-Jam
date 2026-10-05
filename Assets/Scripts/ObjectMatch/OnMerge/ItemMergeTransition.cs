using DG.Tweening;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class ItemMergeTransition : MonoBehaviour
{
    [SerializeField] EyeRotation leftRotate, rightRotate;
    [SerializeField] EyeFreeRoamNonVert leftMove, rightMove;
    [SerializeField] PaneView leftPane, rightPane;
    [SerializeField] Ease ease = Ease.InOutSine;

    [SerializeField] LayerMask wallLayers = ~0;
    [SerializeField] float eyeRadius = 0.3f;
    [SerializeField] float wallSkin = 0.05f;
    [SerializeField] bool alignFacing = false;
    [SerializeField] float maxYawCorrection = 20f;

    [SerializeField] float maxPitchCorrection = 15f;

    Rigidbody[] bodies;
    bool[] wasKinematic;


    Sequence mergeSequence;
    private Behaviour[] lockedScripts;
    private bool[] wasEnabled;

    void OnEnable()
    {
        SeamMatchManager.OnMatched += Merge;
    }

    void OnDisable()
    {
        SeamMatchManager.OnMatched -= Merge;
        mergeSequence?.Kill();
    }

    void Awake()
    {
        lockedScripts = new Behaviour[] { leftRotate, rightRotate, leftMove, rightMove };
        wasEnabled = new bool[lockedScripts.Length];

        bodies = new Rigidbody[]
        {
        leftMove.GetComponent<Rigidbody>(),
        rightMove.GetComponent<Rigidbody>()
        };
        wasKinematic = new bool[bodies.Length];
    }

    // Locks player control, works out which half each pane is framing, then tweens both eyes into place.
    void Merge(SeamPair pair, float overSec)
    {
        EyeMovementSet(false);

        SeamPiece leftPiece;
        SeamPiece rightPiece;
        FindPieces(pair, out leftPiece, out rightPiece);

        mergeSequence?.Kill();
        mergeSequence = DOTween.Sequence();
        mergeSequence.SetLink(gameObject);
        float leftSize;
        float leftY;
        float rightSize;
        float rightY;
        leftPane.Frames(leftPiece, out leftSize, out leftY);
        rightPane.Frames(rightPiece, out rightSize, out rightY);

        float targetY = (leftY + rightY) * 0.5f;
        float targetSize = Mathf.Sqrt(leftSize * rightSize);

        AddEyeTween(mergeSequence, leftMove.transform, leftPane, leftPiece, targetY, targetSize, overSec);
        AddEyeTween(mergeSequence, rightMove.transform, rightPane, rightPiece, targetY, targetSize, overSec);
    }

    void EyeMovementSet(bool set)
    {
        leftRotate.CanRotate = set;
        rightRotate.CanRotate = set;
        leftMove.CanMove = set;
        rightMove.CanMove = set;
    }

    // Decides which half the left pane is looking at. The other half belongs to the right pane.
    void FindPieces(SeamPair pair, out SeamPiece leftPiece, out SeamPiece rightPiece)
    {
        float size;
        float y;

        if (leftPane.Frames(pair.pieceA, out size, out y))
        {
            leftPiece = pair.pieceA;
            rightPiece = pair.pieceB;
        }
        else
        {
            leftPiece = pair.pieceB;
            rightPiece = pair.pieceA;
        }
    }

    // Moves the eye to fix the apparent size, then rotates it about the camera so the seam anchor
    // lands exactly on the pane's seam edge at targetY. The camera position doesn't change during the rotation.
    void AddEyeTween(Sequence sequence, Transform eye, PaneView pane, SeamPiece piece, float targetY, float targetSize, float duration)
    {
        Camera cam = pane.cam;
        Vector3 anchor = piece.seamAnchor.position;

        Vector3 camPos = cam.transform.position;
        Quaternion camRot = cam.transform.rotation;

        Vector3 viewport = cam.WorldToViewportPoint(anchor);
        float depth = viewport.z;
        float currentSize = pane.GetSize(piece, depth);

        Vector3 move = GetSizeMove(camPos, anchor, depth, currentSize, targetSize, cam.nearClipPlane);
        move = ClampToWalls(eye.position, move);
        Vector3 newCamPos = camPos + move;

        float edgeX = 0f;
        if (pane.seamIsOnRight)
        {
            edgeX = 1f;
        }

        float tanHalfV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float tanHalfH = tanHalfV * cam.aspect;

        Vector3 wantedLocal = new Vector3((edgeX - 0.5f) * 2f * tanHalfH, (targetY - 0.5f) * 2f * tanHalfV, 1f).normalized;
        Vector3 worldDirection = (anchor - newCamPos).normalized;
        Vector3 currentLocal = Quaternion.Inverse(camRot) * worldDirection;

        Quaternion newCamRot = camRot * Quaternion.FromToRotation(wantedLocal, currentLocal);
        Quaternion worldDelta = newCamRot * Quaternion.Inverse(camRot);

        Vector3 startPosition = eye.position;
        Quaternion startRotation = eye.rotation;
        Quaternion targetRotation = worldDelta * startRotation;
        Vector3 targetPosition = newCamPos + worldDelta * (startPosition - camPos);

        Rigidbody body = eye.GetComponent<Rigidbody>();
        float progress = 0f;

        Tween tween = DOTween.To(
            () => progress,
            value =>
            {
                progress = value;
                Vector3 position = Vector3.Lerp(startPosition, targetPosition, value);
                Quaternion rotation = Quaternion.Slerp(startRotation, targetRotation, value);

                if (body != null)
                {
                    body.MovePosition(position);
                    body.MoveRotation(rotation);
                }
                else
                {
                    eye.SetPositionAndRotation(position, rotation);
                }
            },
            1f,
            duration).SetEase(ease);

        sequence.Join(tween);
    }

    // Returns a horizontal move toward or away from the anchor that gives the piece the wanted apparent size.
    Vector3 GetSizeMove(Vector3 camPos, Vector3 anchor, float depth, float currentSize, float targetSize, float nearClip)
    {
        Vector3 flatToAnchor = Flatten(anchor - camPos, false);
        float flatLength = flatToAnchor.magnitude;

        if (flatLength < 0.0001f || targetSize < 0.0001f || currentSize < 0.0001f)
        {
            return Vector3.zero;
        }

        float newDepth = depth * currentSize / targetSize;
        float distanceChange = depth - newDepth;
        distanceChange = Mathf.Min(distanceChange, flatLength - (nearClip + 0.5f));

        return flatToAnchor / flatLength * distanceChange;
    }

    // Shortens a move so the eye's sphere doesn't pass through walls.
    Vector3 ClampToWalls(Vector3 start, Vector3 move)
    {
        float distance = move.magnitude;
        if (distance < 0.001f)
        {
            return move;
        }

        Vector3 direction = move / distance;
        RaycastHit hit;
        if (Physics.SphereCast(start, eyeRadius, direction, out hit, distance, wallLayers, QueryTriggerInteraction.Ignore))
        {
            float safeDistance = Mathf.Max(0f, hit.distance - wallSkin);
            return direction * safeDistance;
        }

        return move;
    }




    // Removes the vertical part of a direction so only the left/right turn counts.
    Vector3 Flatten(Vector3 direction, bool normalize = true)
    {
        direction.y = 0f;
        if (normalize)
        {
            return direction.normalized;
        }
        return direction;
    }

    // Stops the body's current motion and makes it kinematic so only the tween moves it.
    void FreezeBodies()
    {
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] == null)
            {
                continue;
            }

            wasKinematic[i] = bodies[i].isKinematic;

            if (!bodies[i].isKinematic)
            {
                bodies[i].linearVelocity = Vector3.zero;   // use .velocity on versions before Unity 6
                bodies[i].angularVelocity = Vector3.zero;
            }

            bodies[i].isKinematic = true;
        }
    }

    // Puts each body back to the kinematic state it had before the merge.
    void ReleaseBodies()
    {
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] != null)
            {
                bodies[i].isKinematic = wasKinematic[i];
            }
        }
    }

    // Returns the eye rotation with a small yaw correction so the cut face points at the seam edge.
    // The correction is capped so the eye never turns far.
    Quaternion GetFacingRotation(Transform eye, PaneView pane, SeamPiece piece)
    {
        Vector3 up = Vector3.up;
        Vector3 faceDirection = Flatten(piece.seamAnchor.forward);

        Vector3 wantedRight = faceDirection;
        if (!pane.seamIsOnRight)
        {
            wantedRight = -faceDirection;
        }

        Vector3 wantedForward = Vector3.Cross(wantedRight, up);
        Vector3 currentForward = Flatten(pane.cam.transform.forward);

        float yawDelta = Vector3.SignedAngle(currentForward, wantedForward, up);
        yawDelta = Mathf.Clamp(yawDelta, -maxYawCorrection, maxYawCorrection);

        return Quaternion.AngleAxis(yawDelta, up) * eye.rotation;
    }

    // Works out how far the eye must slide sideways for the anchor to sit on the seam edge,
    // then shortens that move if a wall is in the way.
    Vector3 GetTargetPosition(Transform eye, PaneView pane, SeamPiece piece, Quaternion targetRotation)
    {
        Camera cam = pane.cam;
        Vector3 savedPosition = eye.position;
        Quaternion savedRotation = eye.rotation;

        eye.rotation = targetRotation;

        Vector3 viewportPos = cam.WorldToViewportPoint(piece.seamAnchor.position);
        Vector3 flatRight = Flatten(cam.transform.right);

        float halfHeight = cam.orthographicSize;
        if (!cam.orthographic)
        {
            halfHeight = viewportPos.z * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }
        float fullWidth = 2f * halfHeight * cam.aspect;

        float edgeX = 0f;
        if (pane.seamIsOnRight)
        {
            edgeX = 1f;
        }

        eye.rotation = savedRotation;

        Vector3 move = flatRight * (viewportPos.x - edgeX) * fullWidth;
        Vector3 target = savedPosition + move;
        target.y = savedPosition.y;

        Vector3 toTarget = target - savedPosition;
        float distance = toTarget.magnitude;

        if (distance > 0.001f)
        {
            Vector3 direction = toTarget / distance;
            RaycastHit hit;
            if (Physics.SphereCast(savedPosition, eyeRadius, direction, out hit, distance, wallLayers, QueryTriggerInteraction.Ignore))
            {
                float safeDistance = Mathf.Max(0f, hit.distance - wallSkin);
                target = savedPosition + direction * safeDistance;
            }
        }

        return target;
    }

}