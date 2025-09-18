using UnityEngine;

public class Napkin : Item
{
    [Header("Napkin Settings")]
    public string brand = "일반";
    
    public override string ToString()
    {
        return Get();
    }
}
