using System;
using UnityEngine;

public class Mirror : Prop
{
    [Header("Mirror Settings")]
    public bool isClean = true;
    public bool isBroken = false;
    
    public override string Get()
    {
        // if (isBroken)
        // {
        //     return "깨진 거울입니다.";
        // }
        
        // if (!isClean)
        // {
        //     return "더러운 거울입니다.";
        // }
        
        // return "깨끗한 거울입니다.";

        if(String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"{GetLocalizedStatusDescription()}";
        }
        return $"{LocationToString()}이 있다.";
    }

}
