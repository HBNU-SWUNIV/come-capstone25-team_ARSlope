using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(PhotonView))]
public class CarNetworkSync : MonoBehaviourPun, IPunObservable
{
    private Rigidbody rb;

    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private Vector3 networkVelocity;

    public float lerpRate = 15f;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        networkPosition = transform.position;
        networkRotation = transform.rotation;
        networkVelocity = rb.linearVelocity;
    }

    private void FixedUpdate()
    {
        if (!photonView.IsMine)
        {
            rb.position = Vector3.Lerp(rb.position, networkPosition, Time.fixedDeltaTime * lerpRate);
            rb.rotation = Quaternion.Lerp(rb.rotation, networkRotation, Time.fixedDeltaTime * lerpRate);
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, networkVelocity, Time.fixedDeltaTime * lerpRate);
        }
    }
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(rb.position);
            stream.SendNext(rb.rotation);
            stream.SendNext(rb.linearVelocity);
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            networkVelocity = (Vector3)stream.ReceiveNext();
        }
    }

    /// <summary>
    /// 네트워크 동기화 위치를 강제로 업데이트합니다. (게임 재시작 시 사용)
    /// </summary>
    public void ResetNetworkPosition(Vector3 position, Quaternion rotation)
    {
        networkPosition = position;
        networkRotation = rotation;
        networkVelocity = Vector3.zero;
        
        // 즉시 위치 적용 (보간 없이)
        if (rb != null)
        {
            rb.position = position;
            rb.rotation = rotation;
            rb.linearVelocity = Vector3.zero;
        }
        
        Debug.Log($"[CarNetworkSync] 네트워크 위치 리셋: {position}");
    }
}
