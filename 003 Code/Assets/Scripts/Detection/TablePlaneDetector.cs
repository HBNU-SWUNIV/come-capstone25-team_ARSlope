// TablePlaneDetector.cs
// ------------------------------------------------------------
// 요구: "객체 탐지된 메시(컨벡스 Hull)만 표시하고, 다른 표시되는 것들 숨기기"
//      + "트리거 완료 이후에 컨벡스 Hull을 클릭가능하게" (클릭은 SpawnManager에서 처리)
// 설명:
//  1) [새 코드] showOnlyHulls=true 일 때, 모든 시각화/라인 표시 기능 비활성화
//  2) [새 코드] Hull 게임오브젝트를 별도 리스트(_spawnedHulls)로 관리하고
//               ClearHulls()로 따로 삭제 가능
//  3) [기존 코드] StartDetection() 호출 시 즉시 실행 (최소)
//  4) [새 코드] StartDetection()에 "메시 로드 완료 대기 후 실행" 코루틴
//  5) [새 코드] ClearDetectedVisuals(): 모든 표시물(라인/점/헐) 삭제
//     + ClearHulls(): 헐만 삭제
// ------------------------------------------------------------

using UnityEngine;
using System.Collections; // 코루틴
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.SpatialAwareness;
using Microsoft.MixedReality.Toolkit.SpatialAwareness.Processing;

public class TablePlaneDetector : MonoBehaviour
{
    public SpawnManager spawnManager;                 // 탐지된객체를 스폰하기위한 참조
    public bool useWorldLocking = false;              // WLT 사용시 true
    public Matrix4x4 frozenFromLocked = Matrix4x4.identity; // 월드락 좌표 변환 행렬

    [Header("PlaneFinding")]
    public float snapToGravityThreshold = 5f;
    public float minPlaneArea = 0.001f;

    [Header("Point collection (above plane, inside OBB)")]
    public float objectBandMin = 0.02f;
    public float objectBandMax = 0.18f;

    [Header("Grouping (객체 그룹을 분리)")]
    public bool enableGrouping = true;
    public float groupRadius = 0.08f;
    public int minGroupPoints = 5;

    [Header("Marker clustering (per-group)")]
    public bool useGridClustering = true;
    public float markerRadiusOrCell = 0.03f;
    public int minClusterPoints = 2;
    public float dedupEpsilon = 0.003f;
    public int maxMarkers = 1500;

    [Header("Marker & Hull style")]
    public float markerSize = 0.02f;
    public float markerLift = 0.015f;
    public Color markerColor = Color.red;
    public bool showHull = true;
    public float hullLineWidth = 0.01f;
    public Color hullColor = Color.green;

    [Header("Debug / Visuals")]
    public bool debugShowPlaneOutline = true;     // [기본 표시] 평면 윤곽
    public bool debugShowSamples = true;          // [기본 표시] 샘플 점
    public float sampleSize = 0.02f;
    public Transform debugParent;

    [Header("Logs")]
    public bool debugVerboseLog = true;

    [Header("Ceil/Floor rejection by camera height")]
    public float ceilingAboveCamera = 0.20f;
    public float floorBelowCamera = 0.60f;

    [Header("New Visibility Control")]
    public bool showOnlyHulls = true;             // [새 코드] true일 때 Hull만 표시 (라인/점 표시 안함)

    private bool hasRun = false;

    // [새 코드] 생성된 표시물/헐 관리
    private readonly List<GameObject> _spawnedVisuals = new List<GameObject>(); // 라인/점/헐 생성(시각화)
    private readonly List<GameObject> _spawnedHulls = new List<GameObject>(); // 헐(클릭 가능)

    // ------------------------------------------------------------------------
    // [기존 코드 - 주석]
    // public void StartDetection()
    // {
    //     Debug.Log("[PlaneDetector] 평면 탐지 시작 요청됨");
    //     RunPlaneDetection();
    // }
    // ------------------------------------------------------------------------

    // ------------------------------------------------------------------------
    // [새 코드] 메시가 로드될 때까지 대기 후 실행하는 평면 탐지
    // ------------------------------------------------------------------------
    public void StartDetection()
    {
        hasRun = false;               // 재실행 가능하도록 리셋
        StopAllCoroutines();          // 중복 실행 방지
        StartCoroutine(DetectWhenMeshesReady());
    }

