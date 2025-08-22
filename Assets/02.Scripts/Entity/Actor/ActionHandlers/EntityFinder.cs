using System;
using System.Collections.Generic;
using Agent;
using UnityEngine;

namespace Agent.ActionHandlers
{
    /// <summary>
    /// 엔티티 검색을 위한 유틸리티 클래스
    /// </summary>
    public static class EntityFinder
    {
        /// <summary>
        /// 이름으로 엔티티를 찾습니다.
        /// </summary>
        public static Entity FindEntityByName(Actor actor, string entityName)
        {
            if (string.IsNullOrEmpty(entityName))
                return null;

            // 1. 현재 위치의 상호작용 가능한 엔티티들에서 검색
            if (actor is MainActor thinkingActor)
            {
                var interactableEntities = thinkingActor.sensor.GetInteractableEntities();

                // Actor 검색
                if (interactableEntities.actors.ContainsKey(entityName))
                {
                    return interactableEntities.actors[entityName];
                }

                // Item 검색
                if (interactableEntities.items.ContainsKey(entityName))
                {
                    return interactableEntities.items[entityName];
                }

                // Building 검색
                if (interactableEntities.buildings.ContainsKey(entityName))
                {
                    return interactableEntities.buildings[entityName];
                }

                // Prop 검색
                if (interactableEntities.props.ContainsKey(entityName))
                {
                    return interactableEntities.props[entityName];
                }

                // 2. 현재 위치의 모든 엔티티들에서 검색 (더 넓은 범위)
                var lookableEntities = thinkingActor.sensor.GetLookableEntities();
                if (lookableEntities.ContainsKey(entityName))
                {
                    return lookableEntities[entityName];
                }
            }
            else
            {
                Debug.LogWarning($"[{actor.Name}] sensor 기능을 사용할 수 없습니다.");
            }

            // 3. LocationService를 통한 검색 (전체 월드)
            var locationService = Services.Get<ILocationService>();
            var currentArea = locationService.GetArea(actor.curLocation);
            if (currentArea != null)
            {
                var allEntities = locationService.Get(currentArea, actor);
                foreach (var entity in allEntities)
                {
                    if (entity.Name == entityName)
                    {
                        return entity;
                    }
                }
            }

            Debug.LogWarning($"[{actor.Name}] 엔티티를 찾을 수 없음: {entityName}");
            return null;
        }

        /// <summary>
        /// 이름으로 Actor를 찾습니다.
        /// </summary>
        public static Actor FindActorByName(Actor actor, string actorName)
        {
            if (string.IsNullOrEmpty(actorName))
                return null;

            // 1. 현재 위치의 상호작용 가능한 Actor들에서 검색
            if (actor is MainActor thinkingActor)
            {
                var interactableEntities = thinkingActor.sensor.GetInteractableEntities();
                if (interactableEntities.actors.ContainsKey(actorName))
                {
                    return interactableEntities.actors[actorName];
                }
            }
            else
            {
                Debug.LogWarning($"[{actor.Name}] sensor 기능을 사용할 수 없습니다.");
            }

            // 2. LocationService를 통한 검색
            var locationService = Services.Get<ILocationService>();
            var currentArea = locationService.GetArea(actor.curLocation);
            if (currentArea != null)
            {
                var actors = locationService.GetActor(currentArea, actor);
                foreach (var foundActor in actors)
                {
                    if (foundActor.Name == actorName)
                    {
                        return foundActor;
                    }
                }
            }

            Debug.LogWarning($"[{actor.Name}] Actor를 찾을 수 없음: {actorName}");
            return null;
        }

        /// <summary>
        /// 이름으로 아이템을 찾습니다.
        /// </summary>
        public static Item FindItemByName(Actor actor, string itemName)
        {
            if (string.IsNullOrEmpty(itemName))
                return null;

            // 1. 현재 위치의 상호작용 가능한 Item들에서 검색
            if (actor is MainActor thinkingActor)
            {
                var interactableEntities = thinkingActor.sensor.GetInteractableEntities();
                if (interactableEntities.items.ContainsKey(itemName))
                {
                    return interactableEntities.items[itemName];
                }
            }
            else
            {
                Debug.LogWarning($"[{actor.Name}] sensor 기능을 사용할 수 없습니다.");
            }

            // 2. LocationService를 통한 검색
            var locationService = Services.Get<ILocationService>();
            var currentArea = locationService.GetArea(actor.curLocation);
            if (currentArea != null)
            {
                var items = locationService.GetItem(currentArea);
                foreach (var item in items)
                {
                    if (item.Name == itemName)
                    {
                        return item;
                    }
                }
            }

            Debug.LogWarning($"[{actor.Name}] 아이템을 찾을 수 없음: {itemName}");
            return null;
        }
    }
}
