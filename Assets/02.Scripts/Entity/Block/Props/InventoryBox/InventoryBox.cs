using System.Collections.Generic;
using UnityEngine;

public abstract class InventoryBox : Prop
{
    public int maxItems;

    public List<Entity> items;
    public List<Transform> transforms;

    public override string Get()
    {
        throw new System.NotImplementedException();
    }

    public override string Interact(Actor actor)
    {
        throw new System.NotImplementedException();
    }
}
