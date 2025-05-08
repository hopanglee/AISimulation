using JetBrains.Annotations;
using UnityEngine;

[DefaultExecutionOrder(-9999)]
public class BootStrapper : MonoBehaviour
{
    void Awake()
    {
        Services.Provide<IGameService>(new GameServcie());
        Services.Provide<ILocationService>(new LocationService());
    }
}
