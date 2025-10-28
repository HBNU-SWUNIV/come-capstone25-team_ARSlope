using System.Collections;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.WorldLocking.Core;
using Photon.Pun;
using Photon.Realtime; // DisconnectCause를 사용하기 위해 추가
using UnityEngine;
using UnityEngine.SceneManagement;

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
        // GetInstance()로 싱글톤 인스턴스를 가져온 뒤 Reset() 메서드를 호출합니다.
        try
        {
            WorldLockingManager.GetInstance().Reset();
            Debug.Log("World Locking Tools 상태를 리셋했습니다.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"WorldLockingManager.GetInstance().Reset() 호출 중 예외 발생: {e.Message}");
        }

        // MRTK 공간 인식 시스템도 리셋
        if (CoreServices.SpatialAwarenessSystem != null)
        {
            CoreServices.SpatialAwarenessSystem.Disable();
            CoreServices.SpatialAwarenessSystem.Reset();
        }

        // (이하 기존 코드)
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }
        else
        {
            // 이미 연결이 끊겨있다면 바로 타이틀 씬 로드
            StartCoroutine(LoadTitleSceneRoutine());
        }
    }

    // 포톤 서버와의 연결이 끊어졌을 때 호출되는 콜백 함수
    public override void OnDisconnected(DisconnectCause cause)
    {
        // GetInstance()로 싱글톤 인스턴스를 가져온 뒤 Reset() 메서드를 호출합니다.
        try
        {
            WorldLockingManager.GetInstance().Reset();
            Debug.Log("World Locking Tools 상태를 리셋했습니다.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"WorldLockingManager.GetInstance().Reset() 호출 중 예외 발생: {e.Message}");
        }

        // 연결이 끊어졌으므로 타이틀 씬을 로드합니다.
        StartCoroutine(LoadTitleSceneRoutine());
    }
    
    private IEnumerator LoadTitleSceneRoutine()
    {
        // 현재 프레임의 모든 OnDisable, 이벤트 처리가 완료될 때까지 대기
        yield return new WaitForEndOfFrame();

        // 다음 프레임에 안전하게 씬 로드
        LoadTitleScene();
    }
}
