using System;
using System.Collections.Generic;
using System.Linq;
using Agent;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// 캐릭터의 성격 변화를 관리하는 매니저
/// </summary>
public class PersonalityManager
{
    private readonly string characterName;

    public PersonalityManager(string characterName)
    {
        this.characterName = characterName;
    }

    /// <summary>
    /// 성격 변화를 info.json에 적용합니다.
    /// </summary>
    /// <param name="changeResult">성격 변화 분석 결과</param>
    /// <returns>적용 성공 여부</returns>
    public async UniTask<bool> ApplyPersonalityChangeAsync(PersonalityChangeAgent.PersonalityChangeResult changeResult)
    {
        try
        {
            if (!changeResult.has_personality_change)
            {
                Debug.Log($"[PersonalityManager] {characterName}: 성격 변화 없음");
                return true;
            }

            // CharacterMemoryManager를 통해 캐릭터 정보 로드
            var memoryManager = new CharacterMemoryManager(characterName);
            var characterInfo = memoryManager.GetCharacterInfo();

            // 현재 성격 특성 가져오기
            var currentPersonality = characterInfo.Personality ?? new List<string>();

            Debug.Log($"[PersonalityManager] {characterName}: 현재 성격 특성: [{string.Join(", ", currentPersonality)}]");

            // CharacterInfo의 메서드를 사용하여 성격 특성 수정
            var traitsRemoved = new List<string>();
            var traitsAdded = new List<string>();

            // 제거할 특성들 처리
            foreach (var traitToRemove in changeResult.traits_to_remove)
            {
                if (characterInfo.RemovePersonalityTrait(traitToRemove))
                {
                    traitsRemoved.Add(traitToRemove);
                    Debug.Log($"[PersonalityManager] {characterName}: 성격 특성 제거: {traitToRemove}");
                }
                else
                {
                    Debug.LogWarning($"[PersonalityManager] {characterName}: 제거하려는 특성이 없음: {traitToRemove}");
                }
            }

            // 추가할 특성들 처리
            foreach (var traitToAdd in changeResult.traits_to_add)
            {
                characterInfo.AddPersonalityTrait(traitToAdd);
                traitsAdded.Add(traitToAdd);
                Debug.Log($"[PersonalityManager] {characterName}: 성격 특성 추가: {traitToAdd}");
            }

            // 실제 변화가 있었는지 확인
            if (traitsRemoved.Count == 0 && traitsAdded.Count == 0)
            {
                Debug.Log($"[PersonalityManager] {characterName}: 실제 성격 변화 없음");
                return true;
            }

            // CharacterMemoryManager를 통해 저장 (CharacterInfo의 메서드들이 이미 데이터를 수정함)
            var success = await memoryManager.SaveCharacterInfoAsync();

            Debug.Log($"[PersonalityManager] {characterName}: 성격 변화 적용 완료");
            Debug.Log($"[PersonalityManager] {characterName}: 새로운 성격 특성: [{string.Join(", ", characterInfo.Personality)}]");
            Debug.Log($"[PersonalityManager] {characterName}: 변화 이유: {changeResult.reasoning}");

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PersonalityManager] {characterName}: 성격 변화 적용 실패: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 현재 성격 특성을 가져옵니다.
    /// </summary>
    /// <returns>성격 특성 리스트</returns>
    public List<string> GetCurrentPersonality()
    {
        try
        {
            var memoryManager = new CharacterMemoryManager(characterName);
            var characterInfo = memoryManager.GetCharacterInfo();

            return characterInfo.Personality ?? new List<string>();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PersonalityManager] {characterName}: 성격 특성 로드 실패: {ex.Message}");
            return new List<string>();
        }
    }

    /// <summary>
    /// 현재 기질을 가져옵니다.
    /// </summary>
    /// <returns>기질 리스트</returns>
    public List<string> GetCurrentTemperament()
    {
        try
        {
            var memoryManager = new CharacterMemoryManager(characterName);
            var characterInfo = memoryManager.GetCharacterInfo();

            return characterInfo.Temperament ?? new List<string>();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PersonalityManager] {characterName}: 기질 로드 실패: {ex.Message}");
            return new List<string>();
        }
    }


    /// <summary>
    /// 성격과 기질의 요약 정보를 가져옵니다.
    /// </summary>
    /// <returns>요약 정보</returns>
    public string GetPersonalitySummary()
    {
        try
        {
            var temperament = GetCurrentTemperament();
            var personality = GetCurrentPersonality();

            return $"기질(변하지 않음): [{string.Join(", ", temperament)}]\n" +
                   $"성격(변할 수 있음): [{string.Join(", ", personality)}]";
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PersonalityManager] {characterName}: 성격 요약 생성 실패: {ex.Message}");
            return "성격 정보를 가져올 수 없습니다.";
        }
    }
}
