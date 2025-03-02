using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class SerializableDictionary<TKey, TValue>
    : IDictionary<TKey, TValue>,
        ISerializationCallbackReceiver
{
    // 직렬화를 위한 리스트
    [SerializeField]
    private List<TKey> keys = new();

    [SerializeField]
    private List<TValue> values = new();

    // 런타임에 사용할 Dictionary
    private Dictionary<TKey, TValue> dictionary = new();

    #region IDictionary<TKey, TValue> Implementation

    public TValue this[TKey key]
    {
        get => dictionary[key];
        set => dictionary[key] = value;
    }

    public ICollection<TKey> Keys => dictionary.Keys;

    public ICollection<TValue> Values => dictionary.Values;

    public int Count => dictionary.Count;

    public bool IsReadOnly => ((IDictionary<TKey, TValue>)dictionary).IsReadOnly;

    public void Add(TKey key, TValue value)
    {
        dictionary.Add(key, value);
    }

    public bool ContainsKey(TKey key)
    {
        return dictionary.ContainsKey(key);
    }

    public bool Remove(TKey key)
    {
        return dictionary.Remove(key);
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        return dictionary.TryGetValue(key, out value);
    }

    #endregion

    #region ICollection<KeyValuePair<TKey, TValue>> Implementation

    public void Add(KeyValuePair<TKey, TValue> item)
    {
        ((IDictionary<TKey, TValue>)dictionary).Add(item);
    }

    public void Clear()
    {
        dictionary.Clear();
    }

    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        return ((IDictionary<TKey, TValue>)dictionary).Contains(item);
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        ((IDictionary<TKey, TValue>)dictionary).CopyTo(array, arrayIndex);
    }

    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        return ((IDictionary<TKey, TValue>)dictionary).Remove(item);
    }

    #endregion

    #region IEnumerable<KeyValuePair<TKey, TValue>> Implementation

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return dictionary.GetEnumerator();
    }

    #endregion

    #region IEnumerable Implementation

    IEnumerator IEnumerable.GetEnumerator()
    {
        return dictionary.GetEnumerator();
    }

    #endregion

    #region ISerializationCallbackReceiver Implementation

    public void OnBeforeSerialize()
    {
        keys.Clear();
        values.Clear();
        foreach (var kvp in dictionary)
        {
            keys.Add(kvp.Key);
            values.Add(kvp.Value);
        }
    }

    public void OnAfterDeserialize()
    {
        dictionary = new Dictionary<TKey, TValue>();
        int count = Math.Min(keys.Count, values.Count);
        for (int i = 0; i < count; i++)
        {
            // 중복 키가 발생할 경우 무시합니다.
            if (!dictionary.ContainsKey(keys[i]))
            {
                dictionary.Add(keys[i], values[i]);
            }
        }
    }

    #endregion
}
