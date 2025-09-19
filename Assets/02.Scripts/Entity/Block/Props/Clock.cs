using System;

public class Clock : Prop
{
    public override string Get()
    {
        var timeService = Services.Get<ITimeService>();
        string status = timeService.CurrentTime.ToKoreanString();

        if(String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"{status} {GetLocalizedStatusDescription()}";
        }
        return $"{status}";
    }
}
