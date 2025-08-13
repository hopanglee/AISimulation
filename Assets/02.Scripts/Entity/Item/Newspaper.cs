using UnityEngine;

[System.Serializable]
public class Newspaper : Book
{
    [Header("Newspaper Properties")]
    public string date = "2024-01-01";
    
    public override string Get()
    {
        return $"신문 - {date}";
    }
    
    public override string ToString()
    {
        return $"신문 - {date}";
    }
}
