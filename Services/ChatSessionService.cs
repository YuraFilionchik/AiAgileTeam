using Blazored.LocalStorage;
using AiAgileTeam.Models;

namespace AiAgileTeam.Services;

public class ChatSessionService
{
    private readonly ILocalStorageService _localStorage;
    private const string SessionsKey = "ai_team_sessions";

    public ChatSessionService(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task<List<ChatSession>> GetAllSessionsAsync()
    {
        var sessions = await _localStorage.GetItemAsync<List<ChatSession>>(SessionsKey);
        return sessions ?? new List<ChatSession>();
    }

    public async Task<ChatSession?> GetSessionAsync(string id)
    {
        var sessions = await GetAllSessionsAsync();
        return sessions.FirstOrDefault(s => s.Id == id);
    }

    public async Task SaveSessionAsync(ChatSession session)
    {
        var sessions = await GetAllSessionsAsync();
        var existingIndex = sessions.FindIndex(s => s.Id == session.Id);

        if (existingIndex >= 0)
        {
            sessions[existingIndex] = session;
        }
        else
        {
            sessions.Add(session);
        }

        // Sort by updated descending
        sessions = sessions.OrderByDescending(s => s.UpdatedAt).ToList();
        
        await _localStorage.SetItemAsync(SessionsKey, sessions);
    }

    public async Task DeleteSessionAsync(string id)
    {
        var sessions = await GetAllSessionsAsync();
        sessions.RemoveAll(s => s.Id == id);
        await _localStorage.SetItemAsync(SessionsKey, sessions);
    }
}
