using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 여러 장의 배경(Background_1 = 가장 먼 하늘 … Background_5 = 가장 가까운 마을 실루엣)을 겹쳐
/// 카메라가 움직일 때 먼 층일수록 덜 움직이게 해 원근감(시차)을 만든다.
///  - 각 층은 SpriteRenderer 의 Tiled 그리기 모드로 가로로 끝없이 이어진다 → 마을 길이와 상관없이 끊기지 않음.
///  - 층마다 카메라 화면 높이에 맞춰 크기를 자동으로 맞춘다 (해상도·카메라 크기가 바뀌어도 화면을 꽉 채움).
///  - 카메라가 Visible Camera X 범위(마을) 밖으로 순간이동하면(검은 공간·집 안) 배경을 숨긴다.
/// 기존 Parallax 는 시작 위치 기준이라 순간이동이 많은 챕터3 에선 배경이 엉뚱한 곳에 가 있게 되어,
/// 매 프레임 '현재 카메라 위치'로부터 바로 계산하는 이 방식을 쓴다.
/// </summary>
public class LayeredParallaxBackground : MonoBehaviour
{
    [Serializable]
    public class Layer
    {
        [Tooltip("이 층의 배경 SpriteRenderer (Draw Mode 는 자동으로 Tiled 로 바뀐다)")]
        public SpriteRenderer renderer;
        [Tooltip("카메라를 따라가는 정도. 1 = 화면에 고정(가장 멂, 하늘), 0 = 땅과 같이 움직임. 가까운 층일수록 작게")]
        [Range(0f, 1f)] public float factor = 0.9f;
        [Tooltip("이 층의 크기 배율 (1 = 화면 높이에 맞춤). 작게 하면 같은 지평선 위에 더 작게 놓여 멀리 있는 것처럼 보인다")]
        [Range(0.1f, 2f)] public float heightScale = 1f;
    }

    [Tooltip("먼 층부터 가까운 층 순서로 (Background_1, 2, 3 …). 위에 있을수록 뒤에 그려진다")]
    public List<Layer> layers = new List<Layer>();

    [Header("배치")]
    [Tooltip("따라갈 카메라. 비우면 Camera.main")]
    public Camera cam;
    [Tooltip("첫 층의 Sorting Order. 다음 층은 1씩 커진다 (땅·오브젝트보다 작게 둘 것)")]
    public int baseSortingOrder = -100;
    [Tooltip("배경 높이 = 카메라 화면 높이 × 이 값. 1보다 조금 크게 두면 위아래 가장자리가 안 보인다")]
    [Min(0.1f)] public float heightCoverage = 1.05f;
    [Tooltip("배경 전체(지평선)의 세로 위치 보정 (화면 높이 대비 비율). 0 = 크기 1인 층이 화면 가운데, 양수 = 위로. " +
             "땅에 가려지는 배경 아랫부분(들판 등)이 보이도록 조금 올린다")]
    [Range(-0.5f, 0.5f)] public float verticalOffset = 0f;

    [Header("보이는 범위")]
    [Tooltip("카메라 x 가 이 범위 안일 때만 배경을 보여준다 (x = 최소, y = 최대). 마을 구간에 맞춘다")]
    public Vector2 visibleCameraX = new Vector2(-60f, 100f);

    bool shown = true;

    void Start()
    {
        if (cam == null) cam = Camera.main;
        for (int i = 0; i < layers.Count; i++)
        {
            var r = layers[i].renderer;
            if (r == null) continue;
            r.drawMode = SpriteDrawMode.Tiled;  // 가로 반복
            r.sortingOrder = baseSortingOrder + i;
        }
    }

    void LateUpdate()
    {
        if (cam == null) return;
        Vector3 c = cam.transform.position;

        bool inside = c.x >= visibleCameraX.x && c.x <= visibleCameraX.y;
        if (inside != shown) SetShown(inside);
        if (!inside) return;

        float viewH = cam.orthographicSize * 2f;
        float viewW = viewH * cam.aspect;
        // 모든 층이 같은 아랫선(지평선)에 서도록 기준을 잡는다 (Height Scale 이 1인 층의 아래 가장자리)
        float bottomY = c.y + (verticalOffset - heightCoverage * 0.5f) * viewH;

        foreach (var layer in layers)
        {
            var r = layer.renderer;
            if (r == null || r.sprite == null) continue;

            // 크기: 화면 높이에 맞춘 배율. 폭은 화면 폭 + 한 장 여유 (어느 위치에서도 빈틈 없게)
            Vector2 spriteSize = r.sprite.bounds.size;
            float scale = viewH * heightCoverage * layer.heightScale / spriteSize.y;
            r.transform.localScale = new Vector3(scale, scale, 1f);
            float tileW = spriteSize.x;                                  // 한 장 폭 (로컬)
            int tiles = Mathf.CeilToInt(viewW / (tileW * scale)) + 2;
            r.size = new Vector2(tileW * tiles, spriteSize.y);

            // 위치: 무늬의 기준점은 카메라 이동의 factor 만큼만 따라간다.
            // 렌더러 자체는 한 장 폭 단위로 카메라 근처로 옮겨 붙여 끝없이 이어지는 것처럼 보이게 한다.
            float worldTileW = tileW * scale;
            float origin = c.x * layer.factor;
            float x = origin + Mathf.Round((c.x - origin) / worldTileW) * worldTileW;
            float y = bottomY + spriteSize.y * scale * 0.5f; // 아래 가장자리를 지평선에 맞춤
            r.transform.position = new Vector3(x, y, r.transform.position.z);
        }
    }

    void SetShown(bool on)
    {
        shown = on;
        foreach (var layer in layers) if (layer.renderer != null) layer.renderer.enabled = on;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.8f);
        Gizmos.DrawLine(new Vector3(visibleCameraX.x, -5f), new Vector3(visibleCameraX.x, 10f));
        Gizmos.DrawLine(new Vector3(visibleCameraX.y, -5f), new Vector3(visibleCameraX.y, 10f));
    }
}
