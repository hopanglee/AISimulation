public class KamiyaHouse : House
{
    public override string Interact(Actor actor)
    {
        base.Interact(actor);
        return default;
    }

    public override string Get()
    {
        throw new System.NotImplementedException();
    }
}
