using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using UnityEngine;
using Photon.Pun;

public class HullClickable : MonoBehaviourPun, IMixedRealityPointerHandler
{
    private bool toggled = false;
    private GameObject markerCube;

    public SpawnManager spawnManager;
    public bool useWorldLocking = false;
    public Matrix4x4 frozenFromLocked = Matrix4x4.identity;

    private void Awake()
    {
        if (spawnManager == null)
            spawnManager = FindObjectOfType<SpawnManager>();
    }

    public void OnPointerClicked(MixedRealityPointerEventData eventData)
    {
        if (eventData.Handedness.IsLeft())
        {
            eventData.Use();
            return;
        }

        if (spawnManager.SpawnedObjectIDs.Count % 2 == 0 && PhotonNetwork.IsMasterClient || spawnManager.SpawnedObjectIDs.Count % 2 == 1 && !PhotonNetwork.IsMasterClient)
        {
            var rend = GetComponent<MeshRenderer>();
            if (rend != null)
            {
                Bounds b = rend.bounds;

                if (markerCube == null)
                {
                    if (spawnManager != null && spawnManager.cubePrefab != null)
                    {
                        // 네트워크 오브젝트는 생성 시점에 정확한 위치/회전을 지정해야 클라이언트 간에 동기화됩니다.
                        markerCube = PhotonNetwork.Instantiate(spawnManager.cubePrefab.name, b.center, Quaternion.identity);
                        markerCube.name = "HullMarkerCube";
                    }
                    else
                    {
                        // SpawnManager 또는 cubePrefab이 할당되지 않은 경우, 오류를 기록하고 실행을 중단합니다.
                        Debug.LogError("SpawnManager or cubePrefab is not assigned. Cannot create marker cube.");
                        return;
                    }
                }

                // 스케일은 공통적으로 설정합니다.
                markerCube.transform.localScale = b.size;

                Vector3 pos = b.center;
                Quaternion rot = Quaternion.identity;
                if (useWorldLocking)
                    pos = frozenFromLocked.MultiplyPoint3x4(pos);

                if (spawnManager != null)
                    spawnManager.AddPointFromPlaneDetector(pos, rot);

                toggled = !toggled;

            }
        }

        eventData.Use();
    }

    public void OnPointerDown(MixedRealityPointerEventData eventData) { }
    public void OnPointerDragged(MixedRealityPointerEventData eventData) { }
    public void OnPointerUp(MixedRealityPointerEventData eventData) { }
}
