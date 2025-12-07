using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.SpatialAwareness;   // Observer 제어
using Microsoft.MixedReality.WorldLocking.Core;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using System.Linq;

public class SpawnManager : MonoBehaviourPun, IMixedRealityPointerHandler
{
    public GameObject cubePrefab; // 생성할 프리팹

    private List<Transform> selectedObjects = new List<Transform>();
    private List<int> spawnedObjectIDs = new List<int>(); // 생성 큐브들의 PhotonViewID
    public List<int> SpawnedObjectIDs => spawnedObjectIDs;
    public SplineContainer splineContainer; // 스플라인 연결용

    public Transform spawnRootObject; // 생성할 루트 오브젝트(선택 큐브의 부모)

    public GameObject player1CarPrefab;
    public GameObject player2CarPrefab;
    private bool hasCar = false;

    public PhysicsMaterial physicMaterial;
    SplineExtrude splineExtrude;

    // 트랙 완성 후 추가 입력/스폰 차단 플래그
    private bool trackFinalized = false;

    // 게임 재시작 가능 여부 (레이싱 카가 모두 생성된 후 true)
    public bool canRestart = false;

    private void Awake()
    {
        CoreServices.InputSystem.RegisterHandler<IMixedRealityPointerHandler>(this);
    }

    private void Start()
    {
        splineExtrude = GetComponent<SplineExtrude>();
    }

    public void OnPointerClicked(MixedRealityPointerEventData eventData)
    {
        // 트랙 완성 후에는 입력 차단
        if (trackFinalized) return;

        // 예외 처리
        if (!AnchorTransferStatus.isAnchorImported) return;
        if (eventData.Pointer == null || eventData.Pointer.Result == null || eventData.Pointer.Result.Details.Object == null || eventData.Handedness.IsRight())
        {
            return;
        }

        // 충돌한 오브젝트가 Spatial Awareness일 때만 스폰
        if (eventData.Pointer.Result.Details.Object.layer.Equals(LayerMask.NameToLayer("Spatial Awareness")))
        {
            Vector3 lockedPosition = eventData.Pointer.Result.Details.Point; // Locked 기준 위치
            Quaternion lockedRotation = Quaternion.LookRotation(eventData.Pointer.Result.Details.Normal);

            // FrozenFromLocked 행렬
            var manager = WorldLockingManager.GetInstance();
            var frozenFromLocked = manager.FrozenFromLocked;
            Matrix4x4 frozenFromLockedMatrix = Matrix4x4.TRS(frozenFromLocked.position, frozenFromLocked.rotation, Vector3.one);

            // Frozen 공간으로 변환
            Vector3 frozenPosition = frozenFromLockedMatrix.MultiplyPoint3x4(lockedPosition);

            if (spawnedObjectIDs.Count % 2 == 0 && PhotonNetwork.IsMasterClient || spawnedObjectIDs.Count % 2 == 1 && !PhotonNetwork.IsMasterClient)
            {
                GameObject cube = PhotonNetwork.Instantiate(cubePrefab.name, frozenPosition, lockedRotation);
                cube.transform.parent = spawnRootObject;
                photonView.RPC("SpawnObject", RpcTarget.AllBuffered, cube.GetComponent<PhotonView>().ViewID);
            }
            else return;
        }
    }
    public void OnPointerDragged(MixedRealityPointerEventData eventData) { }
    public void OnPointerUp(MixedRealityPointerEventData eventData) { }

    // 에어탭 했을 때 (왼손: 원하는 곳에 선택 큐브 스폰)
    public void OnPointerDown(MixedRealityPointerEventData eventData) { }

    // 물체 생성 및 저장
    [PunRPC]
    public void SpawnObject(int spawnedObjectID)
    {
        spawnedObjectIDs.Add(spawnedObjectID); // 물체 저장

        // 생성된 물체가 최대 물체 생성 개수와 동일한 경우
        if (UIManager.instance.maxSpawnObjectCount == spawnedObjectIDs.Count)
        {
            // ---------------------- [원래 코드] ----------------------
            // CoreServices.SpatialAwarenessSystem.Disable(); // 공간 비활성화 (트랙 전 비활성화 문제) → 주석
            // ---------------------------------------------------------

            // ViewID를 Transform으로 변환 후 스플라인 생성
            photonView.RPC("ViewIDToTransform", RpcTarget.AllBuffered, spawnedObjectIDs.ToArray());
        }
    }

