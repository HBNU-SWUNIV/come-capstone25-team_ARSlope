using UnityEngine;
using System.Collections;
using Photon.Pun;
using Photon.Realtime; // CustomProperties 사용을 위해 추가
using ExitGames.Client.Photon; // CustomProperties 사용을 위해 추가

// 이 스크립트는 PhotonView 컴포넌트와 함께 차량 오브젝트에 붙어있어야 합니다.
[RequireComponent(typeof(PhotonView))]
public class ItemEffectHandler : MonoBehaviourPunCallbacks // MonoBehaviourPun 대신 Callbacks 상속
{
    private CarMove carMove;
    private RaceManager raceManager;

    // private int goldCount = 0; // -> CustomProperties로 대체
    private bool isInvincible = false;
    private Collider myCollider;

    [Header("시각적 아이템 표시 위치")]
    public Transform itemDisplayPoint;
    private GameObject currentItemVisual;

    [Header("아이템 프리팹")]
    public GameObject crownPrefab;
    public GameObject goldPrefab;
    public GameObject boosterPrefab;
    public GameObject bombPrefab; // 중요: 이 프리팹은 PhotonView와 PhotonTransformView가 있어야 합니다.
    public GameObject oilRedPrefab;
    public GameObject oilGreenPrefab;
    public GameObject hookPrefab;

    [Header("폭탄 / 갈고리 설정")]
    public float bombThrowForce = 30f; // (원본 12 * 2.5)
    public float bombExplosionRadius = 3f;
    public float hookPullSpeed = 10f;

    [Header("폭발 연출 설정")]
    // public GameObject explosionEffectPrefab; // 폭발 파티클
    // public AudioClip explosionSound;         // 폭발 사운드

    // 플레이어 커스텀 속성 키
    public const string GOLD_COUNT_KEY = "Gold";

    private void Start()
    {
        carMove = GetComponent<CarMove>();
        myCollider = GetComponent<Collider>();
        raceManager ??= FindAnyObjectByType<RaceManager>();

        if (carMove == null)
            Debug.LogError("CarMove 컴포넌트를 찾을 수 없습니다!");

        // 로컬 플레이어인 경우에만 금괴 수 초기화
        if (photonView.IsMine)
        {
            SetGold(0);
        }
    }

    #region 아이템 표시 (네트워크)

    // 아이템을 차 위에 표시 (모든 플레이어에게 보임)
    void ShowItemOnCar(GameObject itemPrefab, float duration)
    {
        if (currentItemVisual != null)
        {
            PhotonNetwork.Destroy(currentItemVisual);
        }

        // 차량 크기에 맞춰 아이템 스케일 조절 (원본 로직)
        var itemScale = carMove.GetSize() * 10f;

        // 네트워크를 통해 아이템 시각 효과 생성
        currentItemVisual = PhotonNetwork.Instantiate(
            itemPrefab.name,
            itemDisplayPoint.position,
            itemDisplayPoint.rotation,
            0,
            new object[] { itemScale } // InstantiationData로 스케일 전달 (프리팹에 스케일 동기화 로직 필요)
        );

        // 생성된 아이템을 내 차의 자식으로 설정 (위치 고정)
        currentItemVisual.transform.parent = itemDisplayPoint;

        // 표시 시간 후 자동 제거 (마스터 클라이언트가 파괴하도록 예약)
        StartCoroutine(RemoveItemVisualAfter(duration));
    }

    IEnumerator RemoveItemVisualAfter(float delay)
    {
        yield return new WaitForSeconds(delay);

        // 내가 생성한 아이템이거나, 마스터 클라이언트인 경우에만 파괴 권한을 가짐
        if (currentItemVisual != null && currentItemVisual.GetComponent<PhotonView>().IsMine)
        {
            PhotonNetwork.Destroy(currentItemVisual);
            currentItemVisual = null;
        }
    }

    #endregion

    #region 아이템 효과 적용 (메인 로직)

