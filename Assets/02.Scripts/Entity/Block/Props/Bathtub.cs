public class Bathtub : SitableProp
{
    [Header("Bath Settings")]
    public int maxOccupants = 4;

    [Header("Sit Positions (Editor Assigned)")]
    public Transform[] sitPositions;

    [SerializeField] private Actor[] seatedActors;

    private void Start()
    {
        InitializeBath();
    }

    public void InitializeBath()
    {
        if (sitPositions == null || sitPositions.Length == 0)
        {
            Debug.LogWarning("Bath: sitPositions가 설정되지 않았습니다. 에디터에서 설정해주세요.");
            seatedActors = new Actor[0];
            return;
        }

        // 에디터에서 지정한 위치 개수를 기준으로 좌석 수 설정
        maxOccupants = sitPositions.Length;
        seatedActors = new Actor[maxOccupants];
    }

    public override bool TrySit(Actor actor)
    {
        if (!CanSit(actor))
        {
            return false;
        }
        
        int availablePosition = GetAvailablePosition();
        if (availablePosition == -1)
        {
            return false;
        }

        return SitAtPosition(actor, availablePosition);
    }

    public int GetAvailablePosition()
    {
        if (sitPositions == null) return -1;

        for (int i = 0; i < sitPositions.Length; i++)
        {
            if (seatedActors[i] == null)
            {
                return i;
            }
        }
        return -1;
    }

    private bool SitAtPosition(Actor actor, int position)
    {
        if (sitPositions == null) return false;
        if (position < 0 || position >= sitPositions.Length) return false;
        if (seatedActors[position] != null) return false;

        seatedActors[position] = actor;

        if (sitPositions[position] != null)
        {
            MoveActorToSitPosition(actor, sitPositions[position].position);
        }

        return true;
    }

    public override void StandUp(Actor actor)
    {
        for (int i = 0; i < seatedActors.Length; i++)
        {
            if (seatedActors[i] == actor)
            {
                seatedActors[i] = null;
                base.StandUp(actor);
                break;
            }
        }
    }

    public int GetOccupantCount()
    {
        if (seatedActors == null) return 0;
        
        int count = 0;
        for (int i = 0; i < seatedActors.Length; i++)
        {
            if (seatedActors[i] != null)
            {
                count++;
            }
        }
        return count;
    }
    
    public override bool IsActorSeated(Actor actor)
    {
        if (seatedActors == null) return false;
        
        for (int i = 0; i < seatedActors.Length; i++)
        {
            if (seatedActors[i] == actor)
            {
                return true;
            }
        }
        return false;
    }
    
    public override bool IsOccupied()
    {
        return GetOccupantCount() > 0;
    }

    public override string Get()
    {
        int count = GetOccupantCount();
        string status = "";
        if (count == 0)
        {
            status = "욕조에 아무도 없다.";
        }
        else status = $"욕조에 {count}명이 들어가있다.";

        if(String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"{GetLocalizedStatusDescription()} {status}";
        }
        return $"{status}";
    }

    public override async UniTask<string> Interact(Actor actor, CancellationToken cancellationToken = default)
    {
        ActivityBubbleUI bubble = null;
        try
        {
            if (actor is MainActor ma && ma.activityBubbleUI != null)
            {
                bubble = ma.activityBubbleUI;
                bubble.SetFollowTarget(actor.transform);
            }

            await SimDelay.DelaySimMinutes(1, cancellationToken);
            // 이미 앉아있는지 확인
            for (int i = 0; i < seatedActors.Length; i++)
            {
                if (seatedActors[i] == actor)
                {
                    if (bubble != null) bubble.Show("욕조에서 나오는 중", 0);
                    await SimDelay.DelaySimMinutes(1, cancellationToken);
                    StandUp(actor);
                    //await SimDelay.DelaySimMinutes(1, cancellationToken);
                    return "욕조에서 나왔다.";
                }
            }

            if (sitPositions == null || GetAvailablePosition() == -1)
            {
                return "욕조에 들어갈 자리이 없습니다.";
            }

            if (bubble != null) bubble.Show("욕조에 들어가는 중", 0);
            await SimDelay.DelaySimMinutes(1, cancellationToken);
            if (TrySit(actor))
            {
                //await SimDelay.DelaySimMinutes(1, cancellationToken);
                return "욕조에 들어왔다.";
            }

            return "욕조에 들어갈 수 없다.";
        }
        finally
        {
            if (bubble != null) bubble.Hide();
        }
    }
}
