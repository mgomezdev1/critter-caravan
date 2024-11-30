using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ObstacleCategory", menuName = "Scriptable Objects/ObstacleCategory")]
public class ObstacleCategory : ScriptableObject
{
    public string categoryName;
    public Sprite categoryIcon;

    public List<ObstacleData> obstacles;
}