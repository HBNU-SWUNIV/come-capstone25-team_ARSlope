using Photon.Pun;
using UnityEngine;
using Microsoft.MixedReality.Toolkit.Utilities.Solvers;

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
            car.SetMovementEnabled(false);
            var indicator = car.GetComponentInChildren<DirectionalIndicator>();
            if (indicator != null)
            {
                indicator.enabled = false;
                var meshRenderer = indicator.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.enabled = false;
                }
            }
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

    /// <summary>게임 재시작 요청 (클라이언트 → 마스터)</summary>
    public void RequestRestartGame()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            RestartGame();
        }
        else
        {
            photonView.RPC(nameof(RPC_RequestRestartGame), RpcTarget.MasterClient);
        }
    }

    /// <summary>클라이언트 → 마스터 : 재시작 요청</summary>
    [PunRPC]
    private void RPC_RequestRestartGame()
    {
        RestartGame();
    }

    /// <summary>마스터가 직접 호출 : 게임 재시작</summary>
    public void RestartGame()
    {
        Debug.Log("[RaceManager] ▶ 게임 재시작 시작");
        photonView.RPC(nameof(RPC_RestartGame), RpcTarget.All);
    }

    /// <summary>모든 클라이언트에서 실행 : 실제 게임 재시작</summary>
    [PunRPC]
    private void RPC_RestartGame()
    {
        Debug.Log("[RaceManager] ▶ RPC_RestartGame 수신");

        // 게임 종료 상태 리셋
        gameEnded = false;

        // GameoverUI 비활성화
        if (UIManager.instance != null)
        {
            if (UIManager.instance.gameoverUI != null)
            {
                UIManager.instance.gameoverUI.SetActive(false);
            }
        }

        // 모든 차량 초기화
        CarMove[] allCars = FindObjectsOfType<CarMove>();
        foreach (CarMove car in allCars)
        {
            car.ResetCar();
            var indicator = car.GetComponentInChildren<DirectionalIndicator>();
            if (indicator != null)
            {
                indicator.enabled = true;
                var meshRenderer = indicator.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.enabled = true;
                }
            }
        }

        // 아이템 재생성 (마스터만)
        if (PhotonNetwork.IsMasterClient)
        {
            ItemSpawner itemSpawner = FindObjectOfType<ItemSpawner>();
            if (itemSpawner != null)
            {
                Debug.Log("[RaceManager] 아이템 재생성 시작");
                itemSpawner.SpawnItemsWithDelay();
            }

            ObstacleSpawner obstacleSpawner = FindObjectOfType<ObstacleSpawner>();
            if (obstacleSpawner != null)
            {
                Debug.Log("[RaceManager] 장애물 재생성 시작");
                obstacleSpawner.ClearAndRespawnObstacles();
            }
        }
    }
}
