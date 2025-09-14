public class Clock : Prop
{
    public override string Get()
    {
        var timeService = Services.Get<ITimeService>();
        return timeService.CurrentTime.ToIsoString();
    }
}
