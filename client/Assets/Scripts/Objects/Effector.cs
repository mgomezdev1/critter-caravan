using System.Collections.Generic;
using UnityEngine;

public class EffectResult
{
    public EffectResult() { }
}

public interface IEffector
{
    public Vector2Int Cell { get; set; }
    public EffectResult OnEntityEnter(CellEntity entity);
    public EffectResult OnEntityExit(CellEntity entity);
}

[RequireComponent(typeof(CellElement))]
public class Effector : MonoBehaviour
{
    [SerializeField] private bool stopFall;
    [SerializeField] private List<Transform> effectorMoves;
    [SerializeField] private bool allowMovingThroughWalls = false;

    private CellElement cellElement;

    private void Start()
    {
        cellElement = GetComponent<CellElement>();
    }

    
}