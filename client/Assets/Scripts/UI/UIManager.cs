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

public class UIManager : BaseUIManager
{
    VisualElement toolBar;
    readonly List<Button> speedButtons = new();

#pragma warning disable CS8618
    [SerializeField] private ObstacleCompendium compendium;
#pragma warning restore CS8618

    private void Awake()
    {
        _document = GetComponent<UIDocument>();

        Button playButton = Q<Button>("PlayButton");
        Button play2SetupButton = Q<Button>("PlayToSetupButton");
        Button edit2SetupButton = Q<Button>("EditToSetupButton");
        Button editButton = Q<Button>("EditButton");
        playButton.clicked += () => HandleMode(GameMode.Play);
        play2SetupButton.clicked += () => HandleMode(GameMode.Setup);
        edit2SetupButton.clicked += () => HandleMode(GameMode.Setup);
        editButton.clicked += () => HandleMode(GameMode.LevelEdit);

        toolBar = Q("ToolBar");
        Button toolMenuToggleButton = toolBar.Q<Button>("ToolBarShowButton");
        toolMenuToggleButton.clicked += ToggleToolMenu;

        VisualElement fallthrough = Q(null, "fallthrough");

        fallthrough.RegisterCallback<MouseDownEvent>(HandleWorldMouseDown, TrickleDown.TrickleDown);

        foreach (var speedButton in Q("SpeedSettings").Query<Button>(className: "speed-button").ToList())
        {
            if (speedButton.dataSource is not SpeedSetting setting) continue;
            speedButtons.Add(speedButton);
            speedButton.clicked += () => { HandleSetSpeed(speedButton, setting.speed); };
        }

        ObstacleCompendiumView compendiumView = Q<ObstacleCompendiumView>();
        compendiumView.OnCategorySelected += HandleCategorySelected;
        compendiumView.OnObstacleSelected += HandleObstacleSelected;

    }

    private void Start()
    {
        HandleMode(WorldManager.Instance.GameMode);
    }

    public T Q<T>(string name = null, string className = null) where T : VisualElement
    {
        return Root.Q<T>(name, className);
    }
    public VisualElement Q(string name = null, string className = null)
    {
        return Root.Q(name, className);
    }
    public IEnumerable<T> Query<T>(string name = null, string className = null) where T : VisualElement
    {
        return Root.Query<T>(name, className).ToList();
    }

    private VisualElement BuildScoreVisualizer(ColorScore score)
    {
        Label label = new($"{score.score} / {score.maxScore}");
        label.style.color = score.color.color;
        label.pickingMode = PickingMode.Ignore;
        return label;
    }

    public void OpenWindow(string containerId)
    {
        foreach (var window in windows)
        {
            window.SetVisible(window.containerId == containerId);
        }
    }

    public void SetScores(IEnumerable<ColorScore> scores)
    {
        VisualElement holder = Root.Q("ScoreHolder");
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
        if (!WorldManager.Instance.SetGameMode(gameMode)) return;

        if (gameMode == GameMode.Play)
        {
            EnsureSpeedSelected(WorldManager.Instance.World.TimeSpeed);
        }

        Root.EnableInClassList("play-mode", gameMode == GameMode.Play);
        Root.EnableInClassList("setup-mode", gameMode == GameMode.Setup);
        Root.EnableInClassList("edit-mode", gameMode == GameMode.LevelEdit);
    }

    private void HandleObstacleSelected(ObstacleData data)
    {
        Obstacle newObstacle = WorldManager.Instance.SpawnObstacle(data);
        WorldManager.Instance.World.Raycast(WorldManager.Instance.GetCameraRay(), out Vector3 newPosition);
        newObstacle.transform.position = newPosition;
        WorldManager.Instance.AttachObstacleToCursor(newObstacle);
    }

    private void HandleCategorySelected(ObstacleCategory category)
    {
        // do nothing, as of now
    }

    public void ToggleToolMenu()
    {
        toolBar.ToggleInClassList("collapsed");
    }

    public void SetVerifiedStatus(bool verified)
    {
        Root.EnableInClassList("verified", verified);
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
