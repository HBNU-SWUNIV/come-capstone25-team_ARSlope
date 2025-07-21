using UnityEngine;
using Photon.Pun;
using Microsoft.MixedReality.WorldLocking.Core;
using TMPro;

[RequireComponent(typeof(PhotonView))]
public class SelectedObject : MonoBehaviourPunCallbacks
{
    public TextMeshPro text; // 텍스트 UI

    private void Start()
    {
        text.text = "Player" + photonView.Owner.ActorNumber + " Select"; // 선택한 플레이어 ID 표기

        // 내가 설치한 물체인 경우
        if (photonView.IsMine)
        {
            // 물체 위치 동기화 및 색상 설정
            photonView.RPC("SyncWorldLockedObject", RpcTarget.OthersBuffered, transform.position, transform.rotation);
            GetComponent<MeshRenderer>().material.color = Color.green;
            text.color = Color.green;
        }
        else
        {
            GetComponent<MeshRenderer>().material.color = Color.red;
            text.color = Color.red;
        }
    }

    // 물체 위치 고정 및 동기화
    [PunRPC]
    void SyncWorldLockedObject(Vector3 frozenPosition, Quaternion frozenRotation)
    {
        var manager = WorldLockingManager.GetInstance();

        if (manager == null || manager.FrozenFromLocked.rotation == Quaternion.identity || !AnchorTransferStatus.isAnchorImported)
        {
            Debug.LogWarning("WLT 초기화 또는 앵커 공유가 완료되지 않았습니다. 고정 취소.");
            return;
        }

        // 재고정 없이 위치만 설정 (Frozen 공간 기준)
        transform.SetPositionAndRotation(frozenPosition, frozenRotation);
    }
}
