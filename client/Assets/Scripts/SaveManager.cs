using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    [SerializeField] private WorldSerializationOptions serializationOptions = new(2, CompressionType.Base64Gzip);
    public WorldSerializationOptions SerializationOptions { get { return serializationOptions; } }

    private static SaveManager instance;
    public static SaveManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = GameObject.FindAnyObjectByType<SaveManager>();
            }
            return instance;
        }
        set { instance = value; }
    }

    [SerializeField] private List<ObstacleData> obstacleDataIndex;
    [SerializeField] private List<CritterColor> critterColorIndex;

    public string GetWorldDataString(GridWorld world)
    {
        List<ObstacleSaveData> obstacles = new();
        foreach (var obstacle in world.GetAllObstacles())
        {
            if (obstacle.Generated) continue;
            obstacles.Add(GetObstacleData(obstacle));
        }
        WorldSaveData saveData = new(world.GridSize, obstacles);
        string serialized = saveData.Serialize(serializationOptions);
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

    public int GetId(ObstacleData obstacleData)
    {
        return obstacleDataIndex.IndexOf(obstacleData);
    }
    public int GetObstacleId(Obstacle obstacle)
    {
        return GetObstacleIdByName(obstacle.ObstacleName);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetObstacleIdByName(string obstacleName)
    {
        return obstacleDataIndex.FindIndex(data => data.obstacleName == obstacleName);
    }
    public int GetId(CritterColor critterColor)
    {
        return critterColorIndex.IndexOf(critterColor);
    }

    public ObstacleSaveData GetObstacleData(Obstacle target)
    {
        if (target.Generated)
        {
            throw new InvalidOperationException("Attempted to get save data for a generated obstacle.");
        }

        return new ObstacleSaveData(
            GetObstacleId(target),
            target.MoveMode,
            target.World.GetIdFromCell(target.Cell),
            MathLib.CardinalDirectionFromObstacleRotation(target.transform.rotation),
            GetId(target.Color)
        );
    }
    public Obstacle LoadObstacleData(GridWorld world, ObstacleSaveData saveData)
    {
        // we need to create a new obstacle
        ObstacleData data = obstacleDataIndex[saveData.obstacleId];
        Obstacle target = WorldManager.Instance.SpawnObstacle(data,
            world.GetCellFromId(saveData.cellId),
            MathLib.ObstacleRotationFromCardinalDirection(saveData.facingDirection)
        );
        target.MoveMode = saveData.moveType;
        target.Color = critterColorIndex[saveData.colorId];
        target.Initialize(world); // this also registers the target to the world
        return target;
    }
}