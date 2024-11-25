using Unity.VisualScripting;
using UnityEngine;

public interface IMovable 
{
    public void AfterMove(Vector2Int originCell, Quaternion originRotation, Vector2Int targetCell, Quaternion targetRotation);
}
