using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ObstacleCompendium", menuName = "Scriptable Objects/ObstacleCompendium")]
public class ObstacleCompendium : ScriptableObject
{
    public List<ObstacleCategory> categories;

    public IEnumerable<ObstacleData> GetAllObstacles()
    {
        HashSet<ObstacleData> found = new();

        foreach (var category in categories)
        {
            foreach (var obstacle in category.obstacles)
            {
                if (found.Contains(obstacle)) continue;
                found.Add(obstacle);
                yield return obstacle;
            }
        }
    }
}
