using System.Collections.Generic;
using UnityEngine;

public class TourAPIImageController : MonoBehaviour
{
    [SerializeField] private DoubleTap3D doubleTap3DScript;

    private void Awake()
    {
        if (doubleTap3DScript == null)
        {
            enabled = false;
            return;
        }
    }

    // 관광공사 사진은 주소만 넘긴다 — 사진은 상세를 열 때 받는다
    // (예전엔 오브젝트가 뜰 때 원본을 최대 10장씩 받았고, 풀에서 다시 쓰일 때마다 앞 장소 것이 지워지지 않고 쌓였다)
    public void SetTourImages(List<string> imageUrls)
    {
        if (doubleTap3DScript != null) doubleTap3DScript.SetImageUrls(imageUrls, false);
    }

    public void ClearImages()
    {
        if (doubleTap3DScript != null) doubleTap3DScript.ReleasePhotos();
    }
}
