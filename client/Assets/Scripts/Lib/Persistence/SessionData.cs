#nullable enable
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

[JsonObject]
public class UserSessionData
{
    public static readonly string SESSION_PATH = "session.json";

    [JsonProperty("userToken")]
    public string? UserToken { get; set; } = null;
    [JsonProperty("isGuest")]
    public bool IsGuest { get; set; } = false;
    [JsonProperty("tokenExpiration")]
    public DateTime? TokenExpiration { get; set; } = null;

    [JsonIgnore]
    public LocalLevelStats? LevelStats { get; set; }

    public UserSessionData(string? token = null, DateTime? tokenExpiration = null, bool isGuest = false)
    {
        UserToken = token;
        TokenExpiration = tokenExpiration;
        IsGuest = isGuest;
    }

    public async Task ClearAndSave()
    {
        UserToken = null;
        IsGuest = false;
        TokenExpiration = null;
        await Save();
    }

    public static async Task<UserSessionData> GetPersistentSession()
    {
        UserSessionData? session = await Persistence.ReadJsonFile<UserSessionData>(SESSION_PATH);
        if (session != null) return session;
        session = new();
        await session.Save();
        return session;
    }
    public static UserSessionData GetPersistentSessionSync()
    {
        UserSessionData? session = Persistence.ReadJsonFileSync<UserSessionData>(SESSION_PATH);
        if (session != null) return session;
        session = new();
        session.SaveSync();
        return session;
    }

    public void SaveSync()
    {
        Persistence.WriteJsonFileSync(SESSION_PATH, this);
    }

    public async Task<bool> Save()
    {
        return await Persistence.WriteJsonFile(SESSION_PATH, this);
    }

    public async Task LoadLevelStats(string? userId)
    {
        if (LevelStats == null || LevelStats.UserId != userId)
        {
            LevelStats = new(userId);
        }
        await LevelStats.LoadLocalCompletions();
        await LevelStats.TryDownloadServerCompletions();
    }
}
