using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using UnityEditor;
#endif

public interface IService
{
    public UniTask Initialize();
}

public static class Services
{
    private static readonly Dictionary<Type, object> services = new();

    public static T Provide<T>(T service)
        where T : IService
    {
        var type = typeof(T);
        if (services.ContainsKey(type))
        {
            services[type] = service;
        }
        else
        {
            services.Add(type, service);
        }
        return service;
    }

    public static T Provide<T>()
        where T : IService, new()
    {
        var service = new T();
        return Provide(service);
    }

    public static T Get<T>()
        where T : IService, new()
    {
        var type = typeof(T);
        if (services.TryGetValue(type, out var service))
        {
            return (T)service;
        }
        else
        {
            var provided = Provide<T>();
            return provided;
        }
    }

#if UNITY_EDITOR
    public class ServiceWindow : OdinEditorWindow
    {
        [MenuItem("Window/Services")]
        public static void OpenWindow() => GetWindow<ServiceWindow>();

        [ShowInInspector]
        public static Dictionary<Type, object> Services => services;
    }
#endif
}