    // ViewID를 Transform으로 변환
    [PunRPC]
    private void ViewIDToTransform(int[] spawnedObjectIDs)
    {
        Debug.Log("spawnedObjectIDs: " + spawnedObjectIDs.Length);

        // 기존 데이터 초기화
        selectedObjects.Clear();

        foreach (int viewID in spawnedObjectIDs)
        {
            PhotonView pv = PhotonView.Find(viewID);
            if (pv != null)
            {
                selectedObjects.Add(pv.transform);
                Debug.Log("selectedObjects: " + selectedObjects.Count);
            }
        }

        CreateSpline(selectedObjects); // 스플라인 생성
    }

    // 스플라인 생성
    private void CreateSpline(List<Transform> selectedObjects)
    {
        // 스플라인 컨테이너가 없는 경우
        if (splineContainer == null)
        {
            Debug.LogError("SplineContainer가 연결되어 있지 않습니다.");
            return;
        }

        // 최소 물체 개수가 아닌 경우
        if (selectedObjects.Count <= 2)
        {
            Debug.LogWarning("Spline을 만들려면 최소 3개의 오브젝트가 필요합니다.");
            return;
        }

        var spline = splineContainer.Spline;
        spline.Clear();

        for (int i = 0; i < selectedObjects.Count; i++)
        {
            Transform obj = selectedObjects[i];

            Vector3 prev = selectedObjects[(i - 1 + selectedObjects.Count) % selectedObjects.Count].position;
            Vector3 next = selectedObjects[(i + 1) % selectedObjects.Count].position;

            // 물체의 높이 계산
            float height = 0f;
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                height = renderer.bounds.size.y;
            }
            else
            {
                var collider = obj.GetComponent<Collider>();
                if (collider != null)
                    height = collider.bounds.size.y;
            }

            // 현재 위치 보정: 물체 위로 살짝 띄우기
            Vector3 current = obj.position + new Vector3(0, height * 0.5f + 0.2f, 0);

            Vector3 toPrev = (prev - current).normalized;
            Vector3 toNext = (next - current).normalized;
            Vector3 dir = (toNext - toPrev).normalized;

            float scale = Vector3.Distance(current, next) * 0.25f;
            Vector3 tangentOut = dir * scale;
            Vector3 tangentIn = -tangentOut;

            BezierKnot knot = new BezierKnot(current, tangentIn, tangentOut);
            spline.Add(knot);
        }

        spline.Closed = true;

        // 스플라인 너비 조정
        float4x4 localToWorld = float4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        float totalLength = SplineUtility.CalculateLength(spline, localToWorld);
        if (splineExtrude != null)
        {
            splineExtrude.Radius = Mathf.Clamp(totalLength * 0.05f, 0.1f, 0.4f);
        }

        // 도로 자동 Extrude & 머티리얼 분리
        if (splineExtrude != null)
        {
            // 메시 재생성
            splineExtrude.Rebuild();
            Debug.Log("🔁 SplineExtrude.Rebuild() 자동 호출됨!");

            // MultiMaterialSpline 자동 실행
            var multiMat = splineExtrude.GetComponent<MultiMaterialSpline>();
            if (multiMat != null)
            {
                // 0.1초 지연 후 적용 (Rebuild가 완료된 뒤)
                splineExtrude.StartCoroutine(ApplyAfterDelay(multiMat, 0.1f));
            }
            else
            {
                Debug.LogWarning("⚠️ MultiMaterialSpline 컴포넌트를 찾을 수 없습니다. 머터리얼 분리 생략됨.");
            }
        }
        else
        {
            Debug.LogWarning("⚠️ SplineExtrude가 연결되어 있지 않습니다.");
        }

