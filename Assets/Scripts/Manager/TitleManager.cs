using Microsoft.MixedReality.Toolkit;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleManager : MonoBehaviour
{
    // 게임 시작 시 공간인식 비활성화
    private void Start()
    {
        CoreServices.SpatialAwarenessSystem.Disable();
    }

    // 게임 시작
    public void StartGame()
    {
        SceneManager.LoadScene("RacingScene");
    }
}
