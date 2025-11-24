using System.Collections;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Splines;
using TMPro;

[RequireComponent(typeof(Rigidbody))]
public class CarMove : MonoBehaviourPunCallbacks
{
    // ────── 주행 파라미터 ──────
    [Header("Spline & Speed")]
    public SplineContainer splineContainer;
    public float speed = 0.3f;

    [Tooltip("리스폰 시 트랙에서 들어올릴 y 오프셋(m)")]
    public float respawnLift = 0.1f;
    public LayerMask groundLayer;

    // ────── 리스폰 파라미터 ──────
    [Header("Off‑Track Respawn")]
    [Tooltip("트랙 중앙선에서 이 이상 멀어지면 리스폰")] public float offTrackDistance = 2f;
    [Tooltip("트랙 높이에서 이 이상 위/아래로 벗어나면 리스폰")] public float offTrackHeight = 0.5f;
    [Tooltip("한 번 리스폰 후 다음 리스폰까지 쿨타임(s)")] public float respawnCooldown = 2f;

    // ────── 내부 상태 ──────
    [HideInInspector] public float progress;      // 0‒1
    private Rigidbody rb;
    private RaceManager raceManager;

    [SerializeField] private int goalLaps = 2;    // 차량별 목표 랩 수
    private float prevProgress = 0f;
    private float lapProgress = 0f;               // 연속적인 랩 진행도 (0~goalLaps)
    public bool finished = false;

    // 스타트 지연
    private bool raceStarted = false;

    // 디버그 플래그
    private const bool LOG_EVERY_FRAME = false;

    // 리스폰 관련
    private float lastSafeProgress = 0f;   // 마지막으로 "온트랙" 판정된 위치
    private float lastRespawnTime = -Mathf.Infinity;
    private bool isMovingAllowed = true; // 차량 이동 허용 플래그

    [SerializeField] private ItemEffectHandler effectHandler;
    [SerializeField] private TMP_Text playerNumberText;
    private bool previousIsMine = false; // 이전 프레임의 IsMine 상태

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        raceManager ??= FindAnyObjectByType<RaceManager>();
        progress = 0f;

        if (splineContainer == null)
        {
            splineContainer = FindAnyObjectByType<SplineContainer>();
        }

        // 초기 텍스트 설정 및 상태 초기화
        if (photonView != null)
        {
            previousIsMine = photonView.IsMine;
        }
        UpdatePlayerText();

