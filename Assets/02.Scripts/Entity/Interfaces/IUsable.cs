using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>
/// 사용 가능한 엔티티를 나타내는 인터페이스
/// </summary>
public interface IUsable
{
    /// <summary>
    /// 엔티티를 사용합니다.
    /// </summary>
    /// <param name="actor">사용하는 Actor</param>
    /// <param name="variable">사용 시 필요한 추가 변수</param>
    /// <returns>사용 결과 메시지</returns>
    UniTask<string> Use(Actor actor, object variable, CancellationToken token = default);
}

