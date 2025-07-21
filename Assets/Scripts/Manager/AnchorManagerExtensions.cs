using Microsoft.MixedReality.WorldLocking.Core;
using System.Threading.Tasks;
using UnityEngine;

public static class AnchorManagerExtensions
{
    public static async Task<bool> SaveAnchorsAsync(this IAnchorManager manager)
    {
        if (manager == null)
        {
            Debug.LogError("SaveAnchorsAsync: AnchorManager is null");
            return false;
        }

        await manager.SaveAnchors();
        Debug.Log("🔵 Anchors saved.");
        return true;
    }

    public static async Task<bool> LoadAnchorsAsync(this IAnchorManager manager)
    {
        if (manager == null)
        {
            Debug.LogError("LoadAnchorsAsync: AnchorManager is null");
            return false;
        }

        await manager.LoadAnchors();
        Debug.Log("🟢 Anchors loaded.");
        return true;
    }
}
