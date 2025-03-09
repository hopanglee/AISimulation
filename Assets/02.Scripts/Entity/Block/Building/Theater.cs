public class Theater : Building
{
    public override string Interact(Actor actor)
    {
        base.Interact(actor);
        return null;
    }

    public override string Get()
    {
        throw new System.NotImplementedException();
    }
}
