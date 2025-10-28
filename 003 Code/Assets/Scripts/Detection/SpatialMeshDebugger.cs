using UnityEngine;

public class SpatialMeshDebugger : MonoBehaviour
{
    void Update()
    {
        var meshes = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
        foreach (var mf in meshes)
        {
            if (mf.gameObject.layer == LayerMask.NameToLayer("Spatial Awareness"))
            {
                Debug.Log($"[Mesh] {mf.name}: {mf.sharedMesh?.vertexCount ?? 0} vertices");
            }
        }
    }
}
