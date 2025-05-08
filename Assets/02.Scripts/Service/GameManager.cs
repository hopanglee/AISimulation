using Cysharp.Threading.Tasks;

public interface IGameService : IService { }

public class GameServcie : IGameService
{
    public async UniTask Initialize()
    {
        return;
    }
}
