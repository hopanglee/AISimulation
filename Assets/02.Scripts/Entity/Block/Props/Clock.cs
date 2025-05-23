public class Clock : Prop
{
    public override string Interact(Actor actor)
    {
        return LookClock();
    }

    private string LookClock()
    {
        return GetTime() + " is displayed.";
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
