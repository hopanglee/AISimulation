using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Refrigerator : InventoryBox
{
    [Header("Refrigerator Settings")]
    [SerializeField] private int maxFoodItems = 10; // 최대 보관 가능한 음식 개수
    [SerializeField] private List<Food> storedFoods = new List<Food>(); // 보관된 음식들

    public override string Interact(Actor actor)
    {
        return default;
    }

    /// <summary>
    /// 냉장고 내용물을 보여줍니다.
    /// </summary>
    private string ShowRefrigeratorContents(Actor actor)
    {
        if (storedFoods.Count == 0)
        {
            return $"{actor.Name} opened the refrigerator. It's empty.";
        }

        string foodList = string.Join(", ", storedFoods.Select(food => food.Name));
        return $"{actor.Name} opened the refrigerator. Contents: {foodList}";
    }

    /// <summary>
    /// 냉장고에 음식을 저장합니다.
    /// </summary>
    public string StoreFood(Actor actor, Food food)
    {
        if (storedFoods.Count >= maxFoodItems)
        {
            return $"{actor.Name} tried to store {food.Name} in the refrigerator, but it's full.";
        }

        if (storedFoods.Contains(food))
        {
            return $"{actor.Name} tried to store {food.Name} in the refrigerator, but it's already there.";
        }

        storedFoods.Add(food);
        food.curLocation = this;
        food.transform.SetParent(transform);
        food.transform.localPosition = Vector3.zero;

        return $"{actor.Name} stored {food.Name} in the refrigerator.";
    }

    /// <summary>
    /// 냉장고에서 음식을 꺼냅니다.
    /// </summary>
    public string TakeFood(Actor actor, string foodName)
    {
        Food foodToTake = storedFoods.FirstOrDefault(food => food.Name == foodName);

        if (foodToTake == null)
        {
            return $"{actor.Name} looked for {foodName} in the refrigerator, but couldn't find it.";
        }

        if (!actor.PickUp(foodToTake))
        {
            return $"{actor.Name} tried to take {foodName} from the refrigerator, but their hands and inventory are full.";
        }

        storedFoods.Remove(foodToTake);
        return $"{actor.Name} took {foodName} from the refrigerator.";
    }

    /// <summary>
    /// 냉장고에 저장된 모든 음식 목록을 반환합니다.
    /// </summary>
    public List<Food> GetStoredFoods()
    {
        return new List<Food>(storedFoods);
    }

    /// <summary>
    /// 냉장고가 비어있는지 확인합니다.
    /// </summary>
    public bool IsEmpty()
    {
        return storedFoods.Count == 0;
    }

    /// <summary>
    /// 냉장고가 가득 찼는지 확인합니다.
    /// </summary>
    public bool IsFull()
    {
        return storedFoods.Count >= maxFoodItems;
    }

    /// <summary>
    /// 냉장고의 남은 공간을 반환합니다.
    /// </summary>
    public int GetRemainingSpace()
    {
        return maxFoodItems - storedFoods.Count;
    }

    public override string Get()
    {
        if (storedFoods.Count == 0)
        {
            return "The refrigerator is empty.";
        }

        string foodList = string.Join(", ", storedFoods.Select(food => food.Name));
        return $"Refrigerator contains: {foodList}";
    }
}