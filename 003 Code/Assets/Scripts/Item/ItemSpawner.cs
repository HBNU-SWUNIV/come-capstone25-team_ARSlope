using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections;
using Photon.Pun;

public class ItemSpawner : MonoBehaviourPun
{
    public SplineContainer splineContainer;
    public GameObject itemPrefab;
    public SplineExtrude splineExtrude;
    public int spawnCount = 8; // 슬라이스 개수 (아이템 배치 지점 수)

    public void SpawnItemsWithDelay()
    {
        Debug.Log("ItemSpawner.SpawnItemsWithDelay() 호출됨");
        StartCoroutine(SpawnAfterMeshReady());
    }

    private IEnumerator SpawnAfterMeshReady()
    {
        yield return null; // 메쉬 생성 프레임 대기
        yield return new WaitForSeconds(0.1f); // Collider 적용 대기

        // 기존 아이템 제거
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        // 도로 폭 기준 간격 계산
        float roadWidth = splineExtrude.Radius;
        float spacing = roadWidth / 7f; // 7등분

        for (int i = 1; i < spawnCount; i++)
        {
            float t = i / (float)spawnCount;
            splineContainer.Spline.Evaluate(t, out float3 pos, out float3 tangent, out float3 normal);

            Vector3 center = (Vector3)pos;
            Vector3 forward = ((Vector3)tangent).normalized;
            Vector3 up = ((Vector3)normal).normalized;
            Vector3 right = Vector3.Cross(forward, up).normalized;

            // 디버그: 도로 전체 폭 시각화 (빨간 선)
            //Debug.DrawLine(center - right * (roadWidth / 2f), center + right * (roadWidth / 2f), Color.red, 5f);

            // 7등분 중 2, 4, 6 번째 위치에만 아이템 생성 (인덱스 1, 3, 5)
            int[] itemPositions = { 1, 3, 5 };

            foreach (int posIndex in itemPositions)
            {
                float offsetAmount = -roadWidth / 2f + spacing * (posIndex + 0.5f);
                Vector3 offset = right * offsetAmount;
                Vector3 spawnPos = center + offset;

                GameObject obj = PhotonNetwork.Instantiate(itemPrefab.name, spawnPos, Quaternion.identity);
                Debug.Log($"아이템 생성 위치: {spawnPos:F2} (7등분 중 {posIndex + 1}번째 지점)");

                //Debug.DrawRay(spawnPos, Vector3.up * 1f, Color.yellow, 5f);
            }
        }

        Debug.Log("아이템 생성 완료 (7등분 기준 2,4,6 위치)");
    }
}