        // 레이스 시작 전 움직임 중지
        SetMovementEnabled(false);
        StartCoroutine(StartRaceAfterDelay());
    }

    private void Update()
    {
        // 오너 변경 감지: IsMine 상태가 변경되었을 때만 텍스트 업데이트
        if (photonView != null && previousIsMine != photonView.IsMine)
        {
            UpdatePlayerText();
            previousIsMine = photonView.IsMine;
        }
    }

    // 플레이어 텍스트 업데이트
    private void UpdatePlayerText()
    {
        if (playerNumberText == null || photonView == null) return;

        if (photonView.IsMine)
        {
            playerNumberText.text = "Player" + photonView.Owner.ActorNumber;
            playerNumberText.color = Color.green;
        }
        else
        {
            playerNumberText.text = "Player" + photonView.Owner.ActorNumber;
            playerNumberText.color = Color.red;
        }
    }

    private IEnumerator StartRaceAfterDelay()
    {
        yield return new WaitForSeconds(5f);  // 5초 대기 후 레이스 시작
        raceStarted = true;
        SetMovementEnabled(true);  // 레이스 시작 시 움직임 활성화
    }

    private void FixedUpdate()
    {
        if (!photonView.IsMine || splineContainer == null) return;

        if (raceStarted)
        {
            // 현재 진행도 위치의 트랙 표면 좌표 계산
            splineContainer.Spline.Evaluate(progress, out var splinePosF3, out _, out _);
            Vector3 splinePos = (Vector3)splinePosF3;

            // 수직 높이 차 계산
            float vertDist = Mathf.Abs(rb.position.y - splinePos.y);

            // 차량이 트랙에서 크게 벗어나면 움직임 비활성화
            if (vertDist > offTrackHeight * 1.5f) // 기존 offTrackHeight보다 더 엄격한 기준 적용
            {
                isMovingAllowed = false;
            }

            UpdateProgressAndMove();   // 손/키 입력 → 차 이동
            MaybeRespawn();           // 트랙 이탈 체크
            DetectLapAndWin();        // 랩·우승 판정
        }

        if (LOG_EVERY_FRAME)
            Debug.Log($"[{photonView.OwnerActorNr}] prog:{progress:F3}  lapProg:{lapProgress:F3}");
    }

    public Vector3 GetSize()
    {
        return this.gameObject.transform.localScale;
    }

    // ──────────────────────────────────────────
    #region Respawn Logic

    /// <summary>
    /// 트랙에서 멀어졌는지 점검 후 필요하면 리스폰.
    /// </summary>
    private void MaybeRespawn()
    {
        // 쿨타임
        if (Time.time - lastRespawnTime < respawnCooldown) return;

        // 현재 진행도 위치의 트랙 표면 좌표 계산
        splineContainer.Spline.Evaluate(progress, out var splinePosF3, out _, out _);
        Vector3 splinePos = (Vector3)splinePosF3;

        // 수평 거리 & 수직 높이 차 계산
        float horizDist = Vector2.Distance(new Vector2(rb.position.x, rb.position.z), new Vector2(splinePos.x, splinePos.z));
        float vertDist = Mathf.Abs(rb.position.y - splinePos.y);

        bool offTrack = horizDist > offTrackDistance || vertDist > offTrackHeight;
        if (offTrack)
        {
            isMovingAllowed = false;
            RespawnAtProgress(lastSafeProgress);
            lastRespawnTime = Time.time;
        }
    }

    /// <summary>
    /// 진행도 t 지점에 차를 리스폰한다.
    /// </summary>
    private void RespawnAtProgress(float t)
    {
        isMovingAllowed = false;
        SetMovementEnabled(false);  // 리스폰 시 움직임 중지
        splineContainer.Spline.Evaluate(t, out var posF3, out var tanF3, out var upF3);
        Vector3 newPos = (Vector3)posF3 + (Vector3)upF3 * respawnLift;
        // 차량이 뒤집히는 것을 방지하기 위해 월드 '위' 방향을 기준으로 회전 설정
        Quaternion newRot = Quaternion.LookRotation((Vector3)tanF3, Vector3.up);

        rb.position = newPos;
        rb.rotation = newRot;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Debug.Log($"[{photonView.OwnerActorNr}] Respawned at {t:F3}");
        StartCoroutine(EnableMovementAfterDelay(respawnCooldown));
    }

    private IEnumerator EnableMovementAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        isMovingAllowed = true;
        SetMovementEnabled(true);  // 리스폰 대기 후 움직임 활성화
    }

    #endregion

    // ──────────────────────────────────────────
    #region Lap & Win Detection

    private void DetectLapAndWin()
    {
        // 승리 조건 체크: lapProgress가 goalLaps에 도달
        if (!finished && lapProgress >= goalLaps)
        {
            finished = true;
            Debug.Log($"[{photonView.OwnerActorNr}] Race Finished! Final lapProgress: {lapProgress:F3}");

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

    // ──────────────────────────────────────────
    #region Movement

    /// <summary>
    /// 입력(손/키) → 차체 이동 & 진행도 계산.
    /// </summary>
    private void UpdateProgressAndMove()
    {
        if (!isMovingAllowed) return;

        // 진행도 계산 (0‒1)
        progress = CalculateProgressFromPosition(transform.position);
        if (progress >= 1f) progress -= 1f;

        // UpdateLapProgress 로직 통합
        float deltaProgress = progress - prevProgress;
        if (deltaProgress < -0.5f) deltaProgress += 1f;
        else if (deltaProgress > 0.5f) deltaProgress -= 1f;

        lapProgress += deltaProgress;
        prevProgress = progress;

        lastSafeProgress = progress; // 온트랙으로 판정된 가장 최근 진행도 저장

        // ──── 손/키 입력 → 이동(예시는 손 추적) ────
        if (HandJointUtils.TryGetJointPose(TrackedHandJoint.IndexTip, Handedness.Right, out var rightHand))
        {
            Vector3 dir = rightHand.Forward; dir.y = 0; dir.Normalize();
            if (dir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.fixedDeltaTime * 1.5f);
            }
            Vector3 moveDir = transform.forward; // 기본은 전방

            // 차 위치에서 아래로 레이를 쏘아 바닥의 기울기(Normal)를 알아냄
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 2.0f, groundLayer))
            {
                // 전진 벡터를 바닥 경사면에 투영(Projection)
                moveDir = Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;

                // (선택사항) 시각적으로 차가 경사면을 따라 기울게 하고 싶다면:
                // 이 부분은 Mesh만 따로 회전시키거나 전체 회전을 보간해야 해서 복잡해질 수 있으니,
                // 일단 '이동'만 잘 되게 하려면 위의 ProjectOnPlane만 있어도 충분합니다.
            }
            rb.linearVelocity = transform.forward * speed;
        }
        else rb.linearVelocity = Vector3.zero;
    }

    private float CalculateProgressFromPosition(Vector3 pos)
    {
        const int SAMPLES = 300;
        float bestT = 0f, bestDist = float.MaxValue;

        for (int i = 0; i <= SAMPLES; i++)
        {
            float t = i / (float)SAMPLES;
            splineContainer.Spline.Evaluate(t, out var samplePos, out _, out _);
            float d = Vector3.Distance(pos, samplePos);
            if (d < bestDist) { bestDist = d; bestT = t; }
        }
        return bestT;
    }

    #endregion

    // ──────────────────────────────────────────
    #region Public Control Methods

    /// <summary>
    /// 차량의 움직임을 제어합니다.
    /// </summary>
    /// <param name="enabled">true면 움직임 활성화, false면 움직임 중지</param>
    public void SetMovementEnabled(bool enabled)
    {
        isMovingAllowed = enabled;
        if (rb != null)
        {
            rb.isKinematic = !enabled;
            if (!enabled)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    #endregion

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Item"))
        {
            // ItemSpawner를 찾아서 리스폰 로직 호출
            ItemSpawner itemSpawner = FindAnyObjectByType<ItemSpawner>();

            // 이미 아이템을 가지고 있는 경우, 박스만 파괴
            if (effectHandler.hasItem())
            {
                Debug.Log("아이템 보유 중 - 박스만 파괴");

                if (itemSpawner != null)
                {
                    itemSpawner.OnItemCollected(other.gameObject);
                }
                else
                {
                    // ItemSpawner를 찾을 수 없으면 기존처럼 파괴
                    PhotonNetwork.Destroy(other.gameObject);
                }
                return; // 아이템 효과는 적용하지 않음
            }

            // 아이템을 가지고 있지 않은 경우, 정상적으로 획득
            Debug.Log("아이템 획득!");

            if (itemSpawner != null)
            {
                itemSpawner.OnItemCollected(other.gameObject);
            }
            else
            {
                // ItemSpawner를 찾을 수 없으면 기존처럼 파괴
                PhotonNetwork.Destroy(other.gameObject);
            }

            if (effectHandler)
            {
                effectHandler.ApplyItemEffect();
            }
        }
    }
    void OnCollisionEnter(Collision collision)
    {
        // 장애물 충돌 시 무적 예외처리
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            if (effectHandler && effectHandler.IsInvincible())
            {
                Collider myCol = GetComponent<Collider>();
                Collider obsCol = collision.collider;

                if (myCol != null && obsCol != null)
                {
                    Physics.IgnoreCollision(myCol, obsCol, true); // 충돌 완전 비활성화
                    Debug.Log("🛡 무적 상태 - 장애물 충돌 완전 무시 (유령 모드)");
                }

                return; // 아무 물리효과도 주지 않음
            }
        }
    }
}
