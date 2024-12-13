using UnityEngine;

[CreateAssetMenu(fileName = "ObstacleData", menuName = "Scriptable Objects/ObstacleData")]
public class ObstacleData : ScriptableObject
{
    public GameObject obstaclePrefab;
    public string obstacleName;
    public Sprite obstacleSprite;
}
