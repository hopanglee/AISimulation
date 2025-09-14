using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Agent;
using System.Collections.Generic;

public class WhiteBoard : InteractableProp
{
    [Header("White Board Settings")]
    public bool isClean = true;
    public string currentText = "";
    
    public override string Get()
    {
        if (isClean)
        {
            return "깨끗한 화이트보드입니다.";
        }
        else
        {
            return $"화이트보드에 '{currentText}'가 적혀있습니다.";
        }
    }
    
    public override async UniTask<string> Interact(Actor actor, CancellationToken cancellationToken = default)
    {
        await SimDelay.DelaySimMinutes(1, cancellationToken);
        
        try
        {
            // WhiteBoardParameterAgent를 사용하여 지능적인 화이트보드 액션 결정
            var agent = new WhiteBoardParameterAgent(currentText);
            agent.SetActor(actor);

            // ActorManager에서 원본 reasoning과 intention 가져오기
            var actorManager = Services.Get<IActorService>();
            string reasoning = "화이트보드에 메모나 정보를 작성하려고 합니다.";
            string intention = "현재 상황에 적합한 내용을 화이트보드에 기록하려고 합니다.";
            
            if (actorManager != null)
            {
                var actResult = actorManager.GetActResult(actor);
                if (actResult != null)
                {
                    reasoning = actResult.Reasoning;
                    intention = actResult.Intention;
                }
            }

            // Agent로부터 파라미터 생성
            var context = new ParameterAgentBase.CommonContext
            {
                Reasoning = reasoning,
                Intention = intention
            };

            var paramResult = await agent.GenerateParametersAsync(context);

            if (paramResult != null && paramResult.Parameters.TryGetValue("action_type", out var actionTypeObj))
            {
                string actionType = actionTypeObj?.ToString() ?? "write";
                
                // 모든 액션을 write 함수로 처리
                if (paramResult.Parameters.TryGetValue("content", out var contentObj))
                {
                    string content = contentObj?.ToString() ?? "";
                    
                    // action_type에 따라 content 처리
                    switch (actionType)
                    {
                        case "write":
                            // 새로운 내용으로 덮어쓰기
                            WriteText(content);
                            Debug.Log($"[WhiteBoard] {actor.Name}이(가) 화이트보드에 작성: {content} (이유: {reasoning}, 의도: {intention})");
                            return $"화이트보드에 '{content}'를 작성했습니다.";
                            
                        case "update":
                            // 기존 내용에 추가/수정
                            string updatedContent = string.IsNullOrEmpty(currentText) ? content : currentText + "\n" + content;
                            WriteText(updatedContent);
                            Debug.Log($"[WhiteBoard] {actor.Name}이(가) 화이트보드 내용을 업데이트: {content} (이유: {reasoning}, 의도: {intention})");
                            return $"화이트보드 내용을 '{content}'로 업데이트했습니다.";
                            
                        case "erase":
                            // 빈 문자열로 덮어쓰기 (지우기)
                            WriteText("");
                            Debug.Log($"[WhiteBoard] {actor.Name}이(가) 화이트보드를 지웠습니다. (이유: {reasoning}, 의도: {intention})");
                            return "화이트보드를 깨끗하게 지웠습니다.";
                            
                        default:
                            Debug.LogWarning($"[WhiteBoard] 알 수 없는 액션 타입: {actionType}");
                            // 기본적으로 write로 처리
                            WriteText(content);
                            return $"화이트보드에 '{content}'를 작성했습니다.";
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[WhiteBoard] 화이트보드 상호작용 중 오류 발생: {ex.Message}");
            
            // 오류 발생 시 기본 동작으로 fallback
            WriteText("메모");
            return "화이트보드에 기본 메모를 작성했습니다.";
        }
        
        return "화이트보드와 상호작용할 수 있습니다.";
    }
    
    /// <summary>
    /// 화이트보드에 직접 텍스트를 작성합니다.
    /// write, update, erase 모든 기능을 이 함수로 처리합니다.
    /// </summary>
    public void WriteText(string text)
    {
        currentText = text ?? "";
        isClean = string.IsNullOrEmpty(currentText);
    }
    
    /// <summary>
    /// 현재 화이트보드 내용을 가져옵니다.
    /// </summary>
    public string GetCurrentText()
    {
        return currentText;
    }
    
    /// <summary>
    /// 화이트보드가 깨끗한지 확인합니다.
    /// </summary>
    public bool IsClean()
    {
        return isClean;
    }
}
