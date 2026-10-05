using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class MergeGlow : MonoBehaviour
{
    [SerializeField] Color glowColor = new Color(1f, 0.75f, 0.85f, 1f);
    [SerializeField] float peakIntensity = 4f;
    [SerializeField] float restingIntensity = 1.5f;
    [SerializeField] float settleTime = 1f;
    [SerializeField] Ease riseEase = Ease.InQuad;

    class GlowTarget
    {
        public Material material;
        public bool usesEmission;
        public string colorProperty;
        public Color originalColor;
    }

    Dictionary<SeamPair, List<GlowTarget>> targetsByPair = new Dictionary<SeamPair, List<GlowTarget>>();
    List<GlowTarget> activeTargets;
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
        foreach (KeyValuePair<SeamPair, List<GlowTarget>> entry in targetsByPair)
        {
            for (int i = 0; i < entry.Value.Count; i++)
            {
                if (entry.Value[i].material != null)
                {
                    Destroy(entry.Value[i].material);
                }
            }
        }
    }

    // Starts from no glow, rises to the peak over overSec (in step with the eye tween),
    // then settles down to the resting glow.
    void Glow(SeamPair pair, float overSec)
    {
        activeTargets = GetTargets(pair);
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

    // Writes the current glow level to every material of the pair.
    void ApplyLevel(float value)
    {
        level = value;

        if (activeTargets == null)
        {
            return;
        }

        for (int i = 0; i < activeTargets.Count; i++)
        {
            GlowTarget target = activeTargets[i];

            if (target.usesEmission)
            {
                target.material.SetColor("_EmissionColor", glowColor * value);
            }
            else
            {
                Color brightened = target.originalColor * (1f + value);
                brightened.a = target.originalColor.a;
                target.material.SetColor(target.colorProperty, brightened);
            }
        }
    }

    // Returns the glow targets for a pair, building them the first time only.
    List<GlowTarget> GetTargets(SeamPair pair)
    {
        List<GlowTarget> targets;
        if (targetsByPair.TryGetValue(pair, out targets))
        {
            return targets;
        }

        targets = new List<GlowTarget>();
        AddPieceTargets(pair.pieceA, targets);
        AddPieceTargets(pair.pieceB, targets);
        targetsByPair[pair] = targets;

        return targets;
    }

    // Creates material instances for every mesh on the piece (ignoring the hint mask copies).
    void AddPieceTargets(SeamPiece piece, List<GlowTarget> targets)
    {
        MeshRenderer[] renderers = piece.GetComponentsInChildren<MeshRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].gameObject.name == "MaskCopy")
            {
                continue;
            }

            Material[] materials = renderers[i].materials;

            for (int m = 0; m < materials.Length; m++)
            {
                GlowTarget target = MakeTarget(materials[m]);
                if (target != null)
                {
                    targets.Add(target);
                }
            }
        }
    }

    // Prepares one material for glowing. Uses emission with the base texture as the emission map
    // when the shader supports it, otherwise brightens the base colour. Returns null if neither works.
    GlowTarget MakeTarget(Material material)
    {
        GlowTarget target = new GlowTarget();
        target.material = material;

        if (material.HasProperty("_EmissionColor"))
        {
            target.usesEmission = true;

            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            if (material.HasProperty("_EmissionMap") && material.GetTexture("_EmissionMap") == null)
            {
                material.SetTexture("_EmissionMap", material.mainTexture);
            }

            material.SetColor("_EmissionColor", Color.black);
            return target;
        }

        if (material.HasProperty("_BaseColor"))
        {
            target.colorProperty = "_BaseColor";
        }
        else if (material.HasProperty("_Color"))
        {
            target.colorProperty = "_Color";
        }
        else
        {
            return null;
        }

        target.originalColor = material.GetColor(target.colorProperty);
        return target;
    }
}