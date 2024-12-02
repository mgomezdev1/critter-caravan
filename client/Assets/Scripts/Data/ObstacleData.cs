using UnityEngine;

[CreateAssetMenu(fileName = "ObstacleData", menuName = "Scriptable Objects/ObstacleData")]
public class ObstacleData : ScriptableObject
{
    public GameObject obstaclePrefab;
    public string obstacleName;
    public Sprite obstacleSprite;

    public string GetPrefabObstacleName()
    {
        Obstacle obstacle = obstaclePrefab.GetComponent<Obstacle>();
        return obstacle.ObstacleName;
    }
}
