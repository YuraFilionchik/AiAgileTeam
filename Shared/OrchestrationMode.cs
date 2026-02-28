using System.Text.Json.Serialization;

namespace AiAgileTeam.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrchestrationMode
{
    GroupChat,
    Magentic
}
