using Newtonsoft.Json;
using Networking;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using Extensions;

#nullable enable
public class SessionManager : PersistentSingletonBehaviour<SessionManager>
{
    public static bool LoggedIn => CurrentUser != null;
    public static bool IsGuest => LoggedIn && AuthToken == null;
    public static bool IsOnline => LoggedIn && AuthToken != null;
    public static User? CurrentUser { get; set; }
    public static string? AuthToken {
        get
        {
            return GetSession().UserToken;
        }
        set
        {
            GetSession().UserToken = value;
        }
    }
    public static DateTime? TokenExpires
    {
        get
        {
            return GetSession().TokenExpiration;
        }
        set
        {
            GetSession().TokenExpiration = value;
        }
    }

    public static TimeSpan AutoRefreshTime { get; private set; } = TimeSpan.FromDays(7);
    public static DateTime? LastRefreshedToken { get; private set; } = null;
    public static TimeSpan RefreshAttemptCooldown { get; private set; } = TimeSpan.FromMinutes(5);

    protected override void Awake()
    {
        base.Awake();
    }

    private UserSessionData? userSessionData;
    public static async Task<UserSessionData> GetSessionAsync()
    {
        if (Instance.userSessionData == null)
        {
            Instance.userSessionData = await UserSessionData.GetPersistentSession();
        }
        return Instance.userSessionData;
    }
    public static UserSessionData GetSession()
    {
        if (Instance.userSessionData == null)
        {
            Instance.userSessionData = UserSessionData.GetPersistentSessionSync();
        }
        return Instance.userSessionData;
    }

    public static async Task<SessionResponse> Login(UserLogin login)
    {
        string jsonPayload = JsonConvert.SerializeObject(login);
        var response = await ServerAPI.PostAsync<SessionResponse>("auth/login", jsonPayload);
        await SetSession(response.User, response.Token, response.ExpirationDate);

        return response;
    }
    public static async Task<SessionResponse> LoginWithToken(string? token = null)
    {
        if (token != null) AuthToken = token;
        if (AuthToken == null)
        {
            throw new InvalidTokenException(string.Empty);
        }

        return await RefreshTokenAsync() ?? 
            throw new InvalidTokenException(AuthToken);
    }

    public static async Task LogOut()
    {
        await ServerAPI.TryDeleteAsync("auth/login");
        await ClearSession();
    }
    public void OnDestroy()
    {
        GetSession().SaveSync();
    }

    public static async Task<SessionResponse?> RefreshTokenAsync()
    {
        if (CurrentUser == null)
        {
            return null;
        }

        try
        {
            var response = await ServerAPI.PostAsync<SessionResponse>("auth/refresh", "{}");
            if (response.User == null || response.User != CurrentUser)
            {
                Debug.LogWarning($"Received mismatched user in token refresh request from server: Expected {CurrentUser}, Got {response.User}.");
            }
            await SetSession(CurrentUser, response.Token, response.ExpirationDate);
            return response;
        }
        catch (ServerAPIInvalidFormatException<SessionResponse>) { throw; }
        catch (Exception e)
        {
            Debug.LogError($"Error while refreshing user token: {e}");
            return null;
        }
    }

    public static async Task SetSession(User user, string authToken, DateTime tokenExpiration)
    {
        if (AuthToken != authToken)
        {
            LastRefreshedToken = DateTime.Now;
        }
        CurrentUser = user;
        AuthToken = authToken;
        TokenExpires = tokenExpiration;
        await GetSession().Save();
    }
    public static async Task SetGuestSession(User user)
    {
        CurrentUser = user;
        AuthToken = null;
        TokenExpires = DateTime.Now;
        await GetSession().Save();
    }

    public static async Task ClearSession()
    {
        CurrentUser = null;
        var session = await GetSessionAsync();
        await session.ClearAndSave();
    }

    public static bool ShouldRefreshToken()
    {
        if (AuthToken == null || CurrentUser == null || TokenExpires == null)
        {
            return false;
        }

        if (TokenExpires.Value.TimeSinceNow() > AutoRefreshTime) return false;
        if (LastRefreshedToken != null && LastRefreshedToken.Value.TimeUntilNow() < RefreshAttemptCooldown) return false;
        return true;
    }
}