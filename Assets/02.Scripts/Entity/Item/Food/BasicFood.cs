using UnityEngine;

public class BasicFood : Food
{
    public override string Get()
    {
       
        return $"{Name} - 영양가: {Nutrition}";
    }
    
    public override string Eat(Actor actor)
    {
        
        return base.Eat(actor);
    }
}
