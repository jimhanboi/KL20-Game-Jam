using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SeamHintRenderer : MonoBehaviour
{
    [SerializeField] PaneView leftPane;
    [SerializeField] PaneView rightPane;
    [SerializeField] RawImage leftOverlay;
    [SerializeField] RawImage rightOverlay;
    [SerializeField] SeamPair[] pairs;

    [SerializeField] Material flatMaterial;
    [SerializeField] Material hintUIMaterial;

    [SerializeField] string leftLayerName = "PaneFX_Left";
    [SerializeField] string rightLayerName = "PaneFX_Right";

    [SerializeField] Color outlineColor = new Color(1f, 0.9f, 0.2f, 1f);
    [SerializeField] Color ghostColor = new Color(0.3f, 0.9f, 1f, 1f);
    [SerializeField] float outlineFill = 0f;
    [SerializeField] float ghostFill = 0.35f;
    [SerializeField] bool showGhost = true;

    class PieceData
    {
        public SeamPiece piece;
        public SeamPair pair;
        public List<GameObject> maskCopies = new List<GameObject>();
        public GameObject[] ghosts = new GameObject[2];
        public int maskPane = -1;
        public bool[] ghostWanted = new bool[2];
        public MeshFilter[] sourceFilters;

    }

    List<PieceData> allData = new List<PieceData>();
    PaneView[] panes;
    RawImage[] overlays;
    Camera[] maskCams = new Camera[2];
    RenderTexture[] maskTextures = new RenderTexture[2];
    Material[] overlayMaterials = new Material[2];
    int[] layers = new int[2];

    PieceData[] framed = new PieceData[2];
    PieceData[] ghostPiece = new PieceData[2];
    int[] ghostSourcePane = new int[2];

    // Looks up the FX layers, creates one mask camera, texture and overlay material per pane,
    // then builds the hidden mask copies and ghosts for every piece once.
    void Start()
    {
        panes = new PaneView[] { leftPane, rightPane };
        overlays = new RawImage[] { leftOverlay, rightOverlay };

        layers[0] = LayerMask.NameToLayer(leftLayerName);
        layers[1] = LayerMask.NameToLayer(rightLayerName);

        if (layers[0] < 0 || layers[1] < 0)
        {
            Debug.LogError("SeamHintRenderer: FX layers not found. Add them in Project Settings > Tags and Layers.");
            enabled = false;
            return;
        }

        for (int p = 0; p < 2; p++)
        {
            CreatePaneResources(p);
        }

        for (int i = 0; i < pairs.Length; i++)
        {
            allData.Add(BuildPieceData(pairs[i].pieceA, pairs[i]));
            allData.Add(BuildPieceData(pairs[i].pieceB, pairs[i]));
        }
    }

    // Runs after the eyes have moved this frame: finds what each pane frames, decides what to draw, draws it.
    void LateUpdate()
    {
        FindFramedPieces();
        AssignWork();
        ApplyWork();
    }

    // Hides everything when this component is turned off (for example after a match).
    void OnDisable()
    {
        HideEverything();
    }

    // Frees the mask textures.
    void OnDestroy()
    {
        for (int p = 0; p < 2; p++)
        {
            if (maskTextures[p] != null)
            {
                maskTextures[p].Release();
            }
        }
    }

    // Creates the mask texture (same size as the pane's own texture), the hidden mask camera,
    // and the overlay's own material instance for one pane.
    void CreatePaneResources(int p)
    {
        int width = 1024;
        int height = 1024;

        RenderTexture paneTexture = panes[p].cam.targetTexture;
        if (paneTexture != null)
        {
            width = paneTexture.width;
            height = paneTexture.height;
        }

        RenderTexture texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        texture.antiAliasing = 1;
        texture.Create();
        maskTextures[p] = texture;

        GameObject camObject = new GameObject("SeamMaskCamera_" + p);
        camObject.transform.SetParent(transform, false);
        Camera maskCam = camObject.AddComponent<Camera>();
        maskCam.enabled = false;
        maskCams[p] = maskCam;

        overlayMaterials[p] = new Material(hintUIMaterial);
        overlays[p].material = overlayMaterials[p];
        overlays[p].texture = texture;
        overlays[p].raycastTarget = false;
        overlays[p].enabled = false;
    }

    // Builds the ghosts first, then the mask copies, so the ghosts never copy a mask copy.
    // Stores the piece's original meshes, then builds its mask copies. Ghosts are built later, on demand.
    PieceData BuildPieceData(SeamPiece piece, SeamPair pair)
    {
        PieceData data = new PieceData();
        data.piece = piece;
        data.pair = pair;
        data.sourceFilters = piece.GetComponentsInChildren<MeshFilter>();

        BuildMaskCopies(data);

        return data;
    }

    // Adds a hidden flat-white copy under every mesh of the piece, so it follows the real mesh exactly.
    void BuildMaskCopies(PieceData data)
    {
        MeshFilter[] filters = data.sourceFilters;
        for (int i = 0; i < filters.Length; i++)
        {
            MeshRenderer source = filters[i].GetComponent<MeshRenderer>();
            if (source == null || !source.enabled || filters[i].sharedMesh == null)
            {
                continue;
            }

            GameObject copy = new GameObject("MaskCopy");
            copy.layer = layers[0];
            copy.transform.SetParent(filters[i].transform, false);

            MeshFilter copyFilter = copy.AddComponent<MeshFilter>();
            copyFilter.sharedMesh = filters[i].sharedMesh;

            MeshRenderer copyRenderer = copy.AddComponent<MeshRenderer>();
            copyRenderer.sharedMaterials = MakeFlatMaterials(filters[i].sharedMesh);
            copyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            copyRenderer.receiveShadows = false;

            copy.SetActive(false);
            data.maskCopies.Add(copy);
        }
    }

    // Builds a hidden flat copy of the piece on its own root, ready to be moved to the assembled pose.
    GameObject BuildGhost(PieceData data, int layer)
    {
        SeamPiece piece = data.piece;

        GameObject root = new GameObject("Ghost_" + piece.name);
        root.layer = layer;
        root.transform.localScale = piece.transform.lossyScale;

        MeshFilter[] filters = data.sourceFilters;

        for (int i = 0; i < filters.Length; i++)
        {
            MeshRenderer source = filters[i].GetComponent<MeshRenderer>();
            if (source == null || !source.enabled || filters[i].sharedMesh == null)
            {
                continue;
            }

            GameObject part = new GameObject("GhostPart");
            part.layer = layer;
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = piece.transform.InverseTransformPoint(filters[i].transform.position);
            part.transform.localRotation = Quaternion.Inverse(piece.transform.rotation) * filters[i].transform.rotation;
            part.transform.localScale = DivideScale(filters[i].transform.lossyScale, piece.transform.lossyScale);

            MeshFilter partFilter = part.AddComponent<MeshFilter>();
            partFilter.sharedMesh = filters[i].sharedMesh;

            MeshRenderer partRenderer = part.AddComponent<MeshRenderer>();
            partRenderer.sharedMaterials = MakeFlatMaterials(filters[i].sharedMesh);
            partRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            partRenderer.receiveShadows = false;
        }

        root.SetActive(false);
        return root;
    }


    // Returns the piece's ghost for this pane, building it only if it doesn't exist yet.
    GameObject GetGhost(PieceData data, int p)
    {
        if (data.ghosts[p] == null)
        {
            data.ghosts[p] = BuildGhost(data, layers[p]);
        }

        return data.ghosts[p];
    }

    // Returns one flat material per submesh so every part of the mesh is drawn.
    Material[] MakeFlatMaterials(Mesh mesh)
    {
        int count = Mathf.Max(1, mesh.subMeshCount);
        Material[] materials = new Material[count];

        for (int i = 0; i < count; i++)
        {
            materials[i] = flatMaterial;
        }

        return materials;
    }

    // Divides one scale by another per axis, ignoring axes where the divisor is zero.
    Vector3 DivideScale(Vector3 a, Vector3 b)
    {
        return new Vector3(SafeDivide(a.x, b.x), SafeDivide(a.y, b.y), SafeDivide(a.z, b.z));
    }

    // Divides a by b, or returns a unchanged if b is almost zero.
    float SafeDivide(float a, float b)
    {
        if (Mathf.Abs(b) < 0.00001f)
        {
            return a;
        }

        return a / b;
    }

    // Asks each pane which piece (if any) it is currently framing correctly.
    void FindFramedPieces()
    {
        for (int p = 0; p < 2; p++)
        {
            framed[p] = null;

            for (int i = 0; i < allData.Count; i++)
            {
                float size;
                float y;
                if (panes[p].Frames(allData[i].piece, out size, out y))
                {
                    framed[p] = allData[i];
                    break;
                }
            }
        }
    }

    // Decides what each pane should show: an outline of its own framed piece, or a ghost
    // of the partner when only the other pane has framed something.
    void AssignWork()
    {
        for (int i = 0; i < allData.Count; i++)
        {
            allData[i].maskPane = -1;
            allData[i].ghostWanted[0] = false;
            allData[i].ghostWanted[1] = false;
        }

        for (int p = 0; p < 2; p++)
        {
            ghostPiece[p] = null;
            ghostSourcePane[p] = -1;

            if (framed[p] != null)
            {
                framed[p].maskPane = p;
            }
        }

        if (!showGhost)
        {
            return;
        }

        for (int p = 0; p < 2; p++)
        {
            int other = 1 - p;

            if (framed[p] == null || framed[other] != null)
            {
                continue;
            }

            SeamPiece partnerPiece = framed[p].pair.GetPartner(framed[p].piece);
            PieceData partner = FindData(partnerPiece);
            if (partner == null)
            {
                continue;
            }

            partner.ghostWanted[other] = true;
            GetGhost(partner, other);
            ghostPiece[other] = partner;
            ghostSourcePane[other] = p;
        }
    }

    // Switches the mask copies and ghosts on or off, then updates each pane's camera and overlay.
    void ApplyWork()
    {
        for (int i = 0; i < allData.Count; i++)
        {
            PieceData data = allData[i];

            for (int c = 0; c < data.maskCopies.Count; c++)
            {
                GameObject copy = data.maskCopies[c];

                if (data.maskPane >= 0)
                {
                    if (copy.layer != layers[data.maskPane])
                    {
                        copy.layer = layers[data.maskPane];
                    }
                    SetActiveIfChanged(copy, true);
                }
                else
                {
                    SetActiveIfChanged(copy, false);
                }
            }

            SetActiveIfChanged(data.ghosts[0], data.ghostWanted[0]);
            SetActiveIfChanged(data.ghosts[1], data.ghostWanted[1]);
        }

        for (int p = 0; p < 2; p++)
        {
            ApplyPane(p);
        }
    }

    // Outline mode uses the pane's own camera. Ghost mode uses the other pane's camera pose,
    // shifted sideways by one screen width so it renders the view just past the seam.
    void ApplyPane(int p)
    {
        if (framed[p] != null)
        {
            ShowPane(p, panes[p], 0f, outlineColor, outlineFill);
            return;
        }

        if (ghostPiece[p] != null)
        {
            int source = ghostSourcePane[p];
            PlaceGhost(ghostPiece[p], framed[source], p);

            float shift = 2f;
            if (!panes[source].seamIsOnRight)
            {
                shift = -2f;
            }

            ShowPane(p, panes[source], shift, ghostColor, ghostFill);
            return;
        }

        HidePane(p);
    }

    // Moves the partner's ghost to where it would sit if it were attached to the framed piece.
    void PlaceGhost(PieceData ghostOwner, PieceData framedData, int p)
    {
        Vector3 position;
        Quaternion rotation;

        if (!framedData.pair.GetPartnerPose(framedData.piece, out position, out rotation))
        {
            return;
        }

        ghostOwner.ghosts[p].transform.SetPositionAndRotation(position, rotation);
    }

    // Points this pane's mask camera at the source camera's view (optionally shifted sideways)
    // and turns on the overlay with the right colour and fill.
    void ShowPane(int p, PaneView source, float shift, Color color, float fill)
    {
        Camera maskCam = maskCams[p];
        Camera sourceCam = source.cam;

        maskCam.CopyFrom(sourceCam);
        maskCam.transform.SetPositionAndRotation(sourceCam.transform.position, sourceCam.transform.rotation);

        Matrix4x4 projection = sourceCam.projectionMatrix;
        projection.m02 += shift;
        maskCam.projectionMatrix = projection;

        maskCam.clearFlags = CameraClearFlags.SolidColor;
        maskCam.backgroundColor = Color.black;
        maskCam.cullingMask = 1 << layers[p];
        maskCam.targetTexture = maskTextures[p];
        maskCam.allowMSAA = false;
        maskCam.allowHDR = false;
        maskCam.enabled = true;

        overlays[p].color = color;
        overlayMaterials[p].SetFloat("_FillAlpha", fill);
        overlays[p].enabled = true;
    }

    // Turns off this pane's mask camera and overlay.
    void HidePane(int p)
    {
        if (overlays != null && overlays[p] != null)
        {
            overlays[p].enabled = false;
        }

        if (maskCams[p] != null)
        {
            maskCams[p].enabled = false;
        }
    }

    // Turns off every mask copy, ghost, overlay and mask camera.
    void HideEverything()
    {
        for (int i = 0; i < allData.Count; i++)
        {
            PieceData data = allData[i];

            for (int c = 0; c < data.maskCopies.Count; c++)
            {
                SetActiveIfChanged(data.maskCopies[c], false);
            }

            SetActiveIfChanged(data.ghosts[0], false);
            SetActiveIfChanged(data.ghosts[1], false);
        }

        for (int p = 0; p < 2; p++)
        {
            HidePane(p);
        }
    }

    // Returns the stored data for a piece, or null if it isn't in any pair.
    PieceData FindData(SeamPiece piece)
    {
        for (int i = 0; i < allData.Count; i++)
        {
            if (allData[i].piece == piece)
            {
                return allData[i];
            }
        }

        return null;
    }

    // Only calls SetActive when the state really changes.
    void SetActiveIfChanged(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}