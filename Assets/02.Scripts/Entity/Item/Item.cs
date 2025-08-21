public abstract class Item : Entity, ICollectible
{
    public string Description;

    /// <summary>
    ///  Use Item Function
    /// </summary>
    /// <param name="actor">actor who use this Item. </param>
    /// <param name="variable"></param>
    /// <returns>result</returns>
    public abstract string Use(Actor actor, object variable);
}
