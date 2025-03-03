using Sirenix.OdinInspector.Editor;

public class Clock : Block
{
    public override string Interact(Actor actor)
    {
        return LookClock();
    }

    private string LookClock()
    {
        return GetTime() + "이라고 나와있다.";
    }

    private string GetTime()
    {
        return System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public override string Get()
    {
        throw new System.NotImplementedException();
    }
}
