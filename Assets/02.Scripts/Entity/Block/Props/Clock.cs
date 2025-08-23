public class Clock : Prop
{
    public override string Get()
    {
        return System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
