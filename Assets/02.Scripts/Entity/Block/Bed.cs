public class Bed : Block
{
    public override string Interact(Actor actor)
    {
        return Sleep(actor);
    }

    private string Sleep(Actor actor)
    {
        actor.Sleep();
        return $"{actor.Name}은 깊게 잠이 들었다.";
    }

    public override string Get()
    {
        throw new System.NotImplementedException();
    }
}
