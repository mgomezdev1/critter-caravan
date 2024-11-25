using UnityEngine;

[RequireComponent(typeof(CellElement))]
public class CellBehaviour<T> : MonoBehaviour where T : CellElement
{
    public virtual Vector2Int Cell {
        get => CellComponent.Cell;
        set => CellComponent.Cell = value;
    }
    public GridWorld World => CellComponent.World;
    private T cellElement;
    public T CellComponent { get {
            if (cellElement == null) cellElement = GetComponent<T>();
            return cellElement;
        }
    }

    protected virtual void Awake()
    {
        cellElement = GetComponent<T>();
    }

    public void InvokeMoved(Vector2Int originCell, Quaternion originRotation, Vector2Int targetCell, Quaternion targetRotation)
    {
        CellComponent.MarkPositionDirty();
        foreach (IMovable movable in CellComponent.GetComponents<IMovable>())
        {
            movable.AfterMove(originCell, originRotation, targetCell, targetRotation);
        }
    }
}
