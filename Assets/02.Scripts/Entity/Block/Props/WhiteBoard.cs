using UnityEngine;

public class WhiteBoard : Prop
{
    [Header("White Board Settings")]
    public bool isClean = true;
    public string currentText = "";
    
    public override string Get()
    {
        if (isClean)
        {
            return "깨끗한 화이트보드입니다.";
        }
        else
        {
            return $"화이트보드에 '{currentText}'가 적혀있습니다.";
        }
    }
    
    public override string Interact(Actor actor)
    {
        return "화이트보드와 상호작용할 수 있습니다.";
    }
}
