using UnityEngine;

[System.Serializable]
public abstract class Food : Item
{
    public override string Use(Actor actor, object variable)
    {
        return Eat(actor);
    }

    public abstract string Eat(Actor actor);
}
