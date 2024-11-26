using UnityEngine;

[CreateAssetMenu(fileName = "SpeedSetting", menuName = "Scriptable Objects/SpeedSetting")]
public class SpeedSetting : ScriptableObject
{
    [Range(0f, 5f)]
    public float speed = 1;
    public Sprite icon;
}
