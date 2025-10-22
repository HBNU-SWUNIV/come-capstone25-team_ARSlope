using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Microsoft.MixedReality.Toolkit;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    private void Start()
    {
        PhotonNetwork.ConnectUsingSettings(); // 포톤 기본 설정
    }

     public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Photon Master Server");
        // 최대 인원이 2명인 방 생성 또는 접속 (예진) MaxPlayers = 1로 바꿔놈 2가 원래.
        PhotonNetwork.JoinOrCreateRoom("TestRoom", new RoomOptions { MaxPlayers = 1 }, TypedLobby.Default);
    }
    public override void OnJoinedRoom()
    {
        Debug.Log("Joined Photon room");

    }

    private void Update()
    {
        //1인 테스트 코드
        if (!PhotonNetwork.InRoom)
            return;

        // 1인 테스트: 대기 UI를 전부 끄고, 개수 선택 UI만 켠다.
        UIManager.instance.PlayerWatingUI(false);
        UIManager.instance.ClientWatingUI(false);
        UIManager.instance.ObjectSelectUI(true);

        // NetworkManager는 더 이상 필요하지 않으므로 비활성화
        this.enabled = false;

        ////아래가 원본
        //// 방에 접속 중인 경우
        //if (PhotonNetwork.InRoom)
        //{
        //    // 접속 인원이 2명이 아닌 경우   
        //    if (PhotonNetwork.CurrentRoom.PlayerCount != 2)
        //    {
        //        UIManager.instance.PlayerWatingUI(true); // 플레이어 대기 UI 활성화
        //    }
        //    // 접속 인원이 2명인 경우
        //    else
        //    {
        //        UIManager.instance.PlayerWatingUI(false); // 플레이어 대기 UI 비활성화
        //        // 마스터인 경우
        //        if (PhotonNetwork.IsMasterClient)
        //        {
        //            UIManager.instance.ObjectSelectUI(true); // 오브젝트 선택 UI 활성화
        //        }
        //        else
        //        {
        //            UIManager.instance.ClientWatingUI(true); // 클라이언트 대기 UI 활성화
        //        }
        //        this.enabled = false; // 스크립트 비활성화
        //    }
        //}
    }
}
