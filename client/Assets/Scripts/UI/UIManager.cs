using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

# nullable enable
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

public struct BrushButtonData
{
    public Button button;
    public Brush brush;

    public BrushButtonData(Button button, Brush brush) : this()
    {
        this.button = button;
        this.brush = brush;
    }
}

public class UIManager : BaseUIManager
{
    public const string ACTIVE_BUTTON_CLASS = "selected";

    VisualElement tilePicker;
    readonly List<Button> speedButtons = new();
    readonly List<BrushButtonData> brushButtons = new();

#pragma warning disable CS8618
    [SerializeField] private ObstacleCompendium compendium;
    ObstacleCompendiumView compendiumView;
    public ObstacleCompendiumView CompendiumView => compendiumView;
    private Brush activeBrush;
#pragma warning restore CS8618

    public ObstacleData? BrushObstacle {
        get => compendiumView.HighlightedObstacles.FirstOrDefault();
        set
        {
            compendiumView.HighlightedObstacles.Clear();
            if (value != null) compendiumView.HighlightedObstacles.Add(value);
            compendiumView.UpdateHighlights();
        }
    }

    private Quaternion obstacleSpawnRotation = Quaternion.identity;
    public Quaternion ObstacleSpawnRotation {
        get => obstacleSpawnRotation;
        set => obstacleSpawnRotation = value;
    }

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

        tilePicker = Q("TilePicker");
        Button tilePickerToggleButton = tilePicker.Q<Button>("TilePickerShowButton");
        tilePickerToggleButton.clicked += ToggleTilePicker;

        VisualElement fallthrough = Q(className: "fallthrough");

        fallthrough.RegisterCallback<MouseDownEvent>(HandleWorldMouseDown, TrickleDown.TrickleDown);
        Root.RegisterCallback<MouseMoveEvent>(HandleMouseMove, TrickleDown.TrickleDown);
        Root.RegisterCallback<MouseUpEvent>(HandleMouseUp, TrickleDown.TrickleDown);
        
        foreach (var speedButton in Q("SpeedSettings").Query<Button>(className: "speed-button").ToList())
        {
            if (speedButton.dataSource is not SpeedSetting setting) continue;
            speedButtons.Add(speedButton);
            speedButton.clicked += () => { HandleSetSpeed(speedButton, setting.speed); };
        }

        compendiumView = Q<ObstacleCompendiumView>();
        compendiumView.OnCategorySelected += HandleCategorySelected;
        compendiumView.OnObstacleSelected += HandleObstacleSelected;
        
        var brushSelectionPanel = Q("BrushSelect");
        var defaultBrush = new DragBrush(this);
        brushButtons.Add(new BrushButtonData(brushSelectionPanel.Q<Button>("Drag")!, defaultBrush));
        brushButtons.Add(new BrushButtonData(brushSelectionPanel.Q<Button>("Place")!, new PlaceBrush(this)));
        brushButtons.Add(new BrushButtonData(brushSelectionPanel.Q<Button>("Delete")!, new DeleteBrush(this)));
        foreach (var brushButton in brushButtons)
        {
            brushButton.button.clicked += () => SelectBrush(brushButton.brush);
        }
        SelectBrush(defaultBrush);

        var rotatorPanel = Q("RotationTool");
        rotatorPanel.Q<Button>("RotateCW").clicked += HandleRotateCW;
        rotatorPanel.Q<Button>("RotateCCW").clicked += HandleRotateCCW;
    }

    private void Start()
    {
        HandleMode(WorldManager.Instance.GameMode);
    }

    // Update is called once per frame
    private void Update()
    {
        
    }


    public T Q<T>(string? name = null, string? className = null) where T : VisualElement
    {
        return Root.Q<T>(name, className);
    }
    public VisualElement Q(string? name = null, string? className = null)
    {
        return Root.Q(name, className);
    }
    public IEnumerable<T> Query<T>(string? name = null, string? className = null) where T : VisualElement
    {
        return Root.Query<T>(name, className).ToList();
    }

    public void SelectBrush(Brush brush)
    {
        if (activeBrush != null) activeBrush.Deactivate();
        activeBrush = brush;
        activeBrush.Activate();
        foreach (var brushButton in brushButtons)
        {
            brushButton.button.EnableInClassList(ACTIVE_BUTTON_CLASS, brushButton.brush == brush);
        }
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
        activeBrush.HandleSelect(data);
    }

    private void HandleCategorySelected(ObstacleCategory category)
    {
        // do nothing, as of now
    }

    public void ToggleTilePicker()
    {
        tilePicker.ToggleInClassList("collapsed");
    }

    public void SetVerifiedStatus(bool verified)
    {
        Root.EnableInClassList("verified", verified);
    }

    private bool dragStartedInWorldSpace = false;
    public void HandleWorldMouseDown(MouseDownEvent evt)
    {
        if (evt.button != 0)
        {
            return;
        }
        dragStartedInWorldSpace = true;
        activeBrush.HandleClick();
    }
    public void HandleMouseMove(MouseMoveEvent evt)
    {
        if (dragStartedInWorldSpace && (evt.pressedButtons & 1) > 0)
        {
            // if left click is pressed, handle dragging
            activeBrush.HandleDrag();
        }
        else if (dragStartedInWorldSpace)
        {
            // if left click is not pressed, but we are still in a dragging state, exit dragging state
            // this can happen if the mouse is released outside the bounds of the window
            dragStartedInWorldSpace = false;
            activeBrush.HandleDragEnd();
        }
    }
    public void HandleMouseUp(MouseUpEvent evt)
    {
        if (evt.button != 0)
        {
            return;
        }

        if (dragStartedInWorldSpace)
        {
            dragStartedInWorldSpace = false;
            activeBrush.HandleDragEnd();
        }
    }

    public void PretendDragStartedInWorldSpace()
    {
        dragStartedInWorldSpace = true;
    }

    private void HandleSetSpeed(Button clickedButton, float speed)
    {
        foreach (var button in speedButtons)
        {
            button.EnableInClassList(ACTIVE_BUTTON_CLASS, button == clickedButton);
        }
        WorldManager.Instance.World.TimeSpeed = speed;
    }
    private void EnsureSpeedSelected(float speed)
    {
        Button bestButton = speedButtons.First();
        var bestDiff = Mathf.Abs((bestButton.dataSource as SpeedSetting)!.speed - speed);
        foreach (var button in speedButtons)
        {
            button.EnableInClassList(ACTIVE_BUTTON_CLASS, false);
            float btnSpeed = (button.dataSource as SpeedSetting)!.speed;
            float diff = Mathf.Abs(speed - btnSpeed);
            if (diff < bestDiff)
            {
                bestButton = button;
                bestDiff = diff;
            }
        }
        bestButton.EnableInClassList(ACTIVE_BUTTON_CLASS, true);
    }
    private void HandleRotateCW()
    {
        HandleRotate(1);
    }
    private void HandleRotateCCW()
    {
        HandleRotate(-1);
    }
    private void HandleRotate(int delta)
    {
        if (WorldManager.Instance.HeldObstacle != null)
        {
            WorldManager.Instance.RotateSelection(delta);
        }
        else
        {
            obstacleSpawnRotation = Quaternion.LookRotation(
                obstacleSpawnRotation * Vector3.forward,
                MathLib.RotateVector2(obstacleSpawnRotation * Vector2.up, -90f * delta)
            );
            compendiumView.ObstacleDisplayRotation = MathLib.AngleFromVector2(obstacleSpawnRotation * Vector2.right); ;
        }
    }
}
