using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections.Generic;
using Photon.Pun;

public class ObstacleSpawner : MonoBehaviourPun
{
    public SplineContainer splineContainer;
    public SplineExtrude splineExtrude;
    public GameObject obstaclePrefab;
    public int obstacleCount = 10;
    public float heightOffset = 0.1f;

    //아이템 위치
    private List<float> blockedItemT = new List<float>
    {
        1f / 8f, 2f / 8f, 3f / 8f, 4f / 8f, 5f / 8f, 6f / 8f, 7f / 8f
    };

    private List<float> placedTValues = new List<float>();
    private float minTSpacing = 0.05f;  // 도로 진행 방향 기준 최소 간격

    private bool IsNearItem(float t)
    {
        foreach (float itemT in blockedItemT)
        {
            if (Mathf.Abs(t - itemT) < 0.03f)
                return true;
        }
        return false;
    }

    private bool IsTooCloseToOtherObstacle(float t)
    {
        foreach (var placedT in placedTValues)
        {
            if (Mathf.Abs(placedT - t) < minTSpacing)
                return true;
        }
        return false;
    }

    public void SpawnObstacles()
    {
        if (splineContainer == null || splineContainer.Spline == null || splineContainer.Spline.Count == 0)
        {
            Debug.LogWarning("스플라인이 비어있어 장애물을 생성할 수 없습니다.");
            return;
        }

        float roadWidth = splineExtrude != null ? splineExtrude.Radius : 1f;
        float baseWidth = 8f;
        float scaleFactor = roadWidth / baseWidth;

        int i = 0;
        int attempt = 0;
        while (i < obstacleCount && attempt < 200)
        {
            float t = UnityEngine.Random.Range(0.05f, 0.95f);
            attempt++;

            if (IsNearItem(t) || IsTooCloseToOtherObstacle(t))
                continue;

            splineContainer.Spline.Evaluate(t, out float3 pos, out float3 tangent, out float3 normal);

            Vector3 center = (Vector3)pos;
            Vector3 forward = ((Vector3)tangent).normalized;
            Vector3 up = ((Vector3)normal).normalized;
            Vector3 right = Vector3.Cross(forward, up).normalized;

            float lateralOffset = UnityEngine.Random.Range(-roadWidth * 0.45f, roadWidth * 0.45f);
            Vector3 spawnPos = center + right * lateralOffset;

            GameObject obj = PhotonNetwork.Instantiate(obstaclePrefab.name, spawnPos, Quaternion.LookRotation(forward));
            obj.transform.parent = this.transform;
            obj.transform.localScale = Vector3.one * scaleFactor;

            placedTValues.Add(t);  // t값 저장
            Debug.Log($"📍 장애물 위치: {spawnPos:F2} / t: {t:F3}");
            i++;
        }

        Debug.Log("✅ 장애물 생성 완료 (t 간격 기반 겹침 방지)");
    }
}
