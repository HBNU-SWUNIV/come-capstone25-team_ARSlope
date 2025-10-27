using Microsoft.MixedReality.Toolkit;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime; // DisconnectCause를 사용하기 위해 추가

public class SceneNavigator : MonoBehaviourPunCallbacks
{
    // 게임 시작 시 공간인식 비활성화
    private void Start()
    {
        // 씬이 로드될 때마다 호출될 수 있으므로, 이 로직이 Title 씬에서는 필요 없는지 확인이 필요합니다.
        if (CoreServices.SpatialAwarenessSystem != null)
        {
            CoreServices.SpatialAwarenessSystem.Disable();
        }
    }

    public void LoadTitleScene()
    {
        SceneManager.LoadScene("Title");
    }
    
    public void LoadRacingScene()
    {
        SceneManager.LoadScene("Racing");
    }

    // 포톤 연결을 끊고 타이틀 씬으로 돌아가는 함수
    public void ResetAndLoadTitleScene()
    {
        // "완전 초기화"를 위해 추가적인 리셋 코드가 필요할 수 있습니다.
        // 예를 들어, 싱글톤으로 관리되는 게임 데이터나 매니저가 있다면 여기서 리셋해야 합니다.
        // 예: GameManager.Instance.ResetGameData();

        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }
        else
        {
            // 이미 연결이 끊겨있다면 바로 타이틀 씬 로드
            LoadTitleScene();
        }
    }

    // 포톤 서버와의 연결이 끊어졌을 때 호출되는 콜백 함수
    public override void OnDisconnected(DisconnectCause cause)
    {
        // 연결이 끊어졌으므로 타이틀 씬을 로드합니다.
        LoadTitleScene();
    }
}
