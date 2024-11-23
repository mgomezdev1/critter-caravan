using UnityEngine;

[RequireComponent(typeof(CellElement))]
public class CellBehaviour<T> : MonoBehaviour where T : CellElement
{
    public virtual Vector2Int Cell => CellComponent.Cell;
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
}
