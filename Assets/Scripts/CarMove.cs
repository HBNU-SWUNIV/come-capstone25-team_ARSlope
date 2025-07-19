using System.Collections;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Splines;

[RequireComponent(typeof(Rigidbody))]
public class CarMove : MonoBehaviourPunCallbacks
{
    public SplineContainer splineContainer;
    public float speed = 0.2f;
    public float reverseSpeed = 1f;
    public float dashMultiplier = 2f;
    public float turnSpeed = 100f;
    public float respawnHeightOffset = 5f; // 리스폰 기준 위치 (스플라인 위치 위로 일정 오프셋 추가)
    public float respawnLift = 0.5f; // 도로 위로 띄우는 높이

    [HideInInspector] public float progress;
    private readonly float exitIgnoreDuration = 0.5f; // 리스폰 직후 0.5초간 Exit 무시
    private float ignoreExitTime;
    private bool isFalling;
    private Vector3 lastGroundedPosition = Vector3.zero;
    private float lastGroundedProgress;
    private Rigidbody rb;
    
    private RaceManager raceManager; // RaceManager 참조

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        raceManager = FindObjectOfType<RaceManager>(); // RaceManager 찾기
        if (raceManager == null)
        {
            Debug.LogError("RaceManager가 씬에 존재하지 않습니다!");
        }
        else
        {
            Debug.Log("RaceManager가 정상적으로 할당되었습니다.");
        }
    }

    private void FixedUpdate()
    {
        if (splineContainer == null || rb == null) return;

        if (photonView.IsMine)
            HandMove();
        else return;
        
        // 바퀴 수 업데이트
        if (raceManager != null)
        {
            raceManager.UpdateLapProgress(progress);
            Debug.Log("LapProgress 업데이트");
        }
        else
        {
            Debug.LogError("RaceManager가 null입니다!");
        }
        //KeyMove();    
    }

    private void OnCollisionExit(Collision collision)
    {
        // 리스폰 직후엔 무시
        if (Time.time < ignoreExitTime) return;

        if (collision.gameObject.CompareTag("Spline") && !isFalling)
        {
            Debug.Log(lastGroundedPosition);
            Debug.Log(lastGroundedProgress);
            isFalling = true;
            StartCoroutine(RespawnAfterDelay(1f));
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Spline"))
        {
            lastGroundedProgress = progress;
            lastGroundedPosition = transform.position;
            isFalling = false;
        }
    }

    //void OnCollisionEnter(Collision collision)
    //{
    //    if (collision.gameObject.CompareTag("spline"))
    //    {
    //        // OnCollisionEnter에서 코루틴 호출
    //        StartCoroutine(HandleCollision());
    //    }
    //}

    //private IEnumerator HandleCollision()
    //{
    //    // Rigidbody를 잠시 비활성화
    //    rb.useGravity = false;

    //    // 1초 대기
    //    yield return new WaitForSeconds(1f);

    //    // Rigidbody를 다시 활성화
    //    rb.useGravity = true;
    //}

    private IEnumerator RespawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        RespawnAtCurrentProgress();
    }

    private void RespawnAtCurrentProgress()
    {
        splineContainer.Spline.Evaluate(lastGroundedProgress, out var pos, out var tangent, out var up);
        var targetPos = (Vector3)pos + ((Vector3)up).normalized * respawnLift;
        var targetRot = Quaternion.LookRotation(tangent, up);

        StartCoroutine(SmoothRespawn(targetPos, targetRot));
    }

    private IEnumerator SmoothRespawn(Vector3 targetPosition, Quaternion targetRotation)
    {
        var duration = 0.6f;
        var elapsed = 0f;

        var startPos = rb.position;
        var startRot = rb.rotation;

        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        while (elapsed < duration)
        {
            var t = elapsed / duration;
            rb.position = Vector3.Lerp(startPos, targetPosition, t);
            rb.rotation = Quaternion.Slerp(startRot, targetRotation, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rb.position = targetPosition;
        rb.rotation = targetRotation;

        yield return new WaitForSeconds(0.1f);

        rb.useGravity = true;
        isFalling = false;
        ignoreExitTime = Time.time + exitIgnoreDuration;

        Debug.Log($"[부드러운 리스폰 완료] 위치: {targetPosition}");
    }

    private float CalculateProgressFromPosition(Vector3 position)
    {
        var sampleCount = 300;
        var closestT = 0f;
        var closestDist = Mathf.Infinity;

        for (var i = 0; i <= sampleCount; i++)
        {
            var t = i / (float)sampleCount;
            splineContainer.Spline.Evaluate(t, out var samplePos, out _, out _);

            var dist = Vector3.Distance(position, samplePos);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestT = t;
            }
        }

        return closestT;
    }

    private void HandMove()
    {
        if (progress >= 1f) progress -= 1f;
        progress = CalculateProgressFromPosition(transform.position);

        // 오른손이 가리키는 방향으로 이동하는 오브젝트
        if (HandJointUtils.TryGetJointPose(TrackedHandJoint.IndexTip, Handedness.Right, out var rightHandPos))
        {
            var handPos = rightHandPos; // 오른손 위치

            var moveDir = handPos.Forward; // 이동 방향
            moveDir.y = 0; // y축으로 이동 금지
            moveDir.Normalize();

            if (moveDir != Vector3.zero)
            {
                var targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * 1.5f);
            }

            rb.linearVelocity = transform.forward * speed;
        }
        else
        {
            // 손 인식 실패 시 모든 움직임 정지 및 회전 유지
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            return;
        }

        var rayOrigin = transform.position + Vector3.up * 0.1f; // 바닥 체크를 위한 Ray 시작 위치
        Vector3 moveDirection;
        if (Physics.Raycast(rayOrigin, Vector3.down, out var slopeHit, 2f)) // 도로가 있는지 확인
        {
            var groundNormal = slopeHit.normal; // 도로의 기울기 벡터
            moveDirection = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;

            var angle = Vector3.Angle(transform.up, groundNormal);
            if (angle < 60f && !isFalling)
            {
                var targetRotation = Quaternion.LookRotation(moveDirection, groundNormal);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * 5f);
            }

            if (angle > 60f)
                if (!isFalling)
                {
                    isFalling = true;
                    StartCoroutine(RespawnAfterDelay(1f));
                }
            //Debug.Log("기울기 각도: " + angle);
        }
        else
        {
            moveDirection = transform.forward; // 공중일 경우엔 원래 방향
        }
    }

    private void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        var data = info.photonView.InstantiationData;
        if (data != null && data.Length > 0)
        {
            float s = (float)data[0];
            transform.localScale = Vector3.one * s;
        }
    }

    private void KeyMove()
    {
        // 현재 스플라인 위치 계산 (한 번만 호출)
        splineContainer.Spline.Evaluate(progress, out var pos, out var tangent, out var up);


        var vInput = Input.GetAxis("Vertical"); // W/S
        var hInput = Input.GetAxis("Horizontal"); // A/D

        // 대쉬 포함한 현재 속도 계산
        var currentSpeed = vInput > 0 ? speed : reverseSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) currentSpeed *= dashMultiplier;

        progress = CalculateProgressFromPosition(transform.position);

        if (progress >= 1f) progress -= 1f;

        // 좌우 회전
        var turn = hInput * turnSpeed * Time.fixedDeltaTime;
        transform.Rotate(0, turn, 0);

        Vector3 moveDirection;
        var rayOrigin = transform.position + Vector3.up * 0.1f; // 바닥 체크를 위한 Ray 시작 위치

        if (Physics.Raycast(rayOrigin, Vector3.down, out var slopeHit, 2f)) // 도로가 있는지 확인
        {
            var groundNormal = slopeHit.normal; // 도로의 기울기 벡터
            moveDirection = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;

            // 회전도 도로 경사에 맞춰 보간
            var targetRotation = Quaternion.LookRotation(moveDirection, groundNormal);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * 5f);

            var angle = Vector3.Angle(transform.up, groundNormal);
            if (angle > 60f)
                if (!isFalling)
                {
                    isFalling = true;
                    StartCoroutine(RespawnAfterDelay(1f));
                }
            //Debug.Log("기울기 각도: " + angle);
        }
        else
        {
            moveDirection = transform.forward; // 공중일 경우엔 원래 방향
        }


        // velocity를 사용하여 속도 설정 (AddForce 없이 직접 설정)
        var forward = transform.forward;
        if (vInput == 0)
            rb.linearVelocity = Vector3.zero; // 이동하지 않으면 속도를 0으로 설정
        else if (vInput > 0)
            rb.linearVelocity = forward * currentSpeed; // 일정 속도로 이동
        else
            rb.linearVelocity = -forward * currentSpeed; // 일정 속도로 이동
        //rb.AddForce(forward * (vInput * currentSpeed * 10f));

        // progress가 1을 넘으면 0으로 돌아가도록 설정
        if (progress >= 1f) progress -= 1f; // progress가 1를 넘으면 0부터 다시 시작

        // 스플라인 방향 디버그 표시
        Debug.DrawLine(transform.position, transform.position + (Vector3)tangent, Color.yellow);
    }
}