using System;

public abstract class Item : Entity, ICollectible
{
    public virtual string GetWhenOnHand()
    {
        return Get();
    }
    /// <summary>
    /// IInteractable과 상호작용합니다. 기본 구현은 true를 반환합니다.
    /// 하위 클래스에서 오버라이드하여 구체적인 상호작용을 구현할 수 있습니다.
    /// </summary>
    /// <param name="actor">상호작용하는 Actor</param>
    /// <param name="interactable">상호작용할 IInteractable</param>
    /// <returns>true: 상호작용 계속 진행, false: 상호작용 중단</returns>
    public virtual bool InteractWithInteractable(Actor actor, IInteractable interactable)
    {
        // 기본 구현: 상호작용 계속 진행
        return true;
    }

    public override string Get()
    {
        if(String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"{GetLocalizedStatusDescription()}";
        }
        return $"{LocationToString()}이 있다.";
    }
}
