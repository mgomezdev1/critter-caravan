#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public interface IObstaclePlacementResult
{
    public Obstacle? Obstacle { get; }
    public bool Success { get; }
    public string Reason { get; }

    public IEnumerable<Obstacle> GetProblemObstacles();
    public IObstaclePlacementResult Blame(Obstacle? otherObstacle);

    public static IObstaclePlacementResult Combine(IEnumerable<IObstaclePlacementResult> results)
    {
        HashSet<ObstaclePlacementFailure> failures = new();
        foreach (var result in results)
        {
            if (!result.Success)
            {
                failures.Add((result as ObstaclePlacementFailure)!);
            }
        }
        if (failures.Count == 0) return new ObstaclePlacementSuccess(null);
        if (failures.Count == 1) return failures.First();

        return new CompoundObstaclePlacementFailure(failures);
    }
}
public class ObstaclePlacementSuccess : IObstaclePlacementResult
{
    private readonly Obstacle? _obstacle;
    public Obstacle? Obstacle => _obstacle;
    public bool Success => true;
    public string Reason => string.Empty;

    public ObstaclePlacementSuccess(Obstacle? obstacle)
    {
        _obstacle = obstacle;
    }
    public IEnumerable<Obstacle> GetProblemObstacles() { yield break; }

    public IObstaclePlacementResult Blame(Obstacle? otherObstacle)
    {
        return new ObstaclePlacementSuccess(otherObstacle);
    }
}
public class ObstaclePlacementFailure : IObstaclePlacementResult
{
    public readonly Obstacle? obstacle;
    public Obstacle? Obstacle => obstacle;
    public bool Success => false;
    public virtual string Reason { get; protected set; }

    public ObstaclePlacementFailure(Obstacle? obstacle, string reason)
    {
        this.obstacle = obstacle;
        Reason = reason;
    }
    public virtual IEnumerable<Obstacle> GetProblemObstacles()
    {
        if (obstacle != null) yield return obstacle; 
        yield break;
        
    }

    public virtual IObstaclePlacementResult Blame(Obstacle? otherObstacle)
    {
        return new ObstaclePlacementFailure(otherObstacle, Reason);
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

    public ObstacleConflict(Obstacle? obstacle, Obstacle conflictObstacle, Vector2Int conflictCell)
        : base(obstacle, string.Empty)
    {
        this.conflictObstacle = conflictObstacle;
        this.conflictCell = conflictCell;
        Reason = $"{(obstacle != null ? obstacle.ObstacleName : "Obstacle")} infringes on the area of {conflictObstacle.ObstacleName} on cell {conflictCell}";
    }

    public override IEnumerable<Obstacle> GetProblemObstacles()
    {
        if (obstacle != null) yield return obstacle;
        yield return conflictObstacle;
    }

    public override IObstaclePlacementResult Blame(Obstacle? otherObstacle)
    {
        return new ObstacleConflict(otherObstacle, conflictObstacle, conflictCell);
    }
}
public class ObstacleMissingSurface : ObstaclePlacementFailure
{
    public readonly Vector2Int cell;
    public readonly Vector2 normal;

    public ObstacleMissingSurface(Obstacle? obstacle, Vector2Int cell, Vector2 normal)
        : base(obstacle, string.Empty)
    {
        this.cell = cell;
        this.normal = normal;
        Reason = $"{(obstacle != null ? obstacle.ObstacleName : "Obstacle")} requires a surface below cell {cell} to stand on.";
    }

    public override IEnumerable<Obstacle> GetProblemObstacles()
    {
        if (obstacle != null) yield return obstacle;
        yield break;
    }

    public override IObstaclePlacementResult Blame(Obstacle? otherObstacle)
    {
        return new ObstacleMissingSurface(otherObstacle, cell, normal);
    }
}
public class ObstacleSideEffectFailure : ObstaclePlacementFailure
{
    public readonly ObstaclePlacementFailure baseFailure;
    public ObstacleSideEffectFailure(Obstacle? movedObstacle, ObstaclePlacementFailure baseFailure) :
        base(movedObstacle, string.Empty)
    {
        this.baseFailure = baseFailure;
        Reason = baseFailure.Reason;
    }

    public override IEnumerable<Obstacle> GetProblemObstacles()
    {
        if (obstacle != null) yield return obstacle;
        foreach (var otherObstacle in baseFailure.GetProblemObstacles())
        {
            if (obstacle == otherObstacle) continue;
            yield return otherObstacle;
        }
    }

    public override IObstaclePlacementResult Blame(Obstacle? otherObstacle)
    {
        return new ObstacleSideEffectFailure(otherObstacle, baseFailure);
    }
}
public class CompoundObstaclePlacementFailure : ObstaclePlacementFailure, IObstaclePlacementResult, IEnumerable<ObstaclePlacementFailure>
{
    public readonly List<ObstaclePlacementFailure> list = new();

    public override string Reason { get
        {
            StringBuilder sb = new();
            foreach (var failure in this)
            {
                sb.Append(failure.Reason);
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }

    public CompoundObstaclePlacementFailure(IEnumerable<ObstaclePlacementFailure> failures): base(null, string.Empty)
    {
        foreach (var failure in failures)
        {
            list.Add(failure);
        }
    }

    public override IEnumerable<Obstacle> GetProblemObstacles()
    {
        HashSet<Obstacle> found = new();
        foreach (var failure in list)
        {
            foreach (var obstacle in failure.GetProblemObstacles())
            {
                if (found.Contains(obstacle)) continue;
                yield return obstacle;
                found.Add(obstacle);
            }
        }
    }

    public override IObstaclePlacementResult Blame(Obstacle? otherObstacle)
    {
        return new ObstacleSideEffectFailure(otherObstacle, this);
    }

    public IEnumerator<ObstaclePlacementFailure> GetEnumerator()
    {
        foreach (var failure in list) yield return failure;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}