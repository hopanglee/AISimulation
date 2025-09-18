using UnityEngine;

[System.Serializable]
public class Magazine : Book
{
    [Header("Magazine Properties")]
    public string category = "일반";
    
    public override string ToString()
    {
        return $"매거진 - {category}";
    }
}