        // SplineExtrude의 메쉬에 MeshCollider 자동 추가
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            MeshCollider meshCollider = GetComponent<MeshCollider>();
            if (meshCollider == null)
                meshCollider = gameObject.AddComponent<MeshCollider>();

            meshCollider.sharedMesh = meshFilter.sharedMesh;
            meshCollider.convex = false;
            meshCollider.material = physicMaterial;
        }

        Debug.Log("Spline 생성 완료");

        // ---------------------- [원래 코드] 선택 큐브 숨김 ----------------------
        foreach (var obj in selectedObjects)
            obj.gameObject.SetActive(false);
        // ----------------------------------------------------------------------

        // ---------------------- [새 코드] 트랙 생성 후 정리 & 입력 차단 ----------------------
        FinalizeTrackAndCleanup();
        // ----------------------------------------------------------------------

        StartCoroutine(WaitAndSpawnCar());
    }
    private IEnumerator ApplyAfterDelay(MultiMaterialSpline multiMat, float delay)
    {
        yield return new WaitForEndOfFrame();
        multiMat.SendMessage("ApplyMultiMaterial", SendMessageOptions.DontRequireReceiver);
    }

    // 트랙 완성 후: 표시 제거 + 큐브 파괴(마스터) + 입력 차단 + Observer 정리
    private void FinalizeTrackAndCleanup()
    {
        // 1) 평면/물체 인식 표시물 전체 제거 (Hull/라인/점/ HullMarkerCube)
        var detector = FindObjectOfType<TablePlaneDetector>();
        if (detector != null)
        {
            detector.ClearDetectedVisuals(); // ← Hull만 지우려면 ClearHulls()
        }

        // 2) 스폰된 큐브 전부 파괴(마스터에게만 요청)
        photonView.RPC("DestroySpawnedCubesMaster", RpcTarget.MasterClient, spawnedObjectIDs.ToArray());

        // 4) 내부 상태 초기화
        selectedObjects.Clear();
        spawnedObjectIDs.Clear();

        // 5) 이후 입력 차단
        trackFinalized = true;
        CoreServices.InputSystem?.UnregisterHandler<IMixedRealityPointerHandler>(this);

        // 6) 공간 메쉬: Occlusion + Suspend (렌더 OFF, 관찰 정지)
        var observer = CoreServices.GetSpatialAwarenessSystemDataProvider<IMixedRealitySpatialAwarenessMeshObserver>();
        if (observer != null)
        {
            observer.DisplayOption = SpatialAwarenessMeshDisplayOptions.Occlusion;
        }

        CoreServices.SpatialAwarenessSystem.Disable();
    }

    private IEnumerator WaitAndSpawnCar()
    {
        yield return new WaitForEndOfFrame();

        if (!hasCar)
        {
            hasCar = true;
            SpawnCarOnSpline();
        }
    }

    void SpawnCarOnSpline()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (splineContainer == null)
        {
            Debug.LogWarning("SplineContainer가 연결되지 않았습니다.");
            return;
        }

        // ① 시작점 정보
        splineContainer.Spline.Evaluate(0f, out float3 posF3, out float3 tanF3, out float3 upF3);
        Vector3 center = (Vector3)posF3;
        Vector3 forward = ((Vector3)tanF3).normalized;
        Vector3 up = ((Vector3)upF3).normalized;
        Vector3 right = Vector3.Cross(up, forward).normalized;

        // ② 차선 오프셋
        float laneOffset = splineExtrude.Radius * 0.2f;
        float heightLift = splineExtrude.Radius * 0.1f;
        Vector3 leftPos = center - right * laneOffset + up * heightLift;
        Vector3 rightPos = center + right * laneOffset + up * heightLift;
        Quaternion rot = Quaternion.LookRotation(forward, up);

        // ③ 공통 스케일
        float carScale = splineExtrude.Radius * 0.1f;

        // ④ 왼쪽(마스터) 차량 생성
        GameObject leftCar = PhotonNetwork.Instantiate(
            player1CarPrefab.name, leftPos, rot, 0, new object[] { carScale });
        leftCar.transform.localScale = Vector3.one * carScale;
        InitCar(leftCar, splineExtrude);

        // ⑤ 오른쪽(다른 플레이어) 차량 생성
        GameObject rightCar = PhotonNetwork.Instantiate(
            player2CarPrefab.name, rightPos, rot, 0, new object[] { carScale });
        rightCar.transform.localScale = Vector3.one * carScale;
        InitCar(rightCar, splineExtrude);

        // ⑥ 소유권 이전: 첫 번째 다른 플레이어에게
        Photon.Realtime.Player other = PhotonNetwork.PlayerListOthers.FirstOrDefault();
        if (other != null)
            rightCar.GetComponent<PhotonView>().TransferOwnership(other);

        StartCoroutine(CheckSpawnAllCar(leftCar, rightCar));
    }

    private IEnumerator CheckSpawnAllCar(GameObject leftCar, GameObject rightCar)
    {
        while (leftCar == null || rightCar == null)
        {
            yield return new WaitForSecondsRealtime(0.1f);
        }

        // 아이템 생성 연결
        ItemSpawner spawner = FindObjectOfType<ItemSpawner>();
        if (spawner != null)
        {
            Debug.Log("ItemSpawner.SpawnItemsWithDelay() 호출됨");
            spawner.SpawnItemsWithDelay(); // 코루틴으로 호출
        }
        else
        {
            Debug.LogWarning("ItemSpawner를 찾지 못했습니다.");
        }

        ObstacleSpawner obstacleSpawner = FindObjectOfType<ObstacleSpawner>();
        if (obstacleSpawner != null)
        {
            Debug.Log("ObstacleSpawner.SpawnObstacles() 호출됨");
            obstacleSpawner.SpawnObstacles();
        }
        else
        {
            Debug.LogWarning("ObstacleSpawner를 찾지 못했습니다.");
        }

        // 레이싱 카가 모두 생성된 후 재시작 가능하도록 설정
        //canRestart = true;
        photonView.RPC("SetCanRestart", RpcTarget.AllBuffered, true);
    }

    [PunRPC]
    public void SetCanRestart(bool value)
    {
        canRestart = value;
        Debug.Log($"[SpawnManager] canRestart set to {value}");
    }

    void InitCar(GameObject car, SplineExtrude splineExtrude)
    {
        var mover = car.GetComponent<CarMove>();
        if (mover != null)
        {
            mover.progress = 0f;
            mover.splineContainer = splineContainer;
            mover.speed = Mathf.Min(splineExtrude.Radius * 0.7f, 0.5f);
        }
    }

    // 헐/마커 선택 경로에서 호출: 선택 포인트를 스폰으로 반영
    public void AddPointFromPlaneDetector(Vector3 frozenPosition, Quaternion frozenRotation)
    {
        // 트랙 완성 후에는 입력 차단
        if (trackFinalized) return;

        GameObject cube = PhotonNetwork.Instantiate(cubePrefab.name, frozenPosition, frozenRotation);
        cube.transform.parent = spawnRootObject;

        // 기존 로직 연결 (스플라인 생성, 차량/아이템 스폰)
        photonView.RPC("SpawnObject", RpcTarget.AllBuffered, cube.GetComponent<PhotonView>().ViewID);

        cube.SetActive(false); // 스플라인 지점 등록 후 물체 비활성화 -> 중복으로 물체 보이는 문제 해결
    }

    // 마스터가 전 클라이언트에 대해 스폰된 선택 큐브를 일괄 파괴
    [PunRPC]
    private void DestroySpawnedCubesMaster(int[] viewIDs)
    {
        if (!PhotonNetwork.IsMasterClient) return; // 안전 가드

        foreach (var id in viewIDs)
        {
            var pv = PhotonView.Find(id);
            if (pv != null && pv.gameObject != null)
            {
                Debug.Log($"[RPC] Destroy cube viewID={id}");
                PhotonNetwork.Destroy(pv.gameObject);
            }
        }
    }
}
