using UnityEngine;
using UnityEngine.SceneManagement;

public class IntroManager : MonoBehaviour
{
    [Tooltip("빌드 세팅(Build Settings)에 추가된 다음 씬 이름")]
    public string nextSceneName;

    void Start()
    {
        Invoke(nameof(LoadNextScene), 1.5f);
    }

    void LoadNextScene()
    {
        SceneManager.LoadScene(nextSceneName);
    }
}
