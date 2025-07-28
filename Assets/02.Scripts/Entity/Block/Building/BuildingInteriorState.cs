using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 빌딩 내부 상태를 관리하는 클래스
/// 자연어 형태로 상태를 저장하고 관리
/// </summary>
public class BuildingInteriorState
{
    private string buildingName;
    private List<string> actors; // 현재 빌딩에 있는 Actor들
    private List<string> interiorObjects; // 내부 오브젝트들 (자연어로 표현)
    private Dictionary<string, object> objectStates; // 오브젝트별 상태

    public BuildingInteriorState(string buildingName)
    {
        this.buildingName = buildingName;
        this.actors = new List<string>();
        this.interiorObjects = new List<string>();
        this.objectStates = new Dictionary<string, object>();
        
        InitializeDefaultInterior();
    }

    /// <summary>
    /// 기본 내부 상태 초기화
    /// </summary>
    private void InitializeDefaultInterior()
    {
        switch (buildingName.ToLower())
        {
            case "cafe":
                interiorObjects.AddRange(new string[]
                {
                    "창가 테이블 (좌표: 1,1) - 깨끗함",
                    "중앙 테이블 (좌표: 2,2) - 깨끗함", 
                    "카운터 (좌표: 3,1) - 깨끗함",
                    "커피머신 (좌표: 3,2) - 정상작동",
                    "냉장고 (좌표: 3,3) - 정상작동",
                    "화장실 (좌표: 4,4) - 깨끗함"
                });
                break;
                
            case "hospital":
                interiorObjects.AddRange(new string[]
                {
                    "접수대 (좌표: 1,1) - 깨끗함",
                    "대기실 의자 (좌표: 2,1) - 깨끗함",
                    "진료실 (좌표: 3,1) - 깨끗함",
                    "약국 (좌표: 4,1) - 정상영업",
                    "응급실 (좌표: 5,1) - 대기중",
                    "화장실 (좌표: 6,1) - 깨끗함"
                });
                break;
                
            case "school":
                interiorObjects.AddRange(new string[]
                {
                    "교무실 (좌표: 1,1) - 깨끗함",
                    "1학년 교실 (좌표: 2,1) - 깨끗함",
                    "2학년 교실 (좌표: 2,2) - 깨끗함",
                    "도서관 (좌표: 3,1) - 조용함",
                    "체육관 (좌표: 4,1) - 비어있음",
                    "화장실 (좌표: 5,1) - 깨끗함"
                });
                break;
                
            case "theater":
                interiorObjects.AddRange(new string[]
                {
                    "매표소 (좌표: 1,1) - 영업중",
                    "로비 (좌표: 2,1) - 깨끗함",
                    "상영관 A (좌표: 3,1) - 비어있음",
                    "상영관 B (좌표: 3,2) - 비어있음",
                    "매점 (좌표: 4,1) - 영업중",
                    "화장실 (좌표: 5,1) - 깨끗함"
                });
                break;
        }
    }

    /// <summary>
    /// Actor 추가
    /// </summary>
    public void AddActor(string actorName)
    {
        if (!actors.Contains(actorName))
        {
            actors.Add(actorName);
        }
    }

    /// <summary>
    /// Actor 제거
    /// </summary>
    public void RemoveActor(string actorName)
    {
        if (actors.Contains(actorName))
        {
            actors.Remove(actorName);
        }
    }

    /// <summary>
    /// 현재 상태를 자연어로 설명
    /// </summary>
    public string GetCurrentStateDescription()
    {
        var description = new System.Text.StringBuilder();
        description.AppendLine($"=== {buildingName} 내부 상태 ===");
        description.AppendLine($"현재 시간: {GetCurrentTimeString()}");
        description.AppendLine();
        
        // Actor 정보
        if (actors.Count > 0)
        {
            description.AppendLine($"현재 있는 사람들: {string.Join(", ", actors)}");
        }
        else
        {
            description.AppendLine("현재 빌딩에 사람이 없습니다.");
        }
        description.AppendLine();
        
        // 내부 오브젝트 정보
        description.AppendLine("내부 오브젝트:");
        foreach (var obj in interiorObjects)
        {
            description.AppendLine($"- {obj}");
        }
        description.AppendLine();
        
        description.AppendLine("=== 상태 끝 ===");
        return description.ToString();
    }

    /// <summary>
    /// 현재 시간 문자열 반환
    /// </summary>
    private string GetCurrentTimeString()
    {
        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;
        return $"{currentTime.hour:D2}:{currentTime.minute:D2}:00";
    }

    /// <summary>
    /// 현재 빌딩에 있는 Actor 목록 반환
    /// </summary>
    public List<string> GetActors()
    {
        return new List<string>(actors);
    }
} 