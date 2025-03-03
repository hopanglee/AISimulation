public class Bed : Block
{
    public override string Interact(Actor actor)
    {
        return Sleep(actor);
    }

    private string Sleep(Actor actor)
    {
        actor.Sleep();
        return $"{actor.Name} fell into a deep sleep.";
    }

    public override string Get()
    {
        throw new System.NotImplementedException();
    }
}
