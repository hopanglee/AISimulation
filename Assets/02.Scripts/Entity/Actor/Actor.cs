using System.Collections.Generic;
using UnityEngine;

public abstract class Actor : Entity
{
    #region Varaible
    public int Money;
    public iPhone iPhone;

    [SerializeField]
    private SerializableDictionary<string, Entity> entities = new(); // Key is Entity's Relative Key, e.g., "iPhone on my right hand".

    [Header("Physical Needs (0 ~ 100)")]
    [Range(0, 100)]
    public int Hunger; // 배고픔

    [Range(0, 100)]
    public int Thirst; // 갈증

    [Range(0, 100)]
    public int Stamina; // 피로 혹은 신체적 지침

    [Header("Mental State")]
    // 정신적 쾌락: 0 이상의 값 (예, 만족감, 즐거움)
    public int MentalPleasure;

    [Range(0, 100)]
    public int Stress; // 스트레스 수치

    [Header("Sleepiness")]
    [Range(0, 100)]
    public int Sleepiness; // 졸림 수치. 일정 수치(예: 80 이상) 이상이면 강제로 잠들게 할 수 있음.
    #endregion

    #region Function
    public virtual void Sleep()
    {
        ;
    }

    public virtual void Death()
    {
        ;
    }

    protected void UpdateEntity()
    {
        // entities 딕셔너리 초기화 (reset)
        entities = new SerializableDictionary<string, Entity>();

        // 현재 Actor의 curLocation에 있는 모든 Entity 가져옴
        var curEntities = Services.Get<LocationManager>().Get(curLocation);

        // 각 Entity를 돌면서 key를 Entity의 LocationToString() 값으로 지정하여 entities에 추가
        foreach (Entity entity in curEntities)
        {
            string key = entity.LocationToString();
            entities[key] = entity;
        }
    }
    #endregion
}