    // 이 함수는 내 차가 아이템을 먹었을 때 로컬에서만 호출되어야 합니다.
    // (예: if (photonView.IsMine) { itemHandler.ApplyItemEffect(); })
    public void ApplyItemEffect()
    {
        int random = Random.Range(0, 100);
        Debug.Log($"[{photonView.Owner.NickName}] 랜덤 값: {random}");

        if (random < 15)
        {
            Debug.Log("🔴 기름통 (빨강) 발동!");
            ApplyOilEffect(0.9f);
            ShowItemOnCar(oilRedPrefab, 3f);
        }
        else if (random < 30)
        {
            Debug.Log("🟢 기름통 (초록) 발동!");
            ApplyOilEffect(1.1f);
            ShowItemOnCar(oilGreenPrefab, 3f);
        }
        else if (random < 50)
        {
            Debug.Log("💣 폭탄 발동!");
            UseBomb(); // 로컬에서 폭탄 발사
            ShowItemOnCar(bombPrefab, 3f);
        }
        else if (random < 70)
        {
            Debug.Log("👑 왕관 발동!");
            // 무적 효과는 모든 클라이언트에 전파되어야 함
            photonView.RPC("RPC_SetInvincibility", RpcTarget.All, true);
            // 일정 시간 후 무적 해제 RPC 예약
            StartCoroutine(ResetInvincibility(3f));
            ShowItemOnCar(crownPrefab, 3f);
        }
        else if (random < 85)
        {
            Debug.Log("⚡ 부스터 발동!");
            StartCoroutine(ApplySpeedBoost(1.5f)); // 로컬에서만 속도 변경
            ShowItemOnCar(boosterPrefab, 1.5f);
        }
        else if (random < 95)
        {
            Debug.Log("💰 금괴 획득!");
            CollectGold(); // 커스텀 속성을 이용해 금괴 획득
            ShowItemOnCar(goldPrefab, 3f);
        }
        else
        {
            Debug.Log("🪝 갈고리 발동!");
            UseHook(); // 로컬에서 갈고리 발사
            ShowItemOnCar(hookPrefab, 3f);
        }
    }

    #endregion

    #region 개별 아이템 로직 (PUN 적용)

    // --- 폭탄 ---

    // (로컬) 폭탄 사용
    public void UseBomb()
    {
        GameObject opponent = FindClosestOpponent();
        if (bombPrefab == null || opponent == null)
        {
            Debug.LogWarning("폭탄 프리팹 또는 상대가 없습니다!");
            return;
        }

        Vector3 spawnPos = transform.position + transform.forward * 1.0f + Vector3.up * 0.3f;

        // 폭탄을 네트워크에 생성 (중요: bombPrefab에 PhotonView, PhotonTransformView 필요)
        GameObject bomb = PhotonNetwork.Instantiate(bombPrefab.name, spawnPos, Quaternion.identity);

        Rigidbody rb = bomb.GetComponent<Rigidbody>();
        if (rb == null) rb = bomb.AddComponent<Rigidbody>();

        // 폭탄의 물리 설정 (발사자 클라이언트에서만 설정)
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        Vector3 targetPos = opponent.transform.position + Vector3.up * 0.1f;
        Vector3 dir = (targetPos - spawnPos).normalized;
        rb.linearVelocity = dir * bombThrowForce;
        bomb.transform.rotation = Quaternion.LookRotation(dir);

        // 폭탄 추적 및 폭발 코루틴은 발사자(마스터 클라이언트 또는 소유자)만 실행
        StartCoroutine(TrackAndExplode(bomb, opponent));

        Debug.Log($"💣 폭탄 발사! → 목표: {opponent.name}");
    }

