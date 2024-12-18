using Extensions;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class LevelSelectUIManager : BaseUIManager
{
    Window levelBrowserWindow;
    Window mainMenu;
    LevelBrowser levelBrowser;

    [SerializeField] private InbuiltLevelCompendium inbuiltLevels;

    private void Awake()
    {
        _document = GetComponent<UIDocument>();

        Button logOutButton = Q<Button>("LogOutButton");
        logOutButton.clicked += HandleLogOut;

        Button playButton = Q<Button>("PlayButton");
        Button levelBrowserButton = Q<Button>("LevelBrowserButton");
        Button levelEditorButton = Q<Button>("LevelEditorButton");

        playButton.clicked += HandleOpenPlayMenu;
        levelBrowserButton.clicked += HandleOpenLevelBrowser;
        levelEditorButton.clicked += HandleOpenLevelEditor;

        Button backToMainButton = Q<Button>("BackToMainButton");
        levelBrowserWindow = new(Q("LevelBrowser"));
        mainMenu = new(Q("MainMenu"));
        RegisterWindow(levelBrowserWindow);
        RegisterWindow(mainMenu).AddWindowButton(backToMainButton, WindowButtonBehaviour.Set);

        Button quitButton = Q<Button>("QuitButton");
        quitButton.clicked += HandleQuit;

        levelBrowser = Q<LevelBrowser>();
    }

    private async void HandleLogOut()
    {
        Scene loginScene = SceneManager.GetSceneByBuildIndex(0);

        List<Task> tasks = new()
        {
            SessionManager.LogOut()
        };
        if (!loginScene.isLoaded)
        {
            tasks.Add(SceneManager.LoadSceneAsync(loginScene.buildIndex, LoadSceneMode.Single).ToTask());
        }
        await Task.WhenAll(tasks);
        SceneManager.SetActiveScene(loginScene);
    }
    private void HandleQuit()
    {
        Application.Quit();
    }

    private void HandleOpenPlayMenu()
    {
        SetActiveWindow(levelBrowserWindow);
        levelBrowser.LevelCompendium = inbuiltLevels;
    }
    private void HandleOpenLevelBrowser()
    { 
        SetActiveWindow(levelBrowserWindow);
    }
    private void HandleOpenLevelEditor()
    {
        SetActiveWindow(levelBrowserWindow);
    }
}
