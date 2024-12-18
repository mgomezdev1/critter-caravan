using Extensions;
using Networking;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

#nullable enable
public class LoginUIManager : BaseUIManager
{
    StringField UsernameField { get; set; }
    StringField PasswordField { get; set; }
    Label GlobalErrorLabel { get; set; }

    [SerializeField] bool autoCheckInGuests = false;

    void Awake()
    {
        _document = GetComponent<UIDocument>();

        UsernameField = Q<StringField>("UsernameField");
        PasswordField = Q<StringField>("PasswordField");
        GlobalErrorLabel = Q<Label>("GlobalErrorLabel");
        GlobalErrorLabel.style.display = DisplayStyle.None;

        foreach (var label in Query<Label>())
        {
            if (label == null) continue;
            label.text = FillInFormatText(label.text);
        }

        Button loginButton = Q<Button>("LoginButton");
        loginButton.clicked += HandleLogin;

        Button guestButton = Q<Button>("GuestButton");
        guestButton.clicked += HandleGuestSignIn;
    }

    private async void Start()
    {
        var session = await SessionManager.GetSessionAsync();
        if (session.IsGuest && autoCheckInGuests) 
        { 
            HandleGuestSignIn(); 
        }
        else if (!session.IsGuest && session.UserToken != null)
        {
            if (!session.TokenExpiration.HasValue || session.TokenExpiration.Value < DateTime.Now + TimeSpan.FromSeconds(ServerAPI.TIMEOUT))
            {
                GlobalErrorLabel.text = $"Your login information expired. Please log in again.";
                GlobalErrorLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                try
                {
                    await SessionManager.LoginWithToken(session.UserToken);
                    await LoadLevelsScene();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }
    }

    private async void HandleLogin()
    {
        UserLogin login = new(UsernameField.Value ?? string.Empty, PasswordField.Value ?? string.Empty);

        try
        {
            await SessionManager.Login(login);

            await LoadLevelsScene();
        }
        catch (ServerAPIException e)
        {
            ServerAggregateValidationResult? formValidation = null;
            if (e.ResponseBody != null)
            {
                formValidation = JsonConvert.DeserializeObject<ServerAggregateValidationResult>(e.ResponseBody);
            }

            if (formValidation != null)
            {
                Dictionary<string, DataField> fields = new() {
                    { "username", UsernameField },
                    { "password", PasswordField }
                };
                ServerAPI.ShowServerFormErrors(formValidation, fields, GlobalErrorLabel);
            }
            else
            {
                GlobalErrorLabel.visible = true;
                GlobalErrorLabel.text = e.Message;
            }
        }
        catch (Exception e) {
            Debug.LogError($"Unhandled exception when attempting to log in: {e}");
            return;
        }
    }

    private async void HandleGuestSignIn()
    {
        await SessionManager.SetGuestSession(User.GuestUser);
        await LoadLevelsScene();
    }

    private async Task LoadLevelsScene()
    {
        Scene target = SceneManager.GetSceneByBuildIndex(1);
        List<Task> tasks = new() { 
            
        };
        if (!target.isLoaded)
        {
            tasks.Add(SceneManager.LoadSceneAsync(target.buildIndex, LoadSceneMode.Single).ToTask());
        }
        await Task.WhenAll(tasks);
        if (!SceneManager.SetActiveScene(target))
        {
            throw new Exception($"Unable to set active scene to target. Did loading fail?");
        }
    }
}
