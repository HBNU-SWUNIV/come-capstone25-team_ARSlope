using Photon.Pun;
using UnityEngine;

// IPunInstantiateMagicCallback 인터페이스를 상속받습니다.
public class ItemVisual : MonoBehaviour, IPunInstantiateMagicCallback
{
    // OnPhotonInstantiate는 PhotonNetwork.Instantiate가 완료될 때 생성된 오브젝트에서 자동으로 호출됩니다.
    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        Debug.Log("OnPhotonInstantiate");
        // Instantiate의 마지막 인자로 보낸 데이터를 여기서 받습니다.
        // info.photonView.InstantiationData는 object[] 타입입니다.
        object[] data = info.photonView.InstantiationData;

        // 1. 크기(Scale) 설정
        if (data != null && data.Length > 0)
        {
            Vector3 scale = (Vector3)data[0]; // 데이터를 원래 타입으로 변환
            transform.localScale = scale;
        }

        // 2. 부모(Parent) 설정
        // 이 아이템을 생성한 플레이어(info.Sender)의 자동차를 찾아서 그 아래에 붙입니다.
        // info.Sender.ActorNumber는 플레이어의 고유 번호입니다.
        int ownerActorNr = info.Sender.ActorNumber;
        PhotonView ownerCarView = GetCarPhotonView(ownerActorNr); // 아래에서 만들 헬퍼 함수

        if (ownerCarView != null)
        {
            // 자동차 오브젝트에서 itemDisplayPoint를 찾습니다. 이름이나 태그로 찾을 수 있습니다.
            Transform displayPoint = ownerCarView.transform.Find("ItemDisplayPoint"); // 경로에 맞게 수정 필요
            if (displayPoint != null)
            {
                transform.SetParent(displayPoint, false);
                transform.localPosition = Vector3.zero; // 로컬 위치/회전 초기화
                transform.localRotation = Quaternion.identity;
            }
        }
    }

    // 액터 번호로 해당 플레이어의 자동차 PhotonView를 찾는 함수 (예시)
    private PhotonView GetCarPhotonView(int actorNr)
    {
        // 씬의 모든 PhotonView를 뒤져서 주인의 액터 번호가 일치하는 것을 찾습니다.
        foreach (var view in FindObjectsOfType<PhotonView>())
        {
            if (view.Owner != null && view.Owner.ActorNumber == actorNr)
            {
                // 찾은 오브젝트가 자동차인지 확인하는 로직 (예: 태그, 컴포넌트 확인)
                if (view.CompareTag("Player")) // 자동차 프리팹에 "Player" 태그를 붙여주세요.
                {
                    return view;
                }
            }
        }
        return null;
    }
}