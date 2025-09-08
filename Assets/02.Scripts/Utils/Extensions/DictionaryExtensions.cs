using System.Collections.Generic;

/// <summary>
/// Dictionary 확장 메서드들
/// </summary>
public static class DictionaryExtensions
{
    /// <summary>
    /// Dictionary에서 키에 해당하는 값을 가져오거나, 없으면 기본값을 반환합니다.
    /// </summary>
    /// <typeparam name="TKey">키 타입</typeparam>
    /// <typeparam name="TValue">값 타입</typeparam>
    /// <param name="dict">Dictionary</param>
    /// <param name="key">찾을 키</param>
    /// <param name="defaultValue">기본값</param>
    /// <returns>키에 해당하는 값 또는 기본값</returns>
    public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue defaultValue = default(TValue))
    {
        return dict.ContainsKey(key) ? dict[key] : defaultValue;
    }

    /// <summary>
    /// Dictionary&lt;string, object&gt;에서 키에 해당하는 값을 가져오거나, 없으면 기본값을 반환합니다.
    /// </summary>
    /// <param name="dict">Dictionary</param>
    /// <param name="key">찾을 키</param>
    /// <param name="defaultValue">기본값</param>
    /// <returns>키에 해당하는 값 또는 기본값</returns>
    public static object GetValueOrDefault(this Dictionary<string, object> dict, string key, object defaultValue = null)
    {
        return dict.ContainsKey(key) ? dict[key] : defaultValue;
    }
}
