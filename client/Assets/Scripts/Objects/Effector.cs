using System.Collections.Generic;
using UnityEngine;

public class EffectResult
{
    public EffectResult() { }
}

public interface IEffector
{
    public Vector2Int Cell { get; }
    public EffectResult OnEntityEnter(CellEntity entity);
    public EffectResult OnEntityExit(CellEntity entity);
}

[RequireComponent(typeof(CellElement))]
public class Effector : MonoBehaviour, IEffector
{
    [SerializeField] private bool stopFall;
    [SerializeField] private List<Transform> effectorMoves;
    [SerializeField] private bool allowMovingThroughWalls = false;

    private CellElement cellElement;
    private CellElement CellElement { 
        get { 
            if (cellElement == null) cellElement = GetComponent<CellElement>();
            return cellElement;
        }
    }

    public Vector2Int Cell => CellElement.Cell;

    public EffectResult OnEntityEnter(CellEntity entity)
    {
        throw new System.NotImplementedException();
    }

    public EffectResult OnEntityExit(CellEntity entity)
    {
        throw new System.NotImplementedException();
    }

    private void Start()
    {
        cellElement = GetComponent<CellElement>();
        cellElement.Grid.RegisterEffector(this);
    }

    private void OnDestroy()
    {
        CellElement.Grid.DeregisterEffector(this);
    }


}