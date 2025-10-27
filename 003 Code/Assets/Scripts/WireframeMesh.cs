using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WireframeMesh : MonoBehaviour
{
    [Tooltip("와이어프레임 머티리얼")]
    public Material lineMaterial;
    
    [Tooltip("면-면 간 최소 각도 (Degree)")] 
    [Range(0, 45)]
    public float minEdgeAngle = 1f;  // 이 각도보다 작은 평면 엣지는 건너뛰기

    void Awake()
    {
        var mf = GetComponent<MeshFilter>();
        var original = mf.sharedMesh;
        mf.mesh = GenerateFilteredWireframe(original);
        GetComponent<MeshRenderer>().material = lineMaterial;
    }

    Mesh GenerateFilteredWireframe(Mesh src)
    {
        var verts = src.vertices;
        var tris  = src.triangles;
        int triCount = tris.Length / 3;

        // 1) 각 삼각형 법선 계산
        var faceNormals = new Vector3[triCount];
        for (int t = 0; t < triCount; t++)
        {
            int i0 = tris[t*3 + 0];
            int i1 = tris[t*3 + 1];
            int i2 = tris[t*3 + 2];
            Vector3 v0 = verts[i1] - verts[i0];
            Vector3 v1 = verts[i2] - verts[i0];
            faceNormals[t] = Vector3.Cross(v0, v1).normalized;
        }

        // 2) 엣지 → 인접 삼각형 리스트 매핑
        var edgeMap = new Dictionary<ulong, List<int>>();
        for (int t = 0; t < triCount; t++)
        {
            int i0 = tris[t*3 + 0];
            int i1 = tris[t*3 + 1];
            int i2 = tris[t*3 + 2];
            AddEdgeKey(i0, i1, t, edgeMap);
            AddEdgeKey(i1, i2, t, edgeMap);
            AddEdgeKey(i2, i0, t, edgeMap);
        }

        // 3) 실제 라인으로 뽑아낼 엣지만 필터링
        float cosThreshold = Mathf.Cos(minEdgeAngle * Mathf.Deg2Rad);
        var wVerts = new List<Vector3>();
        var wInds  = new List<int>();

        foreach (var kv in edgeMap)
        {
            var adj = kv.Value;
            if (adj.Count < 2)
            {
                // 경계 엣지(메쉬가 열려있는 경우)라면 그냥 포함
            }
            else
            {
                // 두 삼각형 법선의 내적이 거의 1이면 같은 평면
                var n0 = faceNormals[adj[0]];
                var n1 = faceNormals[adj[1]];
                if (Vector3.Dot(n0, n1) > cosThreshold)
                    continue;   // 같은 면 위의 대각선 엣지이므로 스킵
            }

            // 키에서 실제 정점 인덱스 복원
            int a = (int)(kv.Key >> 32);
            int b = (int)(kv.Key & 0xFFFFFFFF);

            // 라인으로 추가
            wVerts.Add(verts[a]);
            wVerts.Add(verts[b]);
            int idx = wVerts.Count;
            wInds.Add(idx - 2);
            wInds.Add(idx - 1);
        }

        // 4) Mesh로 만들어서 반환
        var m = new Mesh();
        m.SetVertices(wVerts);
        m.SetIndices(wInds.ToArray(), MeshTopology.Lines, 0);
        return m;
    }

    void AddEdgeKey(int i, int j, int triIndex, Dictionary<ulong, List<int>> map)
    {
        // 언더다(작은), 오버다(큰) 조합으로 키 생성
        uint min = (uint)Mathf.Min(i, j);
        uint max = (uint)Mathf.Max(i, j);
        ulong key = ((ulong)min << 32) | max;

        if (!map.TryGetValue(key, out var list))
        {
            list = new List<int>();
            map[key] = list;
        }
        list.Add(triIndex);
    }
}
