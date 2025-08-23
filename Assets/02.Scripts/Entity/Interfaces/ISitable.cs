/// <summary>
/// 앉을 수 있는 객체를 나타내는 인터페이스
/// </summary>
public interface ISitable
{
    /// <summary>
    /// Actor가 앉을 수 있는지 확인합니다.
    /// </summary>
    /// <param name="actor">앉으려는 Actor</param>
    /// <returns>앉을 수 있으면 true</returns>
    bool CanSit(Actor actor);
    
    /// <summary>
    /// Actor를 앉힙니다.
    /// </summary>
    /// <param name="actor">앉으려는 Actor</param>
    /// <returns>성공하면 true</returns>
    bool TrySit(Actor actor);
    
    /// <summary>
    /// Actor가 일어납니다.
    /// </summary>
    /// <param name="actor">일어날 Actor</param>
    void StandUp(Actor actor);
    
    /// <summary>
    /// Actor가 앉아있는지 확인합니다.
    /// </summary>
    /// <param name="actor">확인할 Actor</param>
    /// <returns>앉아있으면 true</returns>
    bool IsActorSeated(Actor actor);
    
    /// <summary>
    /// 현재 사용 중인지 확인합니다.
    /// </summary>
    /// <returns>사용 중이면 true</returns>
    bool IsOccupied();
}
