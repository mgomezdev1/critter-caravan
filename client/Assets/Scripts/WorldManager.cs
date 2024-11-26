using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

#nullable enable
#pragma warning disable CS8618
public enum GameMode
{
    LevelEdit,
    Setup,
    Play
}

public class WorldManager : MonoBehaviour
{
    private static WorldManager instance;
    public static WorldManager Instance
    {
        get { 
            if (instance == null)
            {
                instance = FindAnyObjectByType<WorldManager>();
            }
            return instance;
        }
    }

    [SerializeField] private CritterColor[] colors;
    [SerializeField] private UIManager ui;
    [SerializeField] private GridWorld activeWorld;
    public UIManager UI => ui;
    public GridWorld World => activeWorld;

    [SerializeField] private GameMode gameMode;
    public GameMode GameMode => gameMode;

    void Awake()
    {
        instance = this;
    }

    // Update is called once per frame
    void Update()
    {
        if (gameMode == GameMode.Setup || gameMode == GameMode.LevelEdit) SimulateMouseEvents();
        
    }

    private Obstacle? heldObstacle = null;
    private bool clickedLast = false;
    void SimulateMouseEvents()
    {
        // Ideally, these events should only be called after checking that an 
        // incoming click did not successfully interact with the UI
        bool clickingNow = Input.GetAxis("Fire1") > 0.1;
        bool clickChanged = clickedLast != clickingNow;
        if (clickingNow)
        {
            if (clickChanged)
            {
                // This is invoked through the UI subsystem
                //HandleWorldMouseDown();
            }

            HandleWorldMouseHeld();
        }
        else if (clickChanged) 
        {
            HandleWorldMouseUp();
        }

        clickedLast = clickingNow;
    }

    public void HandleWorldMouseDown()
    {
        HandleWorldMouseDown(GetCameraRay());
    }
    public void HandleWorldMouseDown(Ray cameraRay)
    {
        // For devices which support multiple pointers, ensure we don't try to pick up multiple obstacles at once
        if (heldObstacle == null)
        {
            Obstacle? candidate = GetObstacleAtRay(cameraRay);
            //Debug.Log($"Attempting to start drag of obstacle {candidate}");
            if (candidate != null && candidate.CanBeDragged())
            {
                //Debug.Log($"Candidate can be dragged!");
                heldObstacle = candidate;
                heldObstacle.BeginDragging();
            }
            else
            {
                heldObstacle = null;
            }
        }
    }
    public void HandleWorldMouseHeld()
    {
        HandleWorldMouseHeld(GetCameraRay());
    }
    public void HandleWorldMouseHeld(Ray cameraRay)
    {
        if (heldObstacle != null)
        {
            //Debug.Log($"Processing drag of obstacle {heldObstacle}");
            heldObstacle.DragToRay(cameraRay);
        }
    }
    public void HandleWorldMouseUp()
    {
        HandleWorldMouseUp(GetCameraRay());
    }
    public void HandleWorldMouseUp(Ray cameraRay)
    {
        if (heldObstacle != null)
        {
            if (!heldObstacle.EndDragging(out IObstaclePlacementResult placementResult))
            {
                DisplayMessage(placementResult.Reason);
                HighlightIncompatibilities(placementResult);
            }
            heldObstacle = null;
        }
    }

    private const float INCOMPATIBILITY_DISPLAY_DURATION = 3.0f;
    private void HighlightIncompatibilities(IObstaclePlacementResult placementResult, bool includeMovedObstacle = false)
    {
        foreach (var obstacle in placementResult.GetProblemObstacles())
        {
            if (!includeMovedObstacle && obstacle == placementResult.Obstacle) continue;
            obstacle.ShowIncompatibility(INCOMPATIBILITY_DISPLAY_DURATION);
        }
    }

    public void DisplayMessage(string message)
    {
        Debug.Log(message);
        StartCoroutine(DisplayMessageCoroutine());
    }
    public IEnumerator DisplayMessageCoroutine()
    {
        yield break;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ray GetCameraRay()
    {
        return activeWorld.GridCamera.ScreenPointToRay(Input.mousePosition);
    }

    Obstacle? GetObstacleAtRay(Ray ray)
    {
        if (!activeWorld.Raycast(ray, out Vector3 point))
            return null;

        Vector2Int cell = activeWorld.GetCell(point);
        return activeWorld.GetObstacle(cell);
    }

    public bool SetGameMode(GameMode gameMode)
    {
        var obstacles = activeWorld.GetAllObstacles();
        foreach (var obstacle in obstacles)
        {
            if (obstacle.IsDragging) return false;
        }

        ResetScenario();
        this.gameMode = gameMode;
        return true;
    }

    /// <summary>
    /// <para>Obtains a list of all valid critter colors that have been registered with the world manager.</para>
    /// <para>Do not modify the returned array. If you must make modifications, create a copy first.</para>
    /// </summary>
    /// <returns>The array of registered critter colors</returns>
    public CritterColor[] GetColors()
    {
        return colors;
    }
    public CritterColor? GetColorByName(string name)
    {
        string nameLower = name.ToLower();
        foreach (var c in colors)
        {
            if (c.name.ToLower() == nameLower) return c;
        }
        return null;
    }

    public void ResetScenario()
    {
        ResetScores();
        activeWorld.HandleReset();
    }

    /* ********************** *
     *     SCORE MANAGER      *
     * ********************** */
    private List<ColorScore> scores = new();
    public void ResetScores()
    {
        scores.Clear();
    }
    public ColorScore AddScore(ColorScore score)
    {
        // Debug.Log($"Registering score {score}");
        for (int i = 0; i < scores.Count; i++)
        {
            if (scores[i].color == score.color)
            {
                scores[i] += score;
                UI.SetScores(scores);
                return scores[i];
            }
        }

        scores.Add(score);
        UI.SetScores(scores);

        return score;
    }
    public List<ColorScore> GetScores()
    {
        return scores;
    }
}
