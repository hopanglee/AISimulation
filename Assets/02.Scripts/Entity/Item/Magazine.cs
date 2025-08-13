using UnityEngine;

[System.Serializable]
public class Magazine : Book
{
    [Header("Magazine Properties")]
    public string category = "일반";
    
    public override string Get()
    {
        return $"매거진 - {category}";
    }
    
    public override string ToString()
    {
        return $"매거진 - {category}";
    }
}
