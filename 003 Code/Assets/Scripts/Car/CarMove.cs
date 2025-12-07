using System.Collections;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Splines;
using TMPro;
using Unity.Mathematics;

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

    [Header("Slope Compensation")]
    [Tooltip("경사면을 오르기 위한 추가 힘 배율 (경사가 급할수록 더 큰 힘 필요)")]
    public float slopeForceMultiplier = 2.0f;
    [Tooltip("경사면 각도 보정을 위한 최소 각도 (이 각도 이상일 때만 보정 적용)")]
    public float minSlopeAngle = 5f;
    [Tooltip("경사면 오르기 최대 힘 (무한정 힘을 주지 않도록 제한)")]
    public float maxSlopeForce = 10f;

    // ────── 리스폰 파라미터 ──────
    [Header("Off‑Track Respawn")]
    [Tooltip("트랙 중앙선에서 이 이상 멀어지면 리스폰")] public float offTrackDistance = 2f;
    [Tooltip("트랙 높이에서 이 이상 위/아래로 벗어나면 리스폰")] public float offTrackHeight = 0.5f;
    [Tooltip("한 번 리스폰 후 다음 리스폰까지 쿨타임(s)")] public float respawnCooldown = 2f;

    [Header("Flipped Respawn")]
    [Tooltip("차가 뒤집힌 상태로 유지되면 리스폰할 시간(초)")] public float flipRespawnTime = 3f;
    [Tooltip("뒤집힘 판정 각도 (이 각도 이상 기울어지면 뒤집힌 것으로 간주)")] public float flipAngleThreshold = 60f;


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
    private float currentFlippedTime = 0f;
    private bool isFlipped = false;
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

    private IEnumerator SetKinematicAfterPositionUpdate()
    {
        // 한 프레임 대기하여 위치 설정이 완료되도록 함
        yield return new WaitForFixedUpdate();

        // 이제 kinematic을 true로 설정하여 움직임 비활성화
        if (rb != null)
        {
            rb.isKinematic = true;
        }
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
            CheckFlipped();           // 뒤집힘 체크
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
    /// 차가 뒤집혀 있는지 확인하고 일정 시간 이상 지속되면 리스폰.
    /// </summary>
    private void CheckFlipped()
    {
        // 쿨타임 중이면 체크 안 함
        if (Time.time - lastRespawnTime < respawnCooldown)
        {
            currentFlippedTime = 0f;
            isFlipped = false;
            return;
        }

        // 현재 진행도의 트랙 Up 벡터 가져오기
        splineContainer.Spline.Evaluate(progress, out _, out _, out var upF3);
        Vector3 trackUp = ((Vector3)upF3).normalized;

        // 차의 Up 벡터와 트랙의 Up 벡터 사이의 각도 계산
        float angle = Vector3.Angle(transform.up, trackUp);

        // 각도가 임계값보다 크면 뒤집힌 것으로 간주
        if (angle > flipAngleThreshold)
        {
            isFlipped = true;
            currentFlippedTime += Time.fixedDeltaTime;
            if (currentFlippedTime > flipRespawnTime)
            {
                Debug.Log($"[{photonView.OwnerActorNr}] Car flipped for {currentFlippedTime:F1}s. Respawning...");
                RespawnAtProgress(lastSafeProgress);
                currentFlippedTime = 0f;
                lastRespawnTime = Time.time;
                isFlipped = false;
            }
        }
        else
        {
            isFlipped = false;
            currentFlippedTime = 0f;
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
        Quaternion newRot = Quaternion.LookRotation((Vector3)tanF3, (Vector3)upF3);

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

        if (isFlipped) return;

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
            Vector3 groundNormal = Vector3.up; // 기본값
            float slopeAngle = 0f;

            // 차 위치에서 아래로 레이를 쏘아 바닥의 기울기(Normal)를 알아냄
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 2.0f, groundLayer))
            {
                groundNormal = hit.normal;
                // 전진 벡터를 바닥 경사면에 투영(Projection)
                moveDir = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;

                // 경사면 각도 계산 (수평면과의 각도)
                slopeAngle = Vector3.Angle(groundNormal, Vector3.up);
            }

            // 경사면에 맞춘 이동 방향으로 속도 설정
            rb.linearVelocity = moveDir * speed;

            // 경사면을 오르기 위한 추가 힘 적용
            if (slopeAngle > minSlopeAngle)
            {
                // 경사면의 오르막 방향 계산 (경사면 normal과 수직이면서 위쪽 성분이 있는 방향)
                Vector3 slopeUpDirection = Vector3.ProjectOnPlane(Vector3.up, groundNormal).normalized;

                // 경사가 급할수록 더 큰 힘 필요 (sin(각도) 사용)
                float slopeFactor = Mathf.Sin(slopeAngle * Mathf.Deg2Rad);
                float additionalForce = slopeFactor * slopeForceMultiplier * speed;

                // 최대 힘 제한
                additionalForce = Mathf.Min(additionalForce, maxSlopeForce);

                // 경사면을 오르는 방향으로 힘 추가
                rb.AddForce(slopeUpDirection * additionalForce, ForceMode.Acceleration);
            }
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

    /// <summary>
    /// 차량을 초기 상태로 리셋합니다. (게임 재시작 시 사용)
    /// </summary>
    public void ResetCar()
    {
        if (splineContainer == null) return;

        // 진행도 및 랩 진행도 초기화
        progress = 0f;
        prevProgress = 0f;
        lapProgress = 0f;
        finished = false;
        raceStarted = false;
        lastSafeProgress = 0f;
        lastRespawnTime = -Mathf.Infinity;

        // 초기 위치 계산 (SpawnManager의 로직과 동일)
        splineContainer.Spline.Evaluate(0f, out float3 posF3, out float3 tanF3, out float3 upF3);
        Vector3 center = (Vector3)posF3;
        Vector3 forward = ((Vector3)tanF3).normalized;
        Vector3 up = ((Vector3)upF3).normalized;
        Vector3 right = Vector3.Cross(up, forward).normalized;

        // 차량의 초기 위치 결정 (ActorNumber 기준: 1번은 왼쪽, 2번은 오른쪽)
        float laneOffset = 0.2f; // SplineExtrude의 Radius를 모르므로 기본값 사용
        float heightLift = 0.1f;

        // SplineExtrude를 찾아서 정확한 오프셋 계산
        SplineExtrude splineExtrude = FindAnyObjectByType<SplineExtrude>();
        if (splineExtrude != null)
        {
            laneOffset = splineExtrude.Radius * 0.2f;
            heightLift = splineExtrude.Radius * 0.1f;
        }

        Vector3 initialPos;
        if (photonView != null && photonView.Owner != null)
        {
            // ActorNumber가 1이거나 홀수면 왼쪽, 짝수면 오른쪽
            bool isLeft = (photonView.Owner.ActorNumber % 2 == 1);
            initialPos = isLeft
                ? center - right * laneOffset + up * heightLift
                : center + right * laneOffset + up * heightLift;
        }
        else
        {
            // 기본값: 왼쪽
            initialPos = center - right * laneOffset + up * heightLift;
        }

        Quaternion initialRot = Quaternion.LookRotation(forward, up);

        // 움직임 먼저 비활성화 (kinematic을 false로 설정하여 위치 변경 가능하게)
        isMovingAllowed = false;
        if (rb != null)
        {
            rb.isKinematic = false; // 위치 설정을 위해 먼저 kinematic 해제
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Rigidbody 위치 설정 (kinematic이 false일 때만 작동)
        // transform을 먼저 설정한 후 Rigidbody를 동기화
        transform.position = initialPos;
        transform.rotation = initialRot;

        if (rb != null)
        {
            // Rigidbody의 position과 rotation을 직접 설정
            rb.position = initialPos;
            rb.rotation = initialRot;
        }

        // 네트워크 동기화 위치 강제 업데이트 (CarNetworkSync가 위치를 덮어쓰지 않도록)
        CarNetworkSync networkSync = GetComponent<CarNetworkSync>();
        if (networkSync != null)
        {
            networkSync.ResetNetworkPosition(initialPos, initialRot);
        }

        // 초기 속도 복원 (SpawnManager의 InitCar와 동일한 로직)
        SplineExtrude splineExtrudeForSpeed = FindAnyObjectByType<SplineExtrude>();
        if (splineExtrudeForSpeed != null)
        {
            speed = Mathf.Min(splineExtrudeForSpeed.Radius * 0.7f, 0.5f);
        }

        // 아이템 효과 초기화
        if (effectHandler != null)
        {
            effectHandler.ResetEffects();
        }

        // 레이스 시작 코루틴 재시작 (먼저 중지)
        StopAllCoroutines();

        // 움직임 비활성화 (레이스 시작 대기) - 위치 설정 후에 호출
        // 코루틴에서 kinematic을 설정하도록 지연
        StartCoroutine(SetKinematicAfterPositionUpdate());

        // 레이스 시작 코루틴 재시작
        StartCoroutine(StartRaceAfterDelay());

        Debug.Log($"[{photonView?.OwnerActorNr ?? -1}] 차량 리셋 완료 - 초기 위치: {initialPos}, 속도: {speed}");
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
