#nullable enable
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

[CreateAssetMenu(fileName = "Asset ID Provider", menuName = "Scriptable Objects/Asset Id Provider")]
public class AssetIdProvider : ScriptableObject
{
    [SerializeField] private List<ObstacleData> obstacleDataIndex;
    [SerializeField] private List<CritterColor> critterColorIndex;

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

    public ObstacleData GetObstacle(int id)
    {
        return obstacleDataIndex[id];
    }
    public CritterColor GetColor(int id)
    {
        return critterColorIndex[id];
    }
}