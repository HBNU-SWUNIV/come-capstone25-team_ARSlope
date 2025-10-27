using Photon.Pun;
using UnityEngine;

public class RaceManager : MonoBehaviourPun
{
    private bool gameEnded = false;

    /// <summary>마스터가 직접 호출(또는 RPC로 요청받아 호출)</summary>
    public void DeclareWinner(int actorNumber)
    {
        if (gameEnded) return;

        Debug.Log($"[RaceManager] ▶ 플레이어 {actorNumber} 우승 선언");
        photonView.RPC(nameof(GameOver), RpcTarget.All, actorNumber);
        gameEnded = true;
    }

    /// <summary>클라이언트 → 마스터 : 우승 요청</summary>
    [PunRPC]
    private void RPC_RequestDeclareWinner(int actorNumber)
        => DeclareWinner(actorNumber);

    /// <summary>모든 클라이언트에서 실행 : 실제 게임 종료</summary>
    [PunRPC]
    private void GameOver(int winnerActorNumber)
    {
        if (gameEnded) return;
        gameEnded = true;

        Debug.Log($"[RaceManager] ▶ GameOver RPC 수신, 우승자: {winnerActorNumber}");
        // Time.timeScale = 0f; // 경고: 이 코드는 홀로렌즈 입력 시스템을 포함한 전체 게임 시간을 멈추므로 예기치 않은 문제를 일으킬 수 있습니다.

        // 모든 자동차의 움직임을 멈춥니다.
        CarMove[] allCars = FindObjectsOfType<CarMove>();
        foreach (CarMove car in allCars)
        {
            car.StopMovement();
        }

        // 자신이 우승자인지 확인
        bool isWinner = PhotonNetwork.LocalPlayer.ActorNumber == winnerActorNumber;
        
        // UIManager를 통해 게임 오버 UI 표시
        if (UIManager.instance != null)
        {
            UIManager.instance.GameoverUI(isWinner);
        }
        else
        {
            Debug.LogError("[RaceManager] UIManager.instance가 null입니다!");
        }
    }
}
