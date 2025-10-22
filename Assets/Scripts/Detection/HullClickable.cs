using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using UnityEngine;
using Photon.Pun; // PhotonView 제거용

public class HullClickable : MonoBehaviour, IMixedRealityPointerHandler
{
    private bool toggled = false;
    private GameObject markerCube;

    public SpawnManager spawnManager;                // SpawnManager 참조 (인스펙터/런타임 주입)
    public bool useWorldLocking = false;             // WLT 쓰면 true
    public Matrix4x4 frozenFromLocked = Matrix4x4.identity; // 잠김→동결 변환 행렬

    private void Awake()
    {
        if (spawnManager == null)
            spawnManager = FindObjectOfType<SpawnManager>();
    }

    public void OnPointerClicked(MixedRealityPointerEventData eventData)
    {
        // 오른손만 허용 (원래 코드는 손 구분 없음)
        if (!eventData.Handedness.IsRight())
        {
            eventData.Use();
            return;
        }

        Debug.Log("[Hull] 영역 클릭됨");

        var rend = GetComponent<MeshRenderer>();
        if (rend != null)
        {
            Bounds b = rend.bounds;

            if (markerCube == null)
            {
                // ----------------------- [원래 코드]
                // markerCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                // markerCube.name = "HullMarkerCube";
                // Destroy(markerCube.GetComponent<Collider>());
                // -----------------------

                // ----------------------- [새 코드] 왼손과 같은 프리팹으로 미리보기 생성
                if (spawnManager != null && spawnManager.cubePrefab != null)
                {
                    markerCube = Instantiate(spawnManager.cubePrefab);
                    markerCube.name = "HullMarkerCube"; // 정리 루틴과 호환되도록 동일 명명

                    // 네트워크/물리 컴포넌트 제거 (로컬 미리보기 오브젝트로만 사용)
                    var pv = markerCube.GetComponent<PhotonView>();
                    if (pv) Destroy(pv);
                    var rb = markerCube.GetComponent<Rigidbody>();
                    if (rb) Destroy(rb);
                    foreach (var col in markerCube.GetComponentsInChildren<Collider>(true))
                        Destroy(col);
                }
                else
                {
                    // 안전망: 프리팹 없으면 기본 큐브
                    markerCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    markerCube.name = "HullMarkerCube";
                    var c = markerCube.GetComponent<Collider>();
                    if (c) Destroy(c);
                }
                // -----------------------
            }

            // 위치/크기 맞추기 (Bounds에 딱 맞게)
            markerCube.transform.position = b.center;
            markerCube.transform.rotation = Quaternion.identity;
            markerCube.transform.localScale = b.size;

            // 좌표계 변환 (WLT 사용 시)
            Vector3 pos = b.center;
            Quaternion rot = Quaternion.identity;
            if (useWorldLocking)
                pos = frozenFromLocked.MultiplyPoint3x4(pos);

            // 네트워크 큐브 실제 생성 (왼손과 동일 경로)
            if (spawnManager != null)
                spawnManager.AddPointFromPlaneDetector(pos, rot);
            else
                Debug.LogWarning("[HullClickable] spawnManager가 연결되지 않았습니다.");

            // [원래 코드] 투명 토글 머티리얼은 제거 → 프리팹 시각 그대로 사용
            // (프리팹과 동일 렌더링을 원하므로 더 이상 색상 토글/투명 처리 안함)

            toggled = !toggled;
        }

        eventData.Use();
    }

    public void OnPointerDown(MixedRealityPointerEventData eventData) { }
    public void OnPointerDragged(MixedRealityPointerEventData eventData) { }
    public void OnPointerUp(MixedRealityPointerEventData eventData) { }
}
