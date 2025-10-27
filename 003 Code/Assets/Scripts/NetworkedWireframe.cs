using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class NetworkedWireframe : MonoBehaviourPunCallbacks, IPunInstantiateMagicCallback
{
    [Tooltip("와이어프레임 머티리얼")]
    public Material lineMaterial;

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        var wf = GetComponent<WireframeMesh>();
        if (wf == null) wf = gameObject.AddComponent<WireframeMesh>();
        wf.lineMaterial = lineMaterial;
    }
}