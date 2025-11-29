using UnityEngine;
using Photon.Pun;

/// <summary>
/// PhotonView의 IsMine이 변경될 때만 GameObject를 활성화/비활성화하는 컴포넌트
/// </summary>
public class PhotonMineOnly : MonoBehaviour
{
    [SerializeField] private bool useParentPhotonView = false;
    private PhotonView photonView;
    private bool previousIsMine = false;

    private void Awake()
    {
        if (useParentPhotonView)
        {
            photonView = GetComponentInParent<PhotonView>();
        }
        else
        {
            photonView = GetComponent<PhotonView>();
        }

        if (photonView == null)
        {
            Debug.LogError("PhotonMineOnly: PhotonView 컴포넌트를 찾을 수 없습니다!", this);
        }
    }

    private void OnEnable()
    {
        if (photonView != null)
        {
            previousIsMine = photonView.IsMine;
            gameObject.SetActive(photonView.IsMine);
        }
    }

    private void Update()
    {
        // IsMine 상태가 변경되었을 때만 활성화/비활성화
        if (photonView != null && previousIsMine != photonView.IsMine)
        {
            gameObject.SetActive(photonView.IsMine);
            previousIsMine = photonView.IsMine;
        }
    }
}

