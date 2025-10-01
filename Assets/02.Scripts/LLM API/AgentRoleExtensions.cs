namespace Agent.Tools
{
    public static class AgentRoleExtensions
    {
        public static string ToGeminiRole(this AgentRole role)
        {
            // Gemini Content.Role: "user" 또는 "model"
            switch (role)
            {
                case AgentRole.User:
                case AgentRole.System:
                case AgentRole.Tool:
                case AgentRole.Function:
                    return "user";
                case AgentRole.Assistant:
                    return "model";
                default:
                    return "user";
            }
        }

        public static string ToGPTRole(this AgentRole role)
        {
            // OpenAI Chat role 문자열로 매핑
            switch (role)
            {
                case AgentRole.System:
                    return "system";
                case AgentRole.User:
                    return "user";
                case AgentRole.Assistant:
                    return "assistant";
                case AgentRole.Tool:
                case AgentRole.Function:
                    return "tool"; // function은 tool로 통일
                default:
                    return "user";
            }
        }

        public static Anthropic.SDK.Messaging.RoleType ToAnthropicRole(this AgentRole role)
        {
            switch (role)
            {
                case AgentRole.System:
                case AgentRole.User:
                case AgentRole.Tool:
                case AgentRole.Function:
                    return Anthropic.SDK.Messaging.RoleType.User;

                case AgentRole.Assistant:
                    return Anthropic.SDK.Messaging.RoleType.Assistant;

                default:
                    return Anthropic.SDK.Messaging.RoleType.User;
            }
        }
    }
}


