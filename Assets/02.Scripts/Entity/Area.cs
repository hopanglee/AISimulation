using System.Collections.Generic;
using UnityEngine;

public class Area : MonoBehaviour, ILocation
{
    public string locationName
    {
        get => throw new System.NotImplementedException();
        set => throw new System.NotImplementedException();
    }
    public ILocation curLocation
    {
        get => throw new System.NotImplementedException();
        set => throw new System.NotImplementedException();
    }
    public string preposition
    {
        get => throw new System.NotImplementedException();
        set => throw new System.NotImplementedException();
    }
    public bool IsHideChild { get; set; }

    public List<Area> connectedAreas = new();
    public SerializableDictionary<Area, Transform> toMovePos = new(); // area : from, transform : target pos

    public string LocationToString()
    {
        throw new System.NotImplementedException();
    }
}
