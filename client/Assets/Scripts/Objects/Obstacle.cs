using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.LightTransport;

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
    [SerializeField] private Transform moveTarget;
    public string ObstacleName => obstacleName;
    public bool IsFixed => isFixed;
    public Requirements Reqs => requirements;
    public Transform MoveTarget => moveTarget;

    protected override void Awake()
    {
        base.Awake();
        desiredRotation = moveTarget.rotation;
    }

    private void Start()
    {
        World.RegisterObstacle(this);
    }

    private void Update()
    {
        MoveToOffset();
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerable<Vector2Int> GetOccupiedCells()
    {
        return GetOccupiedCells(IsDragging ? draggingStartCell : Cell, transform.rotation);
    }
    public IEnumerable<Vector2Int> GetOccupiedCells(Vector2Int origin)
    {
        return GetOccupiedCells(origin, transform.rotation);
    }
    public IEnumerable<Vector2Int> GetOccupiedCells(Vector2Int origin, Quaternion rotation)
    {
        foreach (var offset in occupiedOffsets)
        {
            Vector2Int rotatedOffset = MathLib.RoundVector(rotation * (Vector2)offset);
            yield return origin + rotatedOffset;
        }
    }

    public IObstaclePlacementResult CanBePlaced(Vector2Int origin)
    {
        return CanBePlaced(origin, transform.rotation);
    }
    public IObstaclePlacementResult CanBePlaced(Vector2Int newOrigin, Quaternion newRotation)
    {
        GridWorld world = World;
        foreach (var occupiedCell in GetOccupiedCells(newOrigin, newRotation))
        {
            Obstacle? conflict = world.GetObstacle(occupiedCell);
            if (conflict != null && conflict != this)
            {
                // Might want to introduce localization for this
                return new ObstacleConflict(this, conflict, occupiedCell);
            }
        }

        // Flag-based requirement testing
        IObstaclePlacementResult extraReqTestResult = CheckValidity(newOrigin, newRotation);
        if (!extraReqTestResult.Success) return extraReqTestResult;

        // Side effect check
        Vector2Int currentCell = Cell;
        if (newOrigin != currentCell || transform.rotation != newRotation)
        {
            Vector3 savedPos = transform.position;
            Quaternion savedRot = transform.rotation;
            World.SuppressEffectorUpdates = true;
            World.SuppressSurfaceUpdates = true;
            transform.SetPositionAndRotation(World.GetCellCenter(newOrigin), newRotation);
            InvokeMoved(currentCell, savedRot, newOrigin, newRotation);
            IObstaclePlacementResult result = World.CheckValidObstacles();
            transform.SetPositionAndRotation(savedPos, savedRot);
            InvokeMoved(newOrigin, newRotation, currentCell, savedRot);
            World.SuppressEffectorUpdates = false;
            World.SuppressSurfaceUpdates = false;

            if (!result.Success) return result;
        }

        return new ObstaclePlacementSuccess(this);
    }

    public IObstaclePlacementResult CheckValidity()
    {
        return CheckValidity(Cell, transform.rotation);
    }
    public IObstaclePlacementResult CheckValidity(Vector2Int newOrigin, Quaternion newRotation)
    {
        if (Reqs.HasFlag(Requirements.OnSurface))
        {
            Vector2 rotatedNormal = newRotation * Vector2.up;
            Surface? surface = World.GetSurface(newOrigin, rotatedNormal, 30, Surface.Flags.Virtual);
            if (surface == null)
            {
                return new ObstacleMissingSurface(this, newOrigin, rotatedNormal);
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
            renderer.material.color = initialColor * new Color(1, 1, 1, alphaFactor * alphaFactor);
            yield return new WaitForEndOfFrame();
            remDuration -= Time.deltaTime;
        }
        renderer.enabled = false;
        renderer.material.color = initialColor;
        yield break;
    }

    /* *********************** *
     *     DRAGGING LOGIC      *
     * *********************** */

    private bool dragging = false;
    public bool IsDragging => dragging;
    private Vector3 desiredOffset = Vector3.zero;
    private Vector2Int draggingStartCell = Vector2Int.zero;
    private Quaternion desiredRotation = Quaternion.identity;
    private Quaternion draggingStartRotation = Quaternion.identity;
    public bool CanBeDragged()
    {
        switch (WorldManager.Instance.GameMode)
        {
            case GameMode.LevelEdit: return true;
            case GameMode.Setup: return !IsFixed;
            case GameMode.Play: return false;
            default: return false;
        }
    }

    private Vector3 dragVelocity = Vector3.zero;
    [Header("Dragging Logic")]
    [SerializeField] private float dragAcceleration = 50f;
    [SerializeField] private float maxDragSpeed = 4f;
    [SerializeField] private float dragFriction = 4f;
    [SerializeField] private float approachDistance = 0.25f;
    [SerializeField] private float correctiveAcceleration = 10f;
    [Header("Rotation Logic")]
    [SerializeField] private float rotationAngularVelocityDegrees = 480;

    private Vector3 DragOffset => Vector3.back * (World.GridDepth * 1);
    private void MoveToOffset()
    {
        moveTarget.rotation = Quaternion.RotateTowards(moveTarget.rotation, desiredRotation, rotationAngularVelocityDegrees * Time.deltaTime);

        Vector3 desiredPosition = transform.position + desiredOffset;
        Vector3 desiredMovement = desiredPosition - moveTarget.transform.position;
        float distanceToTarget = desiredMovement.magnitude;
        if (distanceToTarget < 0.03f && dragVelocity.sqrMagnitude < 0.5f * 0.5f)
        {
            moveTarget.position = desiredPosition;
            dragVelocity = Vector3.zero;
        }
        else
        {
            float acceleration = dragAcceleration * Mathf.Clamp01(distanceToTarget / approachDistance);
            float angleDelta = Vector3.Angle(dragVelocity, desiredMovement);
            float multiplicativeFrictionCoefficient = Mathf.Clamp01(1 - Time.deltaTime * dragFriction);
            dragVelocity *= multiplicativeFrictionCoefficient;

            // add corrective acceleration
            acceleration += correctiveAcceleration * (angleDelta / 180f);
            dragVelocity += desiredMovement.normalized * (acceleration * Time.deltaTime);
            if (dragVelocity.sqrMagnitude > maxDragSpeed * maxDragSpeed)
            {
                dragVelocity = dragVelocity.normalized * maxDragSpeed;
            }
            moveTarget.position += dragVelocity * Time.deltaTime;
        }        
    }

    public void BeginDragging()
    {
        if (dragging)
        {
            Debug.LogError($"Dragging on {gameObject.name} started before a previous dragging operation was concluded.");
            return;
        }
        desiredOffset = DragOffset;
        draggingStartCell = Cell;
        draggingStartRotation = transform.rotation;
        dragging = true;
    }

    public bool EndDragging()
    {
        return EndDragging(out _);
    }
    public bool EndDragging(out IObstaclePlacementResult result)
    {
        if (!dragging)
        {
            Debug.LogError($"Dragging on {gameObject.name} ended before it was started.");
            result = new ObstaclePlacementFailure(this, "Invalid Dragging State");
            return false;
        }
        dragging = false;
        desiredOffset = Vector3.zero;
        Vector2Int newCell = Cell;
        result = CanBePlaced(newCell);
        if (result.Success)
        {
            // if the obstacle is fixed, we need to update our internal obstacle map.
            if (IsFixed)
            {
                World.DeregisterObstacleInCell(this, draggingStartCell);
                World.RegisterObstacle(this);
            }

            InvokeMoved(draggingStartCell, draggingStartRotation, newCell, transform.rotation);
        }
        else
        {
            // Move back to the old spot, but keep the target in its current position.
            Vector3 savedMoveTargetPos = moveTarget.transform.position;
            Quaternion savedMovedTargetRot = moveTarget.transform.rotation;
            Cell = draggingStartCell; // also moves object
            transform.rotation = draggingStartRotation;
            moveTarget.transform.SetPositionAndRotation(savedMoveTargetPos, savedMovedTargetRot);
        }
        Debug.Log($"Finished dragging {this}, success={result.Success}, reason=\"{result.Reason}\"");
        return false;
    }

    public void DragToRay(Ray aimRay, bool snapToCellCenter = true)
    {
        if(!World.Raycast(aimRay, out Vector3 point))
        {
            return;
        }
        Vector2Int targetCell = World.GetCell(point);

        if (targetCell != Cell)
        {
            // teleport to cell without moving the moveTarget
            Vector3 cachedPos = MoveTarget.position;
            Cell = targetCell;
            MoveTarget.position = cachedPos;
        }

        desiredOffset = snapToCellCenter ? 
            DragOffset :
            point + DragOffset - transform.position;
        Debug.Log($"Dragging {this} to ray, resulting offset = {desiredOffset}");
    }

    public override string ToString()
    {
        return $"Obstacle {obstacleName} in cell {Cell}, dragging={dragging}";
    }
}
