using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>
/// 상호작용 가능한 엔티티를 나타내는 인터페이스
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// Actor와 상호작용합니다.
    /// </summary>
    /// <param name="actor">상호작용하는 Actor</param>
    /// <param name="cancellationToken">상호작용 취소 토큰</param>
    /// <returns>상호작용 결과 메시지</returns>
    UniTask<string> Interact(Actor actor, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Actor의 HandItem을 먼저 체크한 후 상호작용을 시도합니다.
    /// </summary>
    /// <param name="actor">상호작용하는 Actor</param>
    /// <param name="cancellationToken">상호작용 취소 토큰</param>
    /// <returns>상호작용 결과 메시지</returns>
    UniTask<string> TryInteract(Actor actor, CancellationToken cancellationToken = default);
}
