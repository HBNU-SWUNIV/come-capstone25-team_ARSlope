using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;

public class ItemSpawner : MonoBehaviourPun
{
    public SplineContainer splineContainer;
    public SplineExtrude splineExtrude;
    public GameObject itemPrefab;
    public int spawnCount = 8; // 슬라이스 개수 (아이템 배치 지점 수)
    
    [Header("리스폰 설정")]
    [Tooltip("아이템이 리스폰되기까지 걸리는 시간(초)")]
    public float respawnTime = 5f;
    
    private Dictionary<GameObject, Vector3> itemPositions = new Dictionary<GameObject, Vector3>();
    private Dictionary<GameObject, Quaternion> itemRotations = new Dictionary<GameObject, Quaternion>();

    public void SpawnItemsWithDelay()
    {
        Debug.Log("ItemSpawner.SpawnItemsWithDelay() 호출됨");
        StartCoroutine(SpawnAfterMeshReady());
    }

    private IEnumerator SpawnAfterMeshReady()
    {
        yield return null; // 메쉬 생성 프레임 대기
        yield return new WaitForSeconds(0.1f); // Collider 적용 대기

        // 기존 아이템 제거 및 Dictionary 초기화
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
        itemPositions.Clear();
        itemRotations.Clear();

        // 도로 폭 기준 간격 계산
        float roadWidth = splineExtrude.Radius;
        float baseWidth = 8f;
        float scaleFactor = roadWidth / baseWidth;
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
            int[] spawnIndices = { 1, 3, 5 };

            foreach (int posIndex in spawnIndices)
            {
                float offsetAmount = -roadWidth / 2f + spacing * (posIndex + 0.5f);
                Vector3 offset = right * offsetAmount;
                Vector3 spawnPos = center + offset;

                GameObject obj = PhotonNetwork.Instantiate(itemPrefab.name, spawnPos, Quaternion.identity);
                Debug.Log($"아이템 생성 위치: {spawnPos:F2} (7등분 중 {posIndex + 1}번째 지점)");

                obj.transform.parent = this.transform;
                obj.transform.localScale = Vector3.one * scaleFactor;
                
                // 아이템 위치와 회전 저장 (리스폰용)
                itemPositions[obj] = spawnPos;
                itemRotations[obj] = Quaternion.identity;
                
                //Debug.DrawRay(spawnPos, Vector3.up * 1f, Color.yellow, 5f);
            }
        }

        Debug.Log("아이템 생성 완료 (7등분 기준 2,4,6 위치)");
    }
    
    /// <summary>
    /// 아이템이 획득되었을 때 호출되는 메서드. 아이템을 비활성화하고 일정 시간 후 리스폰
    /// </summary>
    public void OnItemCollected(GameObject item)
    {
        if (!itemPositions.ContainsKey(item))
        {
            Debug.LogWarning($"아이템 {item.name}의 위치 정보를 찾을 수 없습니다.");
            return;
        }
        
        // 네트워크 동기화를 위해 RPC 호출
        if (photonView != null && PhotonNetwork.IsConnected)
        {
            int viewID = item.GetComponent<PhotonView>()?.ViewID ?? -1;
            if (viewID != -1)
            {
                photonView.RPC("RPC_RespawnItem", RpcTarget.AllBuffered, viewID);
            }
        }
        else
        {
            // 오프라인 모드 또는 PhotonView가 없는 경우 직접 처리
            StartCoroutine(RespawnItemCoroutine(item));
        }
    }
    
    [PunRPC]
    private void RPC_RespawnItem(int itemViewID)
    {
        PhotonView itemView = PhotonView.Find(itemViewID);
        if (itemView != null && itemView.gameObject != null)
        {
            StartCoroutine(RespawnItemCoroutine(itemView.gameObject));
        }
    }
    
    private IEnumerator RespawnItemCoroutine(GameObject item)
    {
        if (!itemPositions.ContainsKey(item))
        {
            yield break;
        }
        
        // 아이템 비활성화
        item.SetActive(false);
        
        // 일정 시간 대기
        yield return new WaitForSeconds(respawnTime);
        
        // 아이템이 여전히 존재하는지 확인
        if (item != null && itemPositions.ContainsKey(item))
        {
            // 원래 위치와 회전으로 복귀
            item.transform.position = itemPositions[item];
            item.transform.rotation = itemRotations[item];
            
            // 아이템 다시 활성화
            item.SetActive(true);
            
            Debug.Log($"아이템 리스폰: {item.name} at {itemPositions[item]}");
        }
    }
}