    private IEnumerator DetectWhenMeshesReady()
    {
        Debug.Log("[PlaneDetector] 대기: 공간 메시 로드 중...");
        const float timeout = 8f;     // 필요 시 타임아웃
        const float poll = 0.25f;
        float t = 0f;

        while (t < timeout)
        {
            var dataAccess = CoreServices.SpatialAwarenessSystem as IMixedRealityDataProviderAccess;
            if (dataAccess != null)
            {
                var observers = dataAccess.GetDataProviders<IMixedRealitySpatialAwarenessMeshObserver>();
                if (observers != null && observers.Count > 0)
                {
                    int meshCount = 0;
                    foreach (var obs in observers) meshCount += obs.Meshes?.Count ?? 0;
                    if (meshCount > 0)
                    {
                        if (debugVerboseLog) Debug.Log($"[PlaneDetector] 로드된 메시 수: {meshCount}");
                        break; // 종료
                    }
                }
            }
            yield return new WaitForSeconds(poll);
            t += poll;
        }

        Debug.Log("[PlaneDetector] 평면 탐지 시작 요청됨");
        RunPlaneDetection();
    }
    // ------------------------------------------------------------------------

    void RunPlaneDetection()
    {
        if (hasRun) return;

        var dataAccess = CoreServices.SpatialAwarenessSystem as IMixedRealityDataProviderAccess;
        if (dataAccess == null) { Debug.LogWarning("[Markers] Spatial Awareness unavailable."); return; }

        var observers = dataAccess.GetDataProviders < IMixedRealitySpatialAwarenessMeshObserver > ();
        if (observers == null || observers.Count == 0) { Debug.Log("[Markers] No observers."); return; }

        var meshDataList = new List<PlaneFinding.MeshData>();
        foreach (var observer in observers)
        {
            foreach (var kvp in observer.Meshes)
            {
                if (kvp.Value.Filter && kvp.Value.Filter.sharedMesh)
                    meshDataList.Add(new PlaneFinding.MeshData(kvp.Value.Filter));
            }
        }

        if (meshDataList.Count == 0) { Debug.Log("[Markers] No meshes yet."); return; }

        hasRun = true;
        if (debugVerboseLog) Debug.Log($"[Markers] Meshes: {meshDataList.Count}. Finding planes...");

        var planes = PlaneFinding.FindPlanes(meshDataList, snapToGravityThreshold, minPlaneArea);
        if (debugVerboseLog) Debug.Log($"[Markers] Found planes: {planes.Length}");

        var cam = Camera.main;
        float camY = cam ? cam.transform.position.y : 1.5f;

        foreach (var plane in planes)
        {
            var bounds = plane.Bounds;
            Vector3 center = bounds.Center;
            Quaternion rot = bounds.Rotation;
            float extX = bounds.Extents.x;
            float extZ = bounds.Extents.y;
            Vector3 normal = plane.Plane.normal;

            // 테이블면(수평), 천장/바닥 제외
            if (Mathf.Abs(normal.y) <= 0.75f) continue;
            if (center.y > camY + ceilingAboveCamera) continue;
            if (center.y < camY - floorBelowCamera) continue;

            bool facingUp = Vector3.Dot(normal, Vector3.up) >= 0f;

            // [새 코드] Hull만 표시: 모든 윤곽/라인 표시 안함
            // [기존 코드]
            // if (debugShowPlaneOutline) DrawPlaneOutline(center, extX, extZ, 0.95f);
            if (!showOnlyHulls && debugShowPlaneOutline)
                DrawPlaneOutline(center, extX, extZ, 0.95f);

            List<Vector3> onTopPointsWorld = CollectPointsAbovePlaneInsideOBB(
                observers, center, rot, extX, extZ, objectBandMin, objectBandMax, facingUp
            );
            if (debugVerboseLog) Debug.Log($"[Markers] OnTop points: {onTopPointsWorld.Count}");
            if (onTopPointsWorld.Count == 0) continue;

            // [기존 코드]
            // if (debugShowSamples) { foreach (var pt in onTopPointsWorld) SpawnDot(pt, sampleSize, new Color(0.2f, 1f, 1f), rot, 0.01f); }
            if (!showOnlyHulls && debugShowSamples)
            {
                foreach (var pt in onTopPointsWorld)
                    SpawnDot(pt, sampleSize, new Color(0.2f, 1f, 1f), rot, 0.01f);
            }

            if (enableGrouping)
            {
                // 그룹별로 각 그룹마다 객체 Hull 생성
                DrawHullGroups(onTopPointsWorld, rot, groupRadius, minGroupPoints);
            }
            else
            {
                if (showHull && onTopPointsWorld.Count >= 3)
                    DrawHullGroups(onTopPointsWorld, rot, groupRadius, minGroupPoints);
            }
        }
    }

