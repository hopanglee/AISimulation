using System;
using UnityEngine;

public class Mirror : Prop
{
    [Header("Mirror Settings")]
    public bool isClean = true;
    public bool isBroken = false;
    
    public override string Get()
    {

        if(String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"{GetLocalizedStatusDescription()}";
        }
        return $"{LocationToString()}이 있다.";
    }

}
