using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#nullable enable
public interface IObstaclePlacementResult
{
    public Obstacle Obstacle { get; }
    public bool Success { get; }
    public string Reason { get; }
    
}
public class ObstaclePlacementSuccess : IObstaclePlacementResult
{
    private readonly Obstacle _obstacle;
    public Obstacle Obstacle => _obstacle;
    public bool Success => true;
    public string Reason => string.Empty;

    public ObstaclePlacementSuccess(Obstacle obstacle)
    {
        _obstacle = obstacle;
    }
}
public class ObstaclePlacementFailure : IObstaclePlacementResult
{
    private readonly Obstacle _obstacle;
    public Obstacle Obstacle => _obstacle;
    public bool Success => false;
    public string Reason { get; protected set; }

    public ObstaclePlacementFailure(Obstacle obstacle, string reason)
    {
        _obstacle = obstacle;
        Reason = reason;
    }

    public override string ToString()
    {
        return Reason;
    }
}
public class ObstacleConflict : ObstaclePlacementFailure
{
    public readonly Obstacle conflictObstacle;
    public readonly Vector2Int conflictCell;

    public ObstacleConflict(Obstacle obstacle, Obstacle conflictObstacle, Vector2Int conflictCell)
        : base(obstacle, string.Empty)
    {
        this.conflictObstacle = conflictObstacle;
        this.conflictCell = conflictCell;
        Reason = $"{obstacle.ObstacleName} infringes on the area of {conflictObstacle.ObstacleName} on cell {conflictCell}";
    }
}
public class ObstacleMissingSurface : ObstaclePlacementFailure
{
    public readonly Vector2Int cell;
    public readonly Vector2 normal;

    public ObstacleMissingSurface(Obstacle obstacle, Vector2Int cell, Vector2 normal)
        : base(obstacle, string.Empty)
    {
        this.cell = cell;
        this.normal = normal;
        Reason = $"{obstacle.ObstacleName} requires a surface below cell {cell} to stand on.";
    }
}

public class Obstacle : CellBehaviour<CellElement>
{
    [Flags]
    public enum Requirements
    {
        None = 0,
        OnSurface = 1
    }

    [SerializeField] List<Vector2Int> occupiedOffsets;
    [SerializeField] List<MeshRenderer> incompatibilityDisplays = new();
    [SerializeField] bool isFixed = false;
    [SerializeField] private string obstacleName = "Obstacle";
    [SerializeField] private Requirements requirements = Requirements.None;
    public string ObstacleName => obstacleName;
    public bool IsFixed => isFixed;
    public Requirements Reqs => requirements;

    protected override void Awake()
    {
        base.Awake();
    }

    private void Start()
    {
        World.RegisterObstacle(this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        foreach (var offset in occupiedOffsets)
        {
            Vector3 center = World.GetCellCenter(Cell + offset);
            Gizmos.DrawWireCube(center, Vector3.one * (World.GridScale * 0.9f));
        }
    }

    public IEnumerable<Vector2Int> GetOccupiedCells()
    {
        Vector2Int origin = Cell;
        foreach (var offset in occupiedOffsets)
        {
            yield return offset + origin;
        }
    }

    public IObstaclePlacementResult CanBePlaced(Vector2Int cell)
    {
        GridWorld world = World;
        foreach (var offset in occupiedOffsets)
        {
            Vector2Int rotatedOffset = MathLib.RoundVector(transform.rotation * (Vector2)offset);
            Vector2Int finalCell = cell + rotatedOffset;
            Obstacle? conflict = world.GetObstacle(finalCell);
            if (conflict != null)
            {
                // Might want to introduce localization for this
                return new ObstacleConflict(this, conflict, finalCell);
            }
        }

        if (Reqs.HasFlag(Requirements.OnSurface)) {
            Vector2 rotatedNormal = transform.rotation * Vector2.up;
            Surface? surface = world.GetSurface(cell, rotatedNormal, 30, Surface.Flags.Virtual);
            if (surface == null)
            {
                return new ObstacleMissingSurface(this, cell, rotatedNormal);
            }
        }

        return new ObstaclePlacementSuccess(this);
    }

    public void ShowIncompatibility(float duration)
    {
        foreach (var renderer in incompatibilityDisplays)
        {
            StartCoroutine(ShowIncompatibilityCoroutine(renderer, duration));
        }
    }

    public IEnumerator ShowIncompatibilityCoroutine(MeshRenderer renderer, float duration = 1)
    {
        Color initialColor = renderer.material.color;
        renderer.enabled = true;
        float remDuration = duration;
        while (remDuration > 0)
        {
            float alphaFactor = remDuration / duration;
            renderer.material.color = initialColor * new Color(1, 1, 1, alphaFactor);
            yield return new WaitForEndOfFrame();
        }
        renderer.enabled = false;
        renderer.material.color = initialColor;
        yield break;
    }
}
