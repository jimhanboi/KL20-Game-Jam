using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ItemMergeFadeScreen : MonoBehaviour
{

    [SerializeField] RawImage leftScreen, rightScreen; 

    private void OnEnable()
    {
        SeamMatchManager.OnMatched += MergeScreen;
    }

    private void OnDisable()
    {
        SeamMatchManager.OnMatched -= MergeScreen;
    }

    void MergeScreen(SeamPair pair, float dur)
    {
        leftScreen.transform.DOScale(Vector3.one, dur);
        rightScreen.transform.DOScale(Vector3.one, dur);
    }

}
