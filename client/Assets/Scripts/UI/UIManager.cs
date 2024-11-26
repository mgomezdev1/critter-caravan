using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public struct ColorScore
{
    public int score;
    public int maxScore;
    public CritterColor color;
    public ColorScore(int score, int maxScore, CritterColor color)
    {
        this.score = score;
        this.maxScore = maxScore;
        this.color = color;
    }

    public static ColorScore operator +(ColorScore left, ColorScore right)
    {
        return new ColorScore(left.score + right.score, left.maxScore + right.maxScore, left.color);
    }

    public override readonly string ToString()
    {
        return $"({color.colorName} {score}/{maxScore})";
    }
}

public class UIManager : MonoBehaviour
{
    [SerializeField] private UIDocument _document;

    VisualElement toolBar;
    List<Button> speedButtons = new();

    private void Awake()
    {
        _document = GetComponent<UIDocument>();

        Button playButton = Q<Button>("PlayButton");
        Button editButton = Q<Button>("EditButton");
        playButton.clicked += HandlePlay;
        editButton.clicked += HandleEdit;

        toolBar = Q("ToolBar");
        Button toolMenuToggleButton = Q<Button>("ToolBarShowButton");
        toolMenuToggleButton.clicked += ToggleToolMenu;

        VisualElement fallthrough = Q(null, "fallthrough");

        fallthrough.RegisterCallback<MouseDownEvent>(HandleWorldMouseDown, TrickleDown.TrickleDown);

        foreach (var speedButton in Q("SpeedSettings").Query<Button>(className: "speed-button").ToList())
        {
            if (speedButton.dataSource is not SpeedSetting setting) continue;
            speedButtons.Add(speedButton);
            speedButton.clicked += () => { HandleSetSpeed(speedButton, setting.speed); };
        }
    }

    private void Start()
    {
        HandleMode(WorldManager.Instance.GameMode);
    }

    public T Q<T>(string name = null, string className = null) where T : VisualElement
    {
        return _document.rootVisualElement.Q<T>(name, className);
    }
    public VisualElement Q(string name = null, string className = null)
    {
        return _document.rootVisualElement.Q(name, className);
    }
    public IEnumerable<T> Query<T>(string name = null, string className = null) where T : VisualElement
    {
        return _document.rootVisualElement.Query<T>(name, className).ToList();
    }

    private VisualElement BuildScoreVisualizer(ColorScore score)
    {
        Label label = new($"{score.score} / {score.maxScore}");
        label.style.color = score.color.color;
        label.pickingMode = PickingMode.Ignore;
        return label;
    }

    public void SetScores(IEnumerable<ColorScore> scores)
    {
        VisualElement holder = _document.rootVisualElement.Q("ScoreHolder");
        if (holder == null)
        {
            Debug.LogError($"Error while setting scores, no ScoreHolder UI element can be found");
            return;
        }

        holder.Clear();
        foreach (var score in scores)
        {
            var component = BuildScoreVisualizer(score);
            holder.Add(component);
        }
    }
    
    public void HandleMode(GameMode gameMode)
    {
        switch (gameMode)
        {
            case GameMode.Play: 
                HandlePlay(); 
                return;
            case GameMode.Setup:
            case GameMode.LevelEdit:
                HandleEdit(); 
                return;
        }
    }
    public void HandlePlay()
    {
        if (!WorldManager.Instance.SetGameMode(GameMode.Play)) return;
        EnsureSpeedSelected(WorldManager.Instance.World.TimeSpeed);

        _document.rootVisualElement.EnableInClassList("play-mode", true);
        _document.rootVisualElement.EnableInClassList("edit-mode", false);
    }
    public void HandleEdit()
    {
        if (!WorldManager.Instance.SetGameMode(GameMode.Setup)) return;

        _document.rootVisualElement.EnableInClassList("edit-mode", true);
        _document.rootVisualElement.EnableInClassList("play-mode", false);
    }
    bool toolMenuDisplayed = false;
    public void ToggleToolMenu()
    {
        toolMenuDisplayed = !toolMenuDisplayed;
        Debug.Log($"Toggling tool menu displayed to {toolMenuDisplayed}");
        toolBar.EnableInClassList("collapsed", !toolMenuDisplayed);
    }

    public void HandleWorldMouseDown(MouseDownEvent evt)
    {
        if (evt.button != 0)
        {
            Debug.Log("Detected fallthrough non-left-click button press, dropping event...");
            return;
        }
        Debug.Log("Passing world-space mouse click to world handler");
        WorldManager.Instance.HandleWorldMouseDown();
    }

    private void HandleSetSpeed(Button clickedButton, float speed)
    {
        foreach (var button in speedButtons)
        {
            button.EnableInClassList("selected", button == clickedButton);
        }
        WorldManager.Instance.World.TimeSpeed = speed;
    }
    private void EnsureSpeedSelected(float speed)
    {
        Button bestButton = speedButtons.First();
        var bestDiff = Mathf.Abs((bestButton.dataSource as SpeedSetting).speed - speed);
        foreach (var button in speedButtons)
        {
            button.EnableInClassList("selected", false);
            float btnSpeed = (button.dataSource as SpeedSetting).speed;
            float diff = Mathf.Abs(speed - btnSpeed);
            if (diff < bestDiff)
            {
                bestButton = button;
                bestDiff = diff;
            }
        }
        bestButton.EnableInClassList("selected", true);
    }
}
