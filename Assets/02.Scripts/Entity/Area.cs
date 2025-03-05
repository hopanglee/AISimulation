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
    public bool IsHide
    {
        get => throw new System.NotImplementedException();
        set => throw new System.NotImplementedException();
    }

    public SerializableDictionary<string, Transform> connectedAreas = new();

    public string LocationToString()
    {
        throw new System.NotImplementedException();
    }
}
