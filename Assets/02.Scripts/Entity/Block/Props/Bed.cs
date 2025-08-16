public class Bed : Prop
{
    public override string Interact(Actor actor)
    {
        return Sleep(actor);
    }

    private string Sleep(Actor actor)
    {
        if (actor is MainActor thinkingActor)
        {
            thinkingActor.Sleep();
            return $"{actor.Name} fell into a deep sleep.";
        }
        else
        {
            return $"{actor.Name} cannot sleep (not a thinking actor).";
        }
    }

    public override string Get()
    {
        throw new System.NotImplementedException();
    }
}
