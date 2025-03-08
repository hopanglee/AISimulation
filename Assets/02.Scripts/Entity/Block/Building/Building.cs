public abstract class Building : Block
{
    public override string Interact(Actor actor)
    {
        return null;
    }

    public override string Get()
    {
        throw new System.NotImplementedException();
    }
}