    // ---------- 유틸리티 ----------

    private static List<Vector3> CollectPointsAbovePlaneInsideOBB(
        IReadOnlyList<IMixedRealitySpatialAwarenessMeshObserver> observers,
        Vector3 center, Quaternion rotation,
        float extX, float extZ,
        float bandMin, float bandMax,
        bool facingUp)
    {
        var result = new List<Vector3>();
        var inv = Quaternion.Inverse(rotation);

        float ex = extX * 0.97f;
        float ez = extZ * 0.97f;

        bandMin = Mathf.Max(0.0f, bandMin);
        bandMax = Mathf.Max(bandMin + 0.005f, bandMax);

        foreach (var observer in observers)
        {
            foreach (var kvp in observer.Meshes)
            {
                var mf = kvp.Value.Filter;
                var mesh = mf.sharedMesh;
                if (!mesh) continue;

                var tf = mf.transform;
                var verts = mesh.vertices;

                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 wp = tf.TransformPoint(verts[i]);
                    Vector3 local = inv * (wp - center);
                    float height = facingUp ? local.y : -local.y;
                    if (height < bandMin || height > bandMax) continue;
                    if (Mathf.Abs(local.x) <= ex && Mathf.Abs(local.z) <= ez)
                        result.Add(wp);
                }
            }
        }
        return result;
    }

    private void DrawPlaneOutline(Vector3 center, float extX, float extZ, float scale)
    {
        GameObject lineObj = new GameObject($"Plane_Line_{center.y:F2}");
        _spawnedVisuals.Add(lineObj); // [새 코드] 시각화 리스트에 추가

        var lr = lineObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 5;
        lr.widthMultiplier = 0.02f;

        var mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = Color.yellow;
        lr.material = mat;

        Vector3 lift = Vector3.up * 0.005f;
        Vector3 e = new Vector3(extX * scale, 0f, extZ * scale);

        Vector3 p1 = center + new Vector3(-e.x, 0, -e.z) + lift;
        Vector3 p2 = center + new Vector3(-e.x, 0, e.z) + lift;
        Vector3 p3 = center + new Vector3(e.x, 0, e.z) + lift;
        Vector3 p4 = center + new Vector3(e.x, 0, -e.z) + lift;

        lr.SetPositions(new Vector3[] { p1, p2, p3, p4, p1 });
        if (debugParent) lineObj.transform.SetParent(debugParent, true);
    }

    private void SpawnDot(Vector3 pos, float size, Color color, Quaternion planeRot, float lift)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _spawnedVisuals.Add(go); // [새 코드] 시각화 리스트에 추가

        if (debugParent) go.transform.SetParent(debugParent, true);
        go.transform.position = pos + (planeRot * Vector3.up) * Mathf.Max(0f, lift);
        go.transform.localScale = Vector3.one * Mathf.Max(0.005f, size);

        var r = go.GetComponent<Renderer>();
        if (r) r.material.color = color;
        Destroy(go.GetComponent<Collider>());
    }

    // 그룹별 Hull MeshCollider 생성
    private void DrawHullGroups(List<Vector3> allPoints, Quaternion rot, float minDist, int minPts)
    {
        var groups = PointClusterer.ClusterGroups(allPoints, minDist, minPts);
        foreach (var group in groups)
        {
            if (group.Count < 3) continue;

            // 그룹 높이 계산
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            foreach (var p in group)
            {
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }
            float height = Mathf.Max(0.05f, maxY - minY);

            // Extruded Mesh 생성
            Mesh hullMesh = CreateExtrudedHull(group, height, rot);
            if (hullMesh == null) continue;

            var go = new GameObject("ObjectHull");
            _spawnedVisuals.Add(go);   // [새 코드] 시각화 리스트에 추가
            _spawnedHulls.Add(go);     // [새 코드] 헐 전용 리스트에도 추가

            if (debugParent) go.transform.SetParent(debugParent, true);

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            // [기존 코드] mr.material = new Material(Shader.Find("Unlit/Color")) { color = Color.green };
            // [새 코드] hullColor 필드 사용(기본 컬러)
            mr.material = new Material(Shader.Find("Unlit/Color")) { color = hullColor };
            mf.mesh = hullMesh;

            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = hullMesh;
            collider.convex = true;
            collider.isTrigger = false;

            // HullClickable 추가 및 설정
            var clickable = go.GetComponent<HullClickable>();
            if (clickable == null) clickable = go.AddComponent<HullClickable>();

            clickable.spawnManager = spawnManager;
            clickable.useWorldLocking = useWorldLocking;
            clickable.frozenFromLocked = frozenFromLocked;

            // 클릭 입력을 위한 Collider 확인(중복)
            if (go.GetComponent<Collider>() == null)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = hullMesh;
                mc.convex = true;
            }
        }
    }

    // Extruded Mesh 생성
    private Mesh CreateExtrudedHull(List<Vector3> verts, float height, Quaternion rot)
    {
        int n = verts.Count;
        if (n < 3) return null;

        Vector3 up = rot * Vector3.up * height;
        Vector3[] meshVerts = new Vector3[n * 2];
        for (int i = 0; i < n; i++)
        {
            meshVerts[i] = verts[i];
            meshVerts[i + n] = verts[i] + up;
        }

        List<int> tris = new List<int>();
        for (int i = 1; i < n - 1; i++)
        {
            tris.Add(0); tris.Add(i + 1); tris.Add(i);                // 아래면
            tris.Add(n); tris.Add(n + i); tris.Add(n + i + 1);        // 윗면
        }
        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            tris.Add(i); tris.Add(next); tris.Add(i + n);
            tris.Add(next); tris.Add(next + n); tris.Add(i + n);
        }

        Mesh mesh = new Mesh();
        mesh.vertices = meshVerts;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        return mesh;
    }

    // ------------------------------------------------------------
    // [새 코드] 외부에서 호출할 수 있는 공개 API
    //  - SpawnManager에서 트리거 완료 이후에 ClearHulls() 호출
    //  - (모든 표시물을 삭제하려면 ClearDetectedVisuals())
    // ------------------------------------------------------------
    public void ClearHulls()
    {
        foreach (var go in _spawnedHulls)
        {
            if (go != null) Destroy(go);
        }
        _spawnedHulls.Clear();

        if (debugVerboseLog) Debug.Log("[PlaneDetector] Hull(헐) 삭제 완료");
    }
    public void ClearDetectedVisuals()
    {
        foreach (var go in _spawnedVisuals)
            if (go != null) Destroy(go);
        _spawnedVisuals.Clear();
        _spawnedHulls?.Clear();

        //비활성화된 HullMarkerCube 객체도 모두 삭제 (활성/비활성 무관)
        var all = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < all.Length; i++)
        {
            var g = all[i];
            if (g == null) continue;
            var n = g.name;
            if (string.IsNullOrEmpty(n)) continue;

            // 이름으로 식별: "HullMarkerCube" 또는 "HullMarkerCube(Clone)" 등
            if (n.StartsWith("HullMarkerCube"))
            {
                // 비활성화/비활성화되어 찾아도, hideFlags로 설정되어 수 없음
                // (일반 게임오브젝트는 Destroy로 가능)
                Destroy(g);
            }
        }

        if (debugVerboseLog) Debug.Log("[PlaneDetector] 모든 표시(라인/점/헐/마커) 삭제 완료");
    }

}
