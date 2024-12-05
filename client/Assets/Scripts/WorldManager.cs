using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using static UnityEngine.InputSystem.InputAction;

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

    private void Start()
    {
        StartCoroutine(CheckWorldStateCoroutine());
    }

    public const float WORLD_CHECK_COOLDOWN = 0.25f;
    private IEnumerator CheckWorldStateCoroutine()
    {
        while (true)
        {
            if (GameMode == GameMode.LevelEdit)
            {
                var result = World.CheckAllValidObstacles();
                if (!result.Success)
                {
                    HandlePlacementError(result, true, false);
                }
                else
                {
                    Debug.Log("No issues detected!");
                }
                // ui.SetTryOutButtonEnabled(result.Success);
            }
            yield return new WaitForSeconds(WORLD_CHECK_COOLDOWN);
        }
    }

    private Obstacle? heldObstacle = null;
    public Obstacle? HeldObstacle => heldObstacle;

    public void DropHeldObstacle()
    {
        if (heldObstacle != null)
        {
            if (!heldObstacle.EndDragging(out IObstaclePlacementResult placementResult))
            {
                HandlePlacementError(placementResult);
            }
            heldObstacle = null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Obstacle? GetObstacleAtPointer()
    {
        return GetObstacleAtRay(GetCameraRay());
    }

    public Obstacle? TryGrabObstacleAtPointer()
    {
        Obstacle? candidate = GetObstacleAtPointer();
        var success = TryGrabOstacle(candidate);
        return success ? candidate : null;
    }
    public bool TryGrabOstacle(Obstacle? newDragTarget)
    {
        ChangeSelection(newDragTarget);

        //Debug.Log($"Attempting to start drag of obstacle {candidate}");
        if (newDragTarget != null && newDragTarget.CanBeMoved())
        {
            //Debug.Log($"Candidate can be dragged!");
            heldObstacle = newDragTarget;
            heldObstacle.BeginDragging();
            return true;
        }
        else
        {
            heldObstacle = null;
            return newDragTarget == null;
        }
    }
    private Obstacle? selectedObstacle;
    public Obstacle? SelectedObstacle => selectedObstacle;
    private GameObject? activeSelectHighlight;
    public void ChangeSelection(Obstacle? newSelectTarget)
    {
        if (newSelectTarget == selectedObstacle) return;

        if (activeSelectHighlight != null)
        {
            World.EndHighlight(activeSelectHighlight);
            activeSelectHighlight = null;
        }
        selectedObstacle = newSelectTarget;
        if (selectedObstacle != null)
        {
            activeSelectHighlight = World.HighlightTransform(selectedObstacle.MoveTarget);
        }
    }

    public void RotateSelection(int delta = 1)
    {
        if (selectedObstacle == null) return;
        if (!selectedObstacle.CanBeMoved()) return;
        selectedObstacle.TryRotate(delta);
    }

    public void DeleteObstacle(Obstacle target)
    {
        if (target == selectedObstacle)
        {
            ChangeSelection(null);
        }
        if (target == HeldObstacle)
        {
            DropHeldObstacle();
        }
        target.Delete();
    }


    private const float INCOMPATIBILITY_DISPLAY_DURATION = 3.0f;
    public void HandlePlacementError(IObstaclePlacementResult placementResult, bool includeMovedObstacle = false, bool announceErrorReason = true)
    {
        if (announceErrorReason) DisplayMessage(placementResult.Reason);
        foreach (var obstacle in placementResult.GetProblemObstacles())
        {
            if (!includeMovedObstacle && obstacle == placementResult.Obstacle) continue;
            obstacle.ShowIncompatibility(INCOMPATIBILITY_DISPLAY_DURATION);
        }
    }

    public void DisplayMessage(string message, float duration = INCOMPATIBILITY_DISPLAY_DURATION)
    {
        Debug.Log(message);
        ui.DisplayMessage(message, duration);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ray GetCameraRay()
    {
        return activeWorld.GridCamera.ScreenPointToRay(Input.mousePosition);
    }

    public Obstacle? GetObstacleAtRay(Ray ray)
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

        // If leaving Edit Mode, perform some world checks
        if (this.gameMode == GameMode.LevelEdit && GameMode != GameMode.LevelEdit)
        {
            var worldValidResult = World.CheckValidObstacles(null);
            if (!worldValidResult.Success)
            {
                HandlePlacementError(worldValidResult, false, true);
                return false;
            }
            InitScenario();
        }
        else
        {
            ResetScenario();
        }

        this.gameMode = gameMode;
        if (gameMode == GameMode.Setup)
        {
            foreach (var obstacle in activeWorld.GetDynamicObstacles())
            {
                activeWorld.HighlightCell(obstacle.Cell, 1);
            }
        } 
        else if (gameMode == GameMode.Play) 
        {
            foreach (var obstacle in activeWorld.GetDynamicObstacles().Where(o => o.IsLive))
            {
                activeWorld.HighlightCell(obstacle.Cell, 1);
            }
        }

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

    public void InitScenario()
    {
        activeWorld.HandleInit();
    }

    public Obstacle SpawnObstacle(ObstacleData template)
    {
        GameObject spawned = Instantiate(template.obstaclePrefab, activeWorld.transform);
        Obstacle result = spawned.GetComponent<Obstacle>();
        result.Initialize(activeWorld);

        return result;
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

    public Obstacle SpawnAndGrabObstacle(ObstacleData data, Vector3 position, Quaternion rotation)
    {
        DropHeldObstacle();

        Obstacle newObstacle = SpawnObstacle(data);
        newObstacle.MarkVolatile();
        newObstacle.transform.SetPositionAndRotation(position, rotation);
        TryGrabOstacle(newObstacle);
        return newObstacle;
    }

    public IObstaclePlacementResult TrySpawnAndPlaceObstacle(ObstacleData data, Vector3 position, Quaternion rotation)
    {
        Vector2Int cell = World.GetCell(position);
        Obstacle prefabScript = data.obstaclePrefab.GetComponent<Obstacle>();
        var prefabResult = prefabScript.CanBePlacedNoInit(World, cell, rotation);
        if (!prefabResult.Success)
        {
            return prefabResult.Blame(null);
        }

        Obstacle newObstacle = SpawnObstacle(data);
        newObstacle.transform.SetPositionAndRotation(position, rotation);
        newObstacle.MarkPositionDirty();

        // We'll allow illegal states in the world in Edit Mode
        // World.ScheduleSideEffectCheck(newObstacle);

        return prefabResult.Blame(newObstacle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool RaycastCameraRay(out Vector3 hit)
    {
        return activeWorld.Raycast(GetCameraRay(), out hit);
    }
}
