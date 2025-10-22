using System.Collections.Generic;
using UnityEngine;

public static class PointClusterer
{
    // 빠른 그리드(보xel) 중심점 클러스터
    public static List<Vector3> ClusterGrid(List<Vector3> points, float cell, int minPoints)
    {
        List<Vector3> centers = new();
        if (points == null || points.Count == 0) return centers;

        var bins = new Dictionary<(int gx, int gz), (Vector3 sum, int cnt)>();
        float inv = 1f / Mathf.Max(0.0001f, cell);

        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            int gx = Mathf.FloorToInt(p.x * inv);
            int gz = Mathf.FloorToInt(p.z * inv);
            var key = (gx, gz);

            if (!bins.TryGetValue(key, out var acc))
                acc = (Vector3.zero, 0);

            acc.sum += p;
            acc.cnt += 1;
            bins[key] = acc;
        }

        foreach (var kv in bins)
        {
            var acc = kv.Value;
            if (acc.cnt >= minPoints)
                centers.Add(acc.sum / acc.cnt);
        }

        return centers;
    }

    // 반경 기반(간단) 중심점 클러스터
    public static List<Vector3> Cluster(List<Vector3> points, float radius, int minPoints)
    {
        List<Vector3> centers = new();
        if (points == null || points.Count == 0) return centers;

        float r2 = radius * radius;
        HashSet<int> visited = new();

        for (int i = 0; i < points.Count; i++)
        {
            if (visited.Contains(i)) continue;

            var pivot = points[i];
            var cluster = new List<Vector3> { pivot };
            visited.Add(i);

            for (int j = 0; j < points.Count; j++)
            {
                if (i == j || visited.Contains(j)) continue;
                if ((points[j] - pivot).sqrMagnitude <= r2)
                {
                    cluster.Add(points[j]);
                    visited.Add(j);
                }
            }

            if (cluster.Count >= minPoints)
            {
                Vector3 center = Vector3.zero;
                for (int k = 0; k < cluster.Count; k++) center += cluster[k];
                centers.Add(center / cluster.Count);
            }
        }
        return centers;
    }

    // 물체 감지용: 포인트 묶음(클러스터) 자체 반환 (간단 DBSCAN 유사)
    public static List<List<Vector3>> ClusterGroups(List<Vector3> points, float radius, int minPoints)
    {
        var groups = new List<List<Vector3>>();
        if (points == null || points.Count == 0) return groups;

        float r2 = radius * radius;
        var visited = new bool[points.Count];

        for (int i = 0; i < points.Count; i++)
        {
            if (visited[i]) continue;

            var queue = new Queue<int>();
            var group = new List<Vector3>();
            queue.Enqueue(i);
            visited[i] = true;

            while (queue.Count > 0)
            {
                int idx = queue.Dequeue();
                Vector3 p = points[idx];
                group.Add(p);

                for (int j = 0; j < points.Count; j++)
                {
                    if (visited[j]) continue;
                    if ((points[j] - p).sqrMagnitude <= r2)
                    {
                        visited[j] = true;
                        queue.Enqueue(j);
                    }
                }
            }

            if (group.Count >= minPoints)
                groups.Add(group);
        }

        return groups;
    }
}
