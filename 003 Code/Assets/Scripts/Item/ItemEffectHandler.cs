using UnityEngine;
using System.Collections;
using Photon.Pun;

public class ItemEffectHandler : MonoBehaviourPun
{
    private CarMove carMove;
    private int goldCount = 0;
    private bool isInvincible = false;
    public Transform itemDisplayPoint;  // Unity에서 지정
    private GameObject currentItemVisual;

    public GameObject crownPrefab;
    public GameObject goldPrefab;
    public GameObject boosterPrefab;
    public GameObject bombPrefab;
    public GameObject oilRedPrefab;
    public GameObject oilGreenPrefab;

    private void Start()
    {
        carMove = GetComponent<CarMove>();  // 같은 오브젝트 내에 있을 경우

        if (carMove == null)
        {
            Debug.LogError("CarMove 컴포넌트를 찾을 수 없습니다!");
        }
        else
        {
            Debug.Log("CarMove 정상적으로 할당되었습니다.");
        }
    }

    void ShowItemOnCar(GameObject itemPrefab, float duration)
    {
        if (currentItemVisual != null)
        {
            PhotonNetwork.Destroy(currentItemVisual);
        }

        var itemScale = carMove.GetSize() * 15f;
        currentItemVisual = PhotonNetwork.Instantiate(
            itemPrefab.name, itemDisplayPoint.position, itemDisplayPoint.rotation, 0, new object[] {itemScale});
        currentItemVisual.transform.parent = itemDisplayPoint.transform;

        // 표시 시간 후 자동 제거
        StartCoroutine(RemoveItemVisualAfter(duration));
    }

    IEnumerator RemoveItemVisualAfter(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (currentItemVisual != null)
        {
            PhotonNetwork.Destroy(currentItemVisual);
            currentItemVisual = null;
        }
    }


    public void ApplyItemEffect()
    {
        int random = Random.Range(0, 100);
        Debug.Log($"랜덤 값: {random}");

        if (random < 15)
        {
            Debug.Log("🔴 기름통 (빨강) 발동!");
            ApplyOilEffect(0.9f);
            ShowItemOnCar(oilRedPrefab, 3f);  // 영구 효과 → 3초만 표시
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
            // 폭탄 기능
            ShowItemOnCar(bombPrefab, 3f);
        }
        else if (random < 70)
        {
            Debug.Log("👑 왕관 발동!");
            StartCoroutine(ApplyInvincibility(5f));
            ShowItemOnCar(crownPrefab, 5f);  // 무적 지속 시간만큼 표시
        }
        else if (random < 80)
        {
            Debug.Log("⚡ 부스터 발동!");
            StartCoroutine(ApplySpeedBoost(3f));
            ShowItemOnCar(boosterPrefab, 3f);  // 부스터는 3초 동안 지속
        }
        else if (random < 95)
        {
            Debug.Log("💰 금괴 획득!");
            CollectGold();
            ShowItemOnCar(goldPrefab, 3f);
        }
        else
        {
            Debug.Log("🪝 갈고리 발동!");
            // 갈고리 기능 + 표시
        }
    }


    // 기름통 효과 (영구 속도 보정)
    void ApplyOilEffect(float multiplier)
    {
        carMove.speed *= multiplier;
        // carMove.reverseSpeed *= multiplier;
    }

    // 2x 부스터 효과 (일시 속도 두배)
    IEnumerator ApplySpeedBoost(float duration)
    {
        float originalSpeed = carMove.speed;
        // float originalReverse = carMove.reverseSpeed;

        carMove.speed *= 2f;
        // carMove.reverseSpeed *= 2f;

        yield return new WaitForSeconds(duration);

        carMove.speed = originalSpeed;
        // carMove.reverseSpeed = originalReverse;
    }

    // 왕관 효과 (무적)
    IEnumerator ApplyInvincibility(float duration)
    {
        isInvincible = true;
        Debug.Log("무적 상태 시작");
        yield return new WaitForSeconds(duration);
        isInvincible = false;
        Debug.Log("무적 상태 종료");
    }

    // 금괴 효과
    void CollectGold()
    {
        goldCount++;
        Debug.Log($"현재 금괴 수: {goldCount}/5");

        if (goldCount >= 5)
        {
            Debug.Log("🎉 금괴 5개! 게임 승리!");
            // 승리 조건 처리 - 예: GameManager.EndGame()
        }
    }

    // 무적 상태 확인 메서드 (폭탄 등에서 활용 가능)
    public bool IsInvincible()
    {
        return isInvincible;
    }
}
