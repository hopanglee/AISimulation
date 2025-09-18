using System;

public class Clock : Prop
{
    public override string Get()
    {
        var timeService = Services.Get<ITimeService>();
        string status = timeService.CurrentTime.ToIsoString();

        if(String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"{LocationToString()} - {status} {GetLocalizedStatusDescription()}";
        }
        return $"{LocationToString()} - {status}";
    }
}
