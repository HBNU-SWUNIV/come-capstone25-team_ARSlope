using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine;
using Photon.Pun;
using Unity.Mathematics;
using UnityEngine.UI;
using UnityEngine.Splines;
using System.Collections.Generic;

public class UIManager : MonoBehaviourPun
{
    public static UIManager instance { get; private set; } // 싱글턴

    public GameObject playerWatingUI; // 플레이어 대기 UI
    public GameObject objectSelectUI; // 오브젝트 개수 선택 UI
    public GameObject clientWatingUI; // 마스터가 오브젝트 개수 선택 전까지 클라이언트 대기 UI
    public GameObject gameoverUI; // 게임오버 UI
    public MeshRenderer gameResult;
    public GameObject popupUI;
    public MeshRenderer popupMesh;
    public TablePlaneDetector planeDetector;

    public List<Material> resultMaterials;
    public Material howToPlayMaterial;
    public Material manualMaterial;

    public SplineContainer splineContainer;

    public int maxSpawnObjectCount { get; private set; } // 최대 물체 선택 개수
    public RaceManager raceManager;
    private void Awake()
    {
        if (instance != null)
        {
            Destroy(this.gameObject);
            return;
        }

        instance = this;
    }

    private void Start()
    {
        CoreServices.SpatialAwarenessSystem.Disable();
    }

    // 마스터가 오브젝트 선택할 개수 정하기
    public void OnMasterSelectObject(int count)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("SelectObjectCount: " + count);
            photonView.RPC("StartSelectedObject", RpcTarget.All, count);
        }
    }

    // 물체 선택 시작
    //여기에 함수 복붙해서 시작하도록
    [PunRPC]
    public void StartSelectedObject(int count)
    {
        maxSpawnObjectCount = count;
        CoreServices.SpatialAwarenessSystem.Enable();
        ObjectSelectUI(false);
        ClientWatingUI(false);

        // 평면 인식 시작
        if (planeDetector != null)
        {
            planeDetector.StartDetection();
        }
        else
        {
            Debug.LogWarning("[UIManager] planeDetector가 연결되지 않음");
        }
    }

    // 플레이어 대기 UI 활성화 여부
    public void PlayerWatingUI(bool active)
    {
        if (playerWatingUI == null) return;
        playerWatingUI.SetActive(active);
    }
    // 오브젝트 선택 UI 활성화 여부
    public void ObjectSelectUI(bool active)
    {
        if (objectSelectUI == null) return;
        objectSelectUI.SetActive(active);
    }
    // 마스터가 오브젝트 개수 선택 전까지 클라이언트 대기 UI 활성화 여부
    public void ClientWatingUI(bool active)
    {
        if (clientWatingUI == null) return;
        clientWatingUI.SetActive(active);
    }

    // 게임 종료시 UI 활성화 및 위치 조정
    public void GameoverUI(bool isWinner)
    {
        if (gameoverUI == null || gameResult == null || resultMaterials == null) return;
        gameoverUI.SetActive(true);
        gameResult.material = isWinner ? resultMaterials[0] : resultMaterials[1];
        PlaceObjectAtSplineCenter();
    }
    void PlaceObjectAtSplineCenter()
    {
        if (gameoverUI == null || splineContainer == null) return;

        Vector3 centerPos = GetSplineCenter(splineContainer.Spline, splineContainer.transform);
        centerPos += Vector3.up * 0.2f;

        gameoverUI.transform.position = centerPos;
    }
    Vector3 GetSplineCenter(Spline spline, Transform splineTransform, int samples = 200)
    {
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)samples;
            spline.Evaluate(t, out float3 p, out _, out _);
            sum += (Vector3)p;
        }

        Vector3 localCenter = sum / samples;
        return splineTransform.TransformPoint(localCenter);
    }

    public void OpenPopup(Material image)
    {
        if (popupUI == null) return;
        
        popupUI.SetActive(true);
        popupMesh.material = image;
    }

    public void ClosePopup()
    {
        if (popupUI == null) return;

        popupUI.SetActive(false);
    }

    /// <summary>
    /// 게임 재시작 버튼에서 호출되는 메서드
    /// </summary>
    public void RestartGame()
    {
        // 게임이 시작되지 않았으면 재시작 불가
        SpawnManager spawnManager = splineContainer.GetComponent<SpawnManager>();
        if (spawnManager == null || !spawnManager.canRestart)
        {
            Debug.LogWarning("[UIManager] 게임이 시작되지 않아 재시작할 수 없습니다.");
            return;
        }

        if (raceManager != null)
        {
            raceManager.RequestRestartGame();
        }
        else
        {
            Debug.LogError("[UIManager] RaceManager를 찾을 수 없습니다!");
        }
    }
}
