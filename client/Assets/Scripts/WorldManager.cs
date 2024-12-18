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

public class WorldManager : SingletonBehaviour<WorldManager>
{

    [SerializeField] private CritterColor[] colors;
    [SerializeField] private UIManager ui;
    [SerializeField] private GridWorld activeWorld;
    public static UIManager UI => Instance.ui;
    public static GridWorld World => Instance.activeWorld;

    [SerializeField] private GameMode gameMode;
    public static GameMode GameMode => Instance.gameMode;

    protected override void Awake()
    {
        base.Awake();
    }

    private void Start()
    {
        StartCoroutine(CheckWorldStateCoroutine());
    }

    public const float WORLD_CHECK_COOLDOWN = 0.25f;
    private static IEnumerator CheckWorldStateCoroutine()
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
    public static Obstacle? HeldObstacle => Instance.heldObstacle;

    public static void DropHeldObstacle()
    {
        if (HeldObstacle != null)
        {
            if (!HeldObstacle.EndDragging(out IObstaclePlacementResult placementResult))
            {
                HandlePlacementError(placementResult);
            }
            Instance.heldObstacle = null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Obstacle? GetObstacleAtPointer()
    {
        return GetObstacleAtRay(GetCameraRay());
    }

    public static Obstacle? TryGrabObstacleAtPointer()
    {
        Obstacle? candidate = GetObstacleAtPointer();
        var success = TryGrabOstacle(candidate);
        return success ? candidate : null;
    }
    public static bool TryGrabOstacle(Obstacle? newDragTarget)
    {
        ChangeSelection(newDragTarget);

        //Debug.Log($"Attempting to start drag of obstacle {candidate}");
        if (newDragTarget != null && newDragTarget.CanBeMoved())
        {
            //Debug.Log($"Candidate can be dragged!");
            Instance.heldObstacle = newDragTarget;
            newDragTarget.BeginDragging();
            return true;
        }
        else
        {
            Instance.heldObstacle = null;
            return newDragTarget == null;
        }
    }
    private Obstacle? selectedObstacle;
    public static Obstacle? SelectedObstacle => Instance.selectedObstacle;
    private GameObject? activeSelectHighlight;
    public static void ChangeSelection(Obstacle? newSelectTarget)
    {
        if (newSelectTarget == SelectedObstacle) return;

        if (Instance.activeSelectHighlight != null)
        {
            World.EndHighlight(Instance.activeSelectHighlight);
            Instance.activeSelectHighlight = null;
        }
        Instance.selectedObstacle = newSelectTarget;
        if (SelectedObstacle != null)
        {
            Instance.activeSelectHighlight = World.HighlightTransform(SelectedObstacle.MoveTarget);
        }
    }

    public static void RotateSelection(int delta = 1)
    {
        if (SelectedObstacle == null) return;
        if (!SelectedObstacle.CanBeMoved()) return;
        SelectedObstacle.TryRotate(delta);
    }

    public static void DeleteObstacle(Obstacle target)
    {
        if (target == SelectedObstacle)
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
    public static void HandlePlacementError(IObstaclePlacementResult placementResult, bool includeMovedObstacle = false, bool announceErrorReason = true)
    {
        if (announceErrorReason) DisplayMessage(placementResult.Reason);
        foreach (var obstacle in placementResult.GetProblemObstacles())
        {
            if (!includeMovedObstacle && obstacle == placementResult.Obstacle) continue;
            obstacle.ShowIncompatibility(INCOMPATIBILITY_DISPLAY_DURATION);
        }
    }

    public static void DisplayMessage(string message, float duration = INCOMPATIBILITY_DISPLAY_DURATION)
    {
        Debug.Log(message);
        UI.DisplayMessage(message, duration);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Ray GetCameraRay()
    {
        return World.GridCamera.ScreenPointToRay(Input.mousePosition);
    }

    public static Obstacle? GetObstacleAtRay(Ray ray)
    {
        if (!World.Raycast(ray, out Vector3 point))
            return null;

        Vector2Int cell = World.GetCell(point);
        return World.GetObstacle(cell);
    }

    public static bool SetGameMode(GameMode gameMode)
    {
        var obstacles = World.GetAllObstacles();
        foreach (var obstacle in obstacles)
        {
            if (obstacle.IsDragging) return false;
        }

        // If leaving Edit Mode, perform some world checks
        if (GameMode == GameMode.LevelEdit && GameMode != GameMode.LevelEdit)
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

        Instance.gameMode = gameMode;
        if (gameMode == GameMode.Setup)
        {
            foreach (var obstacle in World.GetDynamicObstacles())
            {
                World.HighlightCell(obstacle.Cell, 1);
            }
        } 
        else if (gameMode == GameMode.Play) 
        {
            foreach (var obstacle in World.GetDynamicObstacles().Where(o => o.IsLive))
            {
                World.HighlightCell(obstacle.Cell, 1);
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

    public static void ResetScenario()
    {
        ResetScores();
        World.HandleReset();
    }

    public static void InitScenario()
    {
        World.HandleInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Obstacle SpawnObstacle(ObstacleData template, Vector2Int cell, Quaternion rotation, bool markVolatile = false)
    {
        var position = World.GetCellCenter(cell);
        return SpawnObstacle(template, position, rotation, markVolatile);
    }
    public static Obstacle SpawnObstacle(ObstacleData template, Vector3 position, Quaternion rotation, bool markVolatile = false)
    {
        GameObject spawned = Instantiate(template.obstaclePrefab, position, rotation, World.transform);
        Obstacle result = spawned.GetComponent<Obstacle>();
        if (markVolatile) result.MarkVolatile();
        result.Initialize(World);

        return result;
    }

    /* ********************** *
     *     SCORE MANAGER      *
     * ********************** */
    private readonly List<ColorScore> scores = new();
    public static void ResetScores()
    {
        Instance.scores.Clear();
    }
    public static ColorScore AddScore(ColorScore score)
    {
        // Debug.Log($"Registering score {score}");
        var scores = Instance.scores;
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
    public static List<ColorScore> GetScores()
    {
        return Instance.scores;
    }

    public static Obstacle SpawnAndGrabObstacle(ObstacleData data, Vector3 position, Quaternion rotation)
    {
        DropHeldObstacle();

        Obstacle newObstacle = SpawnObstacle(data, position, rotation, true);
        TryGrabOstacle(newObstacle);
        return newObstacle;
    }

    public static IObstaclePlacementResult TrySpawnAndPlaceObstacle(ObstacleData data, Vector3 position, Quaternion rotation)
    {
        Vector2Int cell = World.GetCell(position);
        Obstacle prefabScript = data.obstaclePrefab.GetComponent<Obstacle>();
        var prefabResult = prefabScript.CanBePlacedNoInit(World, cell, rotation);
        if (!prefabResult.Success)
        {
            return prefabResult.Blame(null);
        }

        Obstacle newObstacle = SpawnObstacle(data, position, rotation);

        // We'll allow illegal states in the world in Edit Mode
        // World.ScheduleSideEffectCheck(newObstacle);

        return prefabResult.Blame(newObstacle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool RaycastCameraRay(out Vector3 hit)
    {
        return World.Raycast(GetCameraRay(), out hit);
    }

    [Header("World Serialization")]
    [SerializeField] private WorldSerializationOptions serializationOptions = new(2, CompressionType.Base64Gzip);
    public static WorldSerializationOptions SerializationOptions => Instance.serializationOptions;

    [SerializeField] private AssetIdProvider idProvider;

    public static string GetWorldDataString(GridWorld world)
    {
        List<ObstacleSaveData> obstacles = new();
        foreach (var obstacle in world.GetAllObstacles())
        {
            if (obstacle.Generated) continue;
            obstacles.Add(GetObstacleData(obstacle));
        }
        WorldSaveData saveData = new(world.GridSize, obstacles);
        string serialized = saveData.Serialize(SerializationOptions);
        Debug.Log($"Serialized world data string: {saveData} -> {serialized}");

        // test w/ deserialization
        var deserialized = WorldSaveData.Deserialize(serialized, out _);
        Debug.Log($"After deserialization -> {deserialized}");

        return serialized;
    }

    public void LoadWorldData(GridWorld world, string data)
    {
        world.Clear();
        WorldSaveData worldData = WorldSaveData.Deserialize(data, out _);
        world.SetSize(worldData.worldSize);
        foreach (var obstacle in worldData.obstacles)
        {
            LoadObstacleData(world, obstacle);
        }
    }

    public static ObstacleSaveData GetObstacleData(Obstacle target)
    {
        if (target.Generated)
        {
            throw new InvalidOperationException("Attempted to get save data for a generated obstacle.");
        }

        return new ObstacleSaveData(
            Instance.idProvider.GetObstacleId(target),
            target.MoveMode,
            target.World.GetIdFromCell(target.Cell),
            MathLib.CardinalDirectionFromObstacleRotation(target.transform.rotation),
            Instance.idProvider.GetId(target.Color)
        );
    }
    public static Obstacle LoadObstacleData(GridWorld world, ObstacleSaveData saveData)
    {
        // we need to create a new obstacle
        ObstacleData data = Instance.idProvider.GetObstacle(saveData.obstacleId);
        Obstacle target = SpawnObstacle(data,
            world.GetCellFromId(saveData.cellId),
            MathLib.ObstacleRotationFromCardinalDirection(saveData.facingDirection)
        );
        target.MoveMode = saveData.moveType;
        target.Color = Instance.idProvider.GetColor(saveData.colorId);
        target.Initialize(world); // this also registers the target to the world
        return target;
    }
}