    // (로컬) 폭탄 추적 코루틴
    private IEnumerator TrackAndExplode(GameObject bomb, GameObject targetOpponent)
    {
        float minDistance = 2.5f;
        float timeout = 6f;
        float elapsed = 0f;

        // 이 코루틴은 폭탄의 소유자(발사자)만 실행
        while (bomb != null && targetOpponent != null)
        {
            elapsed += Time.deltaTime;

            Vector3 targetPos = targetOpponent.transform.position + Vector3.up * 0.1f;
            Vector3 dir = (targetPos - bomb.transform.position).normalized;

            Rigidbody rb = bomb.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // PhotonTransformView가 이 속도를 다른 클라이언트에 동기화
                rb.linearVelocity = dir * bombThrowForce;
                rb.rotation = Quaternion.LookRotation(dir);
            }

            float dist = Vector3.Distance(bomb.transform.position, targetOpponent.transform.position);
            if (dist <= minDistance || elapsed >= timeout)
            {
                ExplodeNow(bomb, targetOpponent);
                yield break;
            }

            yield return null;
        }

        // 타겟이 사라지면 그냥 폭발
        if (bomb != null)
        {
            ExplodeNow(bomb, null);
        }
    }

    // (로컬) 폭발 처리
    private void ExplodeNow(GameObject bomb, GameObject directTarget)
    {
        if (bomb == null) return;

        Vector3 explosionPos = bomb.transform.position;

        // 1. 모든 클라이언트에게 폭발 시각/청각 효과를 보여주도록 RPC 전송
        photonView.RPC("RPC_ShowExplosion", RpcTarget.All, explosionPos);

        // 2. 폭발 범위 내 상대방에게 데미지 RPC 전송 (마스터 클라이언트나 발사자가 판정)
        Collider[] hits = Physics.OverlapSphere(explosionPos, bombExplosionRadius);

        foreach (var col in hits)
        {
            // 상대방 차량인지 확인 (자신 제외)
            var opponentHandler = col.GetComponent<ItemEffectHandler>();
            if (opponentHandler != null && opponentHandler != this)
            {
                // 3. 상대방이 무적인지 *로컬*에서 확인 (isInvincible은 RPC로 동기화됨)
                if (opponentHandler.IsInvincible())
                {
                    Debug.Log($" {col.name} 무적 상태! 폭탄 효과 무시");
                    continue;
                }

                Debug.Log($" {col.name} 폭탄 피격!");

                // 4. 상대방 클라이언트에게 "너 맞았어"라고 RPC 전송
                opponentHandler.photonView.RPC("RPC_HitByBomb", RpcTarget.All); // RpcTarget.All로 보내도 RPC 내부에서 IsMine으로 거름
            }
        }

        // 5. 폭탄 오브젝트 제거 (발사자만)
        PhotonNetwork.Destroy(bomb);
        Debug.Log("💣 폭탄 폭발 완료!");
    }

    // (RPC) 모든 클라이언트에서 폭발 효과 재생
    [PunRPC]
    private void RPC_ShowExplosion(Vector3 position)
    {
        // if (explosionEffectPrefab != null)
        //     Instantiate(explosionEffectPrefab, position, Quaternion.identity);

        // if (explosionSound != null)
        //     AudioSource.PlayClipAtPoint(explosionSound, position);
    }

    // (RPC) 폭탄에 맞은 클라이언트에서만 실행됨
    [PunRPC]
    private void RPC_HitByBomb()
    {
        // 이 RPC를 받은 클라이언트 중, 자신의 차인 경우에만 로직 실행
        if (photonView.IsMine)
        {
            Debug.Log($"[{photonView.Owner.NickName}] 폭탄에 맞았습니다!");

            // 물리 반응 (위로 튕기기 + 회전)
            if (TryGetComponent<Rigidbody>(out var rb))
            {
                // 폭발 위치가 없으므로 대략적인 방향 설정
                Vector3 forceDir = (Vector3.up * 0.7f) + (Random.insideUnitSphere * 0.3f);
                rb.AddForce(forceDir * 1500f, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 300f, ForceMode.Impulse);
            }

            // 2초간 조작 불가
            StartCoroutine(DisableControlTemporarily(2f));
        }
    }


    // --- 갈고리 ---

    // (로컬) 갈고리 사용
    public void UseHook()
    {
        GameObject opponent = FindClosestOpponent();
        if (opponent == null)
        {
            Debug.LogWarning("상대 차량이 없습니다!");
            return;
        }

        Debug.Log($"🪝 {opponent.name}에게 갈고리 발사!");

        // 상대방 클라이언트에게 "나에게 끌려와"라고 RPC 전송
        // 내 ViewID를 매개변수로 넘겨줌
        opponent.GetComponent<PhotonView>().RPC("RPC_GetPulled", RpcTarget.All, photonView.ViewID);
    }

    // (RPC) 갈고리에 맞은 클라이언트에서 실행됨
    [PunRPC]
    private void RPC_GetPulled(int pullerID)
    {
        // 이 RPC를 받은 클라이언트 중, 자신의 차인 경우에만 로직 실행
        if (photonView.IsMine)
        {
            PhotonView pullerView = PhotonView.Find(pullerID);
            if (pullerView != null)
            {
                Debug.Log($"[{photonView.Owner.ActorNumber}] {pullerView.Owner.ActorNumber}의 갈고리에 맞았습니다!");
                StartCoroutine(PullMyselfTo(pullerView.gameObject));
            }
        }
    }

    // (로컬 코루틴) 갈고리에 맞은 쪽(피해자)이 자신을 끌어당긴 쪽(가해자)에게 이동
    IEnumerator PullMyselfTo(GameObject puller)
    {
        if (puller == null) yield break;
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) yield break;

        rb.isKinematic = false;
        rb.useGravity = true;

        float duration = 1.0f;
        float elapsed = 0f;

        Vector3 startPos = transform.position;

        Debug.Log($"🪝 {puller.name}에게 끌려가기 시작");
        rb.isKinematic = true;
        while (elapsed < duration && puller != null)
        {
            // 목표 위치: 끌어당기는 차의 바로 뒤쪽
            Vector3 endPos = puller.transform.position - puller.transform.forward * 2.5f;

            float t = elapsed / duration;
            float smoothT = Mathf.SmoothStep(0, 1, t);

            // 위치 보간 이동 (PhotonTransformView가 동기화)
            Vector3 newPos = Vector3.Lerp(startPos, endPos, smoothT);
            rb.MovePosition(newPos);

            // 회전은 끌어당기는 쪽을 바라보도록 유지
            Vector3 dir = (puller.transform.position - transform.position).normalized;
            if (dir != Vector3.zero)
                rb.MoveRotation(Quaternion.LookRotation(dir));

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 최종 위치 정렬
        if (puller != null)
        {
            Vector3 finalPos = puller.transform.position - puller.transform.forward * 2.5f;
            rb.MovePosition(finalPos);
        }
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Debug.Log($"🪝 끌려가기 완료!");
        rb.isKinematic = false;
    }


    // --- 기름통 ---
    // (로컬) 속도만 변경. PhotonTransformView가 변경된 속도에 따른 위치를 동기화.
    void ApplyOilEffect(float multiplier)
    {
        carMove.speed *= multiplier;
    }

    // --- 부스터 ---
    // (로컬)
    IEnumerator ApplySpeedBoost(float duration)
    {
        float originalSpeed = carMove.speed;

        carMove.speed *= 1.5f;

        yield return new WaitForSeconds(duration);

        carMove.speed = originalSpeed;
    }

    // --- 왕관 (무적) ---
    // (로컬) 무적 해제 타이머
    IEnumerator ResetInvincibility(float duration)
    {
        yield return new WaitForSeconds(duration);
        // 시간이 다 되면 모든 클라이언트에 무적 해제 RPC 전송
        photonView.RPC("RPC_SetInvincibility", RpcTarget.All, false);
    }

    // (RPC) 모든 클라이언트에서 무적 상태를 동기화
    [PunRPC]
    void RPC_SetInvincibility(bool state)
    {
        isInvincible = state;
        Debug.Log($"[{photonView.Owner.NickName}] 무적 상태 변경: {state}");

        // 물리적 충돌 무시 로직은 각 클라이언트에서 로컬로 처리
        if (myCollider == null) return;

        foreach (var obs in GameObject.FindGameObjectsWithTag("Obstacle"))
        {
            Collider obsCol = obs.GetComponent<Collider>();
            if (obsCol != null)
            {
                Physics.IgnoreCollision(myCollider, obsCol, state);
            }
        }
    }

    // --- 금괴 ---
    // (로컬) 금괴 획득 시 커스텀 속성 변경
    void CollectGold()
    {
        // 로컬 플레이어(IsMine)만 자기 점수를 올릴 수 있음
        if (!photonView.IsMine) return;

        int currentGold = photonView.Owner.GetGold();
        currentGold++;

        // 변경된 금괴 수를 네트워크에 동기화 (모든 플레이어가 알 수 있음)
        SetGold(currentGold);

        Debug.Log($"[{photonView.Owner.NickName}] 현재 금괴 수: {currentGold}/5");

        if (currentGold >= 5)
        {
            Debug.Log("🎉 금괴 5개! 게임 승리!");

            carMove.finished = true;

            if (PhotonNetwork.IsMasterClient)
            {
                raceManager.DeclareWinner(PhotonNetwork.LocalPlayer.ActorNumber);
            }
            else
            {
                raceManager.photonView.RPC("RPC_RequestDeclareWinner", RpcTarget.MasterClient, PhotonNetwork.LocalPlayer.ActorNumber);
            }
        }
    }

    #endregion

    #region 유틸리티 메서드

    // (로컬) 무적 상태 확인
    public bool IsInvincible()
    {
        return isInvincible;
    }

    // (로컬 코루틴) 일정 시간 조작 불가
    private IEnumerator DisableControlTemporarily(float duration)
    {
        if (carMove == null) yield break;

        carMove.enabled = false;
        Debug.Log($"{name} 조작 불가 시작");

        yield return new WaitForSeconds(duration);

        carMove.enabled = true;
        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        Debug.Log($"{name} 조작 복귀 완료");
    }

    // (로컬) 가장 가까운 상대방 찾기
    private GameObject FindClosestOpponent()
    {
        ItemEffectHandler[] allCars = FindObjectsOfType<ItemEffectHandler>();
        GameObject closestOpponent = null;
        float minDistance = Mathf.Infinity;

        foreach (var carHandler in allCars)
        {
            // 자기 자신은 제외
            if (carHandler == this) continue;

            float dist = Vector3.Distance(transform.position, carHandler.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                closestOpponent = carHandler.gameObject;
            }
        }
        return closestOpponent;
    }

    public bool hasItem()
    {
        return currentItemVisual != null;
    }
    #endregion

    #region Player CustomProperties 헬퍼 (금괴 동기화)

    // 플레이어의 금괴 수를 가져오는 헬퍼
    public int GetGold()
    {
        return photonView.Owner.GetGold();
    }

    // 플레이어의 금괴 수를 설정(동기화)하는 헬퍼
    public void SetGold(int value)
    {
        photonView.Owner.SetGold(value);
    }
}

// Player 클래스 확장을 통해 헬퍼 메서드 정의
public static class PlayerCustomPropertiesExtensions
{
    public static int GetGold(this Player player)
    {
        if (player.CustomProperties.TryGetValue(ItemEffectHandler.GOLD_COUNT_KEY, out object gold))
        {
            return (int)gold;
        }
        return 0;
    }

    public static void SetGold(this Player player, int value)
    {
        // 타입을 ExitGames.Client.Photon.Hashtable로 명확하게 지정
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable
        {
            { ItemEffectHandler.GOLD_COUNT_KEY, value }
        };
        player.SetCustomProperties(props);
    }
}
#endregion