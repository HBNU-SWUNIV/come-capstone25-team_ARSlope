using UnityEngine;
using Photon.Pun;
using System.Threading.Tasks;
using Microsoft.MixedReality.WorldLocking.Core;

public static class AnchorTransferStatus
{
    public static bool isAnchorImported = false; // 앵커 공유 여부
}

public class AnchorTransferManager : MonoBehaviourPunCallbacks
{
    private IAnchorManager anchorManager => WorldLockingManager.GetInstance()?.AnchorManager;

    public override async void OnJoinedRoom()
    {
        await WaitForAnchorSystemReady();

        if (PhotonNetwork.IsMasterClient)
        {
            await anchorManager.SaveAnchorsAsync();

            // 상대방에게 저장 완료 신호 전송
            AnchorTransferStatus.isAnchorImported = true;
            photonView.RPC("ReceiveAnchorSignal", RpcTarget.OthersBuffered);
        }
    }

    private async Task WaitForAnchorSystemReady()
    {
        int tries = 0;
        while ((anchorManager == null || WorldLockingManager.GetInstance()?.Plugin == null) && tries < 30)
        {
            await Task.Delay(500);
            tries++;
        }
    }

    [PunRPC]
    private async void ReceiveAnchorSignal()
    {
        await WaitForAnchorSystemReady();

        bool result = await anchorManager.LoadAnchorsAsync();
        if (result)
        {
            AnchorTransferStatus.isAnchorImported = true;
        }
    }
}
