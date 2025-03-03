using System.Collections.Generic;
using UnityEngine;

public abstract class Actor : Entity
{
    public int Money;
    public iPhone iPhone;
    public string Name
    {
        get => AbsoluteKey;
    }

    [SerializeField]
    private SerializableDictionary<string, Entity> entities = new(); // Key is Entity's Relative Key, Created by AbsoluteKey and curLocation. ex. "iPhone on my right hand".
}
