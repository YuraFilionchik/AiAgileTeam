using System;
using System.Collections.Concurrent;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;

namespace AiAgileTeam.Services;

public class SessionData
{
    public AgentGroupChat GroupChat { get; } = new();
    public bool IsConfigured { get; set; } = false;
}

public class SessionStore
{
    public record SessionEntry(SessionData Data, DateTime LastAccessed);

    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new();

    public SessionData GetOrCreate(string sessionId)
    {
        var entry = _sessions.GetOrAdd(sessionId, id => new SessionEntry(new SessionData(), DateTime.UtcNow));
        Touch(sessionId);
        return entry.Data;
    }

    public SessionData? Get(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var entry))
        {
            Touch(sessionId);
            return entry.Data;
        }
        return null;
    }

    public void Touch(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var entry))
        {
            _sessions[sessionId] = entry with { LastAccessed = DateTime.UtcNow };
        }
    }

    public void Remove(string sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
    }
}
