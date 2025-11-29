using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MultiMaterialSpline : MonoBehaviour
{
    [Header("Spline 연결")]
    public SplineContainer splineContainer;
    public SplineExtrude splineExtrude;

    [Header("머터리얼 설정")]
    public Material roadMat;
    public Material curbMat;

    [Header("설정값")]
    [Range(0f, 0.5f)] public float curbWidthRatio = 0.25f; // 반경 대비 커브 비율

    public void ApplyMultiMaterial()
    {
        var meshFilter = GetComponent<MeshFilter>();
        var mesh = meshFilter.sharedMesh;

        if (mesh == null)
        {
            Debug.LogError("❌ MeshFilter에 Mesh가 없습니다.");
            return;
        }

        if (splineContainer == null || splineExtrude == null)
        {
            Debug.LogError("❌ SplineContainer 또는 SplineExtrude가 연결되지 않았습니다.");
            return;
        }

        if (!mesh.isReadable)
        {
            Debug.LogError($"❌ {mesh.name}은 Read/Write 비활성 상태입니다.");
            return;
        }

        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;
        if (verts == null || tris == null || verts.Length == 0 || tris.Length == 0)
        {
            Debug.LogError("❌ Mesh 데이터가 비어 있습니다.");
            return;
        }

        // ✅ 도로 폭 정보
        float radius = splineExtrude.Radius;
        float curbStart = radius * curbWidthRatio;

        List<int> roadTris = new();
        List<int> curbTris = new();

        var spline = splineContainer.Spline;
        var tf = transform;

        for (int i = 0; i < tris.Length; i += 3)
        {
            int a = tris[i];
            int b = tris[i + 1];
            int c = tris[i + 2];

            Vector3 wa = tf.TransformPoint(verts[a]);
            Vector3 wb = tf.TransformPoint(verts[b]);
            Vector3 wc = tf.TransformPoint(verts[c]);
            Vector3 avg = (wa + wb + wc) / 3f;

            // ✅ 스플라인 중심선과의 거리 계산 (Ray 방식)
            SplineUtility.GetNearestPoint(
                spline,
                new Ray(avg + Vector3.up * 5f, Vector3.down),
                out float3 nearest,
                out float t
            );

            float distance = Vector3.Distance(avg, (Vector3)nearest);
            if (i % 500 == 0) Debug.Log($"거리={distance:F2}, 기준={curbStart:F2}");

            // ✅ 반경 기반 분리
            if (distance >= curbStart)
                curbTris.AddRange(new[] { a, b, c });
            else
                roadTris.AddRange(new[] { a, b, c });
        }

        mesh.subMeshCount = 2;
        mesh.SetTriangles(roadTris, 0);
        mesh.SetTriangles(curbTris, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var renderer = GetComponent<MeshRenderer>();
        renderer.materials = new[] { roadMat, curbMat };

        Debug.Log($"✅ SubMesh 분리 완료 (Ray+Radius기준) | Road: {roadTris.Count / 3}, Curb: {curbTris.Count / 3} | Radius={radius:F2}");
    }
}
