using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class TrashBin : InventoryBox
{
    [Header("Trash Bin Settings")]
    public bool isFull = false;

    protected override void Start()
    {
        base.Start(); // 부모 클래스의 Start 호출

        UpdateTrashStatus();
    }

    // maxItems와 itemPlacementPosition 제한을 우회하여 무제한으로 아이템 추가
    public override bool AddItem(Entity item)
    {
        if (isFull)
        {
            return false;
        }

        // 부모 클래스의 제한을 우회하고 직접 아이템 추가
        items.Add(item);

        // 아이템을 쓰레기통 위치에 직접 배치 (위치 제한 무시)
        item.transform.SetParent(transform);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;

        // 아이템을 비활성화 상태로 만듦 (쓰레기통 안에 있는 것처럼)
        item.gameObject.SetActive(false);

        UpdateTrashStatus();
        return true;
    }

    public override Entity GetItem(string itemKey)
    {
        Entity item = base.GetItem(itemKey);
        if (item != null)
        {
            UpdateTrashStatus();
        }
        return item;
    }

    public void EmptyTrash()
    {
        // 모든 아이템을 실제로 삭제
        foreach (Entity item in items)
        {
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }

        items.Clear();

        // 위치 추적 배열도 초기화 (부모 클래스의 positionEntities 사용)
        if (positionEntities != null)
        {
            for (int i = 0; i < positionEntities.Length; i++)
            {
                positionEntities[i] = null;
            }
        }

        isFull = false;
        UpdateTrashStatus();
    }

    private void UpdateTrashStatus()
    {
        // maxItems 제한을 무시하고 항상 false로 설정 (무제한)
        isFull = false;
    }

    public override string Get()
    {
        string status = "";
        if (items.Count > 0)
        {
            status = $"쓰레기통에 {items.Count}개의 물건이 있습니다";
        }

        else status = "쓰레기통이 비어있습니다.";

        if (String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"{GetLocalizedStatusDescription()} {status}";
        }
        return $"{status}";
    }

    public override async UniTask<string> Interact(Actor actor, CancellationToken cancellationToken = default)
    {
        //await SimDelay.DelaySimMinutes(1, cancellationToken);
        // 손에 아이템이 있는 경우: 쓰레기통에 아이템 버리기
        if (actor.HandItem != null)
        {
            if (isFull)
            {
                return "쓰레기통이 가득 찼습니다.";
            }
            ActivityBubbleUI bubble = null;
            if (actor is MainActor ma && ma.activityBubbleUI != null)
            {
                bubble = ma.activityBubbleUI;
                bubble.SetFollowTarget(actor.transform);
                bubble.Show($"쓰레기통에 물건 버리는 중", 0);
            }
            await SimDelay.DelaySimMinutes(1, cancellationToken);

            // 아이템 이름 저장 (나중에 사용하기 위해)
            string itemName = actor.HandItem.Name;

            // 아이템을 쓰레기통에 추가
            AddItem(actor.HandItem);
            if (bubble != null) bubble.Hide();
            // Actor의 손에서 아이템 제거
            actor.HandItem = null;

            return $"{actor.Name}이(가) {itemName}을(를) 쓰레기통에 버렸습니다.";
        }
        // 빈손인 경우: 쓰레기통 상태 확인
        else
        {
            if (items.Count == 0)
            {
                return "쓰레기통이 비어있습니다.";
            }

            return $"쓰레기통에 {items.Count}개의 아이템이 있습니다. 아이템을 추가하거나 비울 수 있습니다.";
        }
    }
}
