using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class ItemMergeGlow : MonoBehaviour
{
    [SerializeField] Color glowColor = new Color(1f, 0.75f, 0.85f, 1f);
    [SerializeField] float peakIntensity = 4f;
    [SerializeField] float restingIntensity = 1.5f;
    [SerializeField] float settleTime = 1f;
    [SerializeField] Ease riseEase = Ease.InQuad;

    [SerializeField] string colorProperty = "_GlowColor";
    [SerializeField] string amountProperty = "_GlowAmount";

    Dictionary<SeamPair, List<Material>> materialsByPair = new Dictionary<SeamPair, List<Material>>();
    List<Material> activeMaterials;
    Sequence glowSequence;
    float level;

    void OnEnable()
    {
        SeamMatchManager.OnMatched += Glow;
    }

    void OnDisable()
    {
        SeamMatchManager.OnMatched -= Glow;

        if (glowSequence != null)
        {
            glowSequence.Kill();
        }
    }

    // Frees the material instances created for the glow.
    void OnDestroy()
    {
        foreach (KeyValuePair<SeamPair, List<Material>> entry in materialsByPair)
        {
            for (int i = 0; i < entry.Value.Count; i++)
            {
                if (entry.Value[i] != null)
                {
                    Destroy(entry.Value[i]);
                }
            }
        }
    }

    // Starts from no glow, rises to the peak over overSec (in step with the eye tween),
    // then settles down to the resting glow.
    void Glow(SeamPair pair, float overSec)
    {
        activeMaterials = GetMaterials(pair);
        ApplyLevel(0f);

        if (glowSequence != null)
        {
            glowSequence.Kill();
        }

        glowSequence = DOTween.Sequence();
        glowSequence.SetLink(gameObject);
        glowSequence.Append(DOTween.To(() => level, ApplyLevel, peakIntensity, overSec).SetEase(riseEase));
        glowSequence.Append(DOTween.To(() => level, ApplyLevel, restingIntensity, settleTime).SetEase(Ease.OutQuad));
    }

    // Writes the current glow amount to every material of the pair.
    void ApplyLevel(float value)
    {
        level = value;

        if (activeMaterials == null)
        {
            return;
        }

        for (int i = 0; i < activeMaterials.Count; i++)
        {
            activeMaterials[i].SetFloat(amountProperty, value);
        }
    }

    // Returns the pair's glow materials, building them the first time only.
    List<Material> GetMaterials(SeamPair pair)
    {
        List<Material> materials;
        if (materialsByPair.TryGetValue(pair, out materials))
        {
            return materials;
        }

        materials = new List<Material>();
        AddPieceMaterials(pair.pieceA, materials);
        AddPieceMaterials(pair.pieceB, materials);
        materialsByPair[pair] = materials;

        Debug.Log("MergeGlow: found " + materials.Count + " glow materials");
        return materials;
    }

    // Creates material instances for every mesh on the piece (ignoring the hint mask copies)
    // and keeps only those that have the glow properties.
    void AddPieceMaterials(SeamPiece piece, List<Material> list)
    {
        MeshRenderer[] renderers = piece.GetComponentsInChildren<MeshRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].gameObject.name == "MaskCopy")
            {
                continue;
            }

            Material[] instances = renderers[i].materials;

            for (int m = 0; m < instances.Length; m++)
            {
                if (!instances[m].HasProperty(amountProperty))
                {
                    Debug.LogWarning("MergeGlow: " + instances[m].name + " has no property " + amountProperty);
                    continue;
                }

                instances[m].SetColor(colorProperty, glowColor);
                instances[m].SetFloat(amountProperty, 0f);
                list.Add(instances[m]);
            }
        }
    }
}