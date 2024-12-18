using MessagePack.Formatters;
using Networking;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;
using Utils;

#nullable enable
public class LocalLevelStats
{
    private Dictionary<string, LevelCompletionResult> LevelCompletions { get; set; } = new();

    private readonly string userId;
    public string? UserId => userId;
    public LocalLevelStats(string? userId) {
        this.userId = userId ?? "guest";
    }

    public string GetLocalRelativePersistencePath()
    {
        return $"stats/{userId}/levels.json";
    }

    public async Task<bool> TryDownloadServerCompletions()
    {
        if (!SessionManager.IsOnline) { return false; }

        try
        {
            IAsyncEnumerable<LevelCompletionResult> serverCompletions = ServerAPI.Levels.FetchAllCompletions();
            await foreach (var serverCompletion in serverCompletions)
            {
                if (LevelCompletions.TryGetValue(serverCompletion.LevelId, out LevelCompletionResult existingVal))
                {
                    existingVal.Merge(serverCompletion);
                }
                else
                {
                    LevelCompletions.Add(serverCompletion.LevelId, serverCompletion);
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return false;
        }
    }
    public async Task<bool> TryUploadLocalCompletions()
    {
        if (!SessionManager.IsOnline) { return false; }

        try
        {
            var updates = await ServerAPI.Levels.UploadCompletions(LevelCompletions.Values);
            foreach (var update in updates)
            {
                UpdateCache(update);
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return false;
        }
    }
    public async Task LoadLocalCompletions()
    {
        string path = GetLocalRelativePersistencePath();
        LevelCompletionResult[]? results = await Persistence.ReadJsonFile<LevelCompletionResult[]>(path);
        if (results != null)
        {
            foreach (var item in results)
            {
                UpdateCache(item);
            }
        }
    }
    public async Task SaveLocalCompletions()
    {
        string path = GetLocalRelativePersistencePath();
        await Persistence.WriteJsonFile(path, LevelCompletions.Values.ToArray(), true);
    }

    public LevelCompletionResult UpdateCache(LevelCompletionResult newCompletionResult)
    {
        if (LevelCompletions.TryGetValue(newCompletionResult.LevelId, out LevelCompletionResult existing))
        {
            existing.Merge(newCompletionResult);
            return existing;
        }
        else
        {
            LevelCompletions[newCompletionResult.LevelId] = newCompletionResult;
            return newCompletionResult;
        }
    }

    public async Task<LevelCompletionResult> LogLevelCompletion(LevelCompletionResult levelCompletionResult)
    {
        LevelCompletionResult result = UpdateCache(levelCompletionResult);

        if (SessionManager.IsOnline)
        {
            try
            {
                result = await ServerAPI.Levels.UploadCompletion(result);
                LevelCompletions[result.LevelId] = result;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        await SaveLocalCompletions();

        return result;
    }

    public bool IsLevelCompleted(string levelId)
    {
        return LevelCompletions.ContainsKey(levelId);
    }
    public LevelCompletionResult? GetCompletion(string levelId)
    {
        if (LevelCompletions.TryGetValue(levelId, out LevelCompletionResult result))
        {
            return result;
        } 
        else
        {
            return null;
        }
    }
}

public class LevelCompletionResult
{
    [JsonProperty("levelId")]
    public string LevelId { get; set; }
    [JsonProperty("completionDate")]
    public DateTime CompletionDate { get; set; }
    [JsonProperty("bestScore")]
    public int BestScore { get; set; }
    [JsonProperty("bestTime")]
    public float BestTime { get; set; }

    [JsonIgnore]
    public bool FromServer { get; protected set; } = false;
    [JsonIgnore]
    public DateTime LastRefresh { get; protected set; } = DateTime.MinValue;

    public LevelCompletionResult(string levelId, int score, float time)
        : this(levelId, score, time, DateTime.Now) { }
    public LevelCompletionResult(string levelId, int score, float time, DateTime completionDate)
    {
        LevelId = levelId;
        BestScore = score;
        BestTime = time;
        CompletionDate = completionDate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MarkDownloaded()
    {
        MarkDownloaded(DateTime.Now);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MarkDownloaded(DateTime downloadTime)
    {
        FromServer = true;
        LastRefresh = GenericUtils.Max(LastRefresh, downloadTime);
    }

    public void Merge(LevelCompletionResult externalCompletion)
    {
        if (LevelId != externalCompletion.LevelId)
        {
            throw new InvalidOperationException($"Attempted to merge completion results from inconsistent levels. {LevelId} != {externalCompletion.LevelId}");
        }

        BestScore = Mathf.Max(BestScore, externalCompletion.BestScore);
        BestTime = Mathf.Min(BestTime, externalCompletion.BestTime);
        CompletionDate = GenericUtils.Min(CompletionDate, externalCompletion.CompletionDate);
        
        if (externalCompletion.FromServer) MarkDownloaded(externalCompletion.LastRefresh);
    }
}