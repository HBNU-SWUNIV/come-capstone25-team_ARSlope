using UnityEngine;

public class RaceManager : MonoBehaviour
{
    private bool gameEnded;
    private float lapProgress;
    private float previousProgress;

    // 게임 종료 처리
    public void UpdateLapProgress(float progress)
    {
        if (gameEnded) return;

        // 누적 진행도 계산
        var deltaProgress = progress - previousProgress;
        if (deltaProgress < -0.5f) deltaProgress += 1f;
        else if (deltaProgress > 0.5f) deltaProgress -= 1f;

        lapProgress += deltaProgress;
        previousProgress = progress;

        if (!gameEnded && lapProgress >= 2f) //바퀴수숫자
        {
            GameOver();
            gameEnded = true;
        }

        Debug.Log($"현재 진행도(progress): {progress:F3}, 변화량(delta): {deltaProgress:F3}, 누적(lap): {lapProgress:F3}");
    }

    private void GameOver()
    {
        Debug.Log("게임 종료!");
        Time.timeScale = 0f;
    }
}