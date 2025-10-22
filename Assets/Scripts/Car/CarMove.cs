using System.Collections;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Splines;

[RequireComponent(typeof(Rigidbody))]
public class CarMove : MonoBehaviourPunCallbacks
{
    // ────── 주행 파라미터 ──────
    [Header("Spline & Speed")]
    public SplineContainer splineContainer;
    public float speed = 0.3f;

    [Tooltip("리스폰 시 트랙에서 들어올릴 y 오프셋(m)")]
    public float respawnLift = 0.1f;

    // ────── 리스폰 파라미터 ──────
    [Header("Off‑Track Respawn")]
    [Tooltip("트랙 중앙선에서 이 이상 멀어지면 리스폰")] public float offTrackDistance = 2f;
    [Tooltip("트랙 높이에서 이 이상 위/아래로 벗어나면 리스폰")] public float offTrackHeight = 0.5f;
    [Tooltip("한 번 리스폰 후 다음 리스폰까지 쿨타임(s)")] public float respawnCooldown = 2f;

    // ────── 내부 상태 ──────
    [HideInInspector] public float progress;      // 0‒1
    private Rigidbody rb;
    private RaceManager raceManager;

    [SerializeField] private int goalLaps = 3;    // 차량별 목표 랩 수
    private float prevProgress = 0f;
    private float lapProgress = 0f;               // 연속적인 랩 진행도 (0~goalLaps)
    private bool finished = false;

    // 스타트 지연
    private bool raceStarted = false;

    // 디버그 플래그
    private const bool LOG_EVERY_FRAME = false;

    // 리스폰 관련
    private float lastSafeProgress = 0f;   // 마지막으로 "온트랙" 판정된 위치
    private float lastRespawnTime = -Mathf.Infinity;
    private bool isMovingAllowed = true; // 차량 이동 허용 플래그

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        raceManager ??= FindAnyObjectByType<RaceManager>();
        progress = 0f;

        if (splineContainer == null)
        {
            splineContainer = FindAnyObjectByType<SplineContainer>();
        }

        StartCoroutine(StartRaceAfterDelay());
    }

    private IEnumerator StartRaceAfterDelay()
    {
        yield return new WaitForSeconds(5f);  // 5초 대기 후 레이스 시작
        raceStarted = true;
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

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Item"))
        {
            Debug.Log("아이템 획득!");
            PhotonNetwork.Destroy(other.gameObject);

            ItemEffectHandler effectHandler = GetComponent<ItemEffectHandler>();
            if (effectHandler != null)
            {
                effectHandler.ApplyItemEffect();
            }
        }
    }
}
