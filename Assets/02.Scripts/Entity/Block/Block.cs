public abstract class Block : Entity
{
    public string Description;

    public abstract string Interact(Actor actor);
}
