using System.Collections.Generic;
using UnityEngine;

public abstract class Actor : Entity
{
    [SerializeField]
    private SerializableDictionary<string, Entity> entities = new(); // Key is Entity's Relative Key, Created by AbsoluteKey and curLocation. ex. "iPhone on my right hand".
}
