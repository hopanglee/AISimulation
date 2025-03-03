using Cysharp.Threading.Tasks;

public class GameManager : IService
{
    public UniTask Initialize()
    {
        return UniTask.CompletedTask;
    }
}
