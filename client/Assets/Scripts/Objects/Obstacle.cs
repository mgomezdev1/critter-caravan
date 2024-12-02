using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.LightTransport;

#nullable enable
public interface IObstaclePlacementResult
{
    public Obstacle? Obstacle { get; }
    public bool Success { get; }
    public string Reason { get; }

    public IEnumerable<Obstacle> GetProblemObstacles();
    public IObstaclePlacementResult Blame(Obstacle? otherObstacle);
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
    public string Reason { get; protected set; }

    public ObstaclePlacementFailure(Obstacle? obstacle, string reason)
    {
        this.obstacle = obstacle;
        Reason = reason;
    }
    public virtual IEnumerable<Obstacle> GetProblemObstacles() {
        if (obstacle == null) yield break;
        yield return obstacle; 
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
        Reason = $"{obstacle?.ObstacleName ?? "obstacle"} infringes on the area of {conflictObstacle.ObstacleName} on cell {conflictCell}";
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
        Reason = $"{obstacle?.ObstacleName ?? "obstacle"} requires a surface below cell {cell} to stand on.";
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

#pragma warning disable CS8618
public class Obstacle : CellElement
{
    [Flags]
    public enum Requirements
    {
        None = 0,
        OnSurface = 1
    }
    public enum MoveType
    {
        Fixed = 0,
        Free = 1,
        Live = 2
    }

    [SerializeField] List<Vector2Int> occupiedOffsets;
    [SerializeField] List<MeshRenderer> incompatibilityDisplays = new();
    [SerializeField] MoveType moveType = MoveType.Fixed;
    [SerializeField] private string obstacleName = "Obstacle";
    [SerializeField] private Requirements requirements = Requirements.None;
    [SerializeField] private Transform moveTarget;
    [SerializeField] private List<Effector> autoAssignSurfaceEffectors;
    public string ObstacleName => obstacleName;
    public bool IsFixed => moveType == MoveType.Fixed;
    public bool IsFree => moveType == MoveType.Free;
    public bool IsLive => moveType == MoveType.Live;
    public MoveType MoveMode => moveType;
    public Requirements Reqs => requirements;
    public Transform MoveTarget => moveTarget;

    private bool isVolatile = false;
    public bool Volatile => isVolatile;

    // EVENTS
    public UnityEvent OnDragStart = new();
    public UnityEvent<bool> OnDragEnd = new();

    protected override void Awake()
    {
        base.Awake();
        desiredRotation = moveTarget.rotation;
    }

    protected override void Start()
    {
        base.Start();
        desiredRotation = transform.rotation;
        if (!Volatile)
            World.RegisterObstacle(this);
    }

    protected override void Update()
    {
        base.Update();
        MoveToOffset();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        var world = World;
        if (world != null)
        {
            world.DeregisterObstacle(this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = UnityEngine.Color.yellow;
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

    public IObstaclePlacementResult CanBePlaced(Vector2Int origin, bool checkSideEffects = true)
    {
        return CanBePlaced(origin, transform.rotation, checkSideEffects);
    }
    public IObstaclePlacementResult CanBePlaced(Vector2Int newOrigin, Quaternion newRotation, bool checkSideEffects = true)
    {
        GridWorld world = World;
        var preliminaryResult = this.CanBePlacedNoInit(world, newOrigin, newRotation);
        if (!preliminaryResult.Success)
        {
            return preliminaryResult;
        }

        // Side effect check
        Vector2Int currentCell = Cell;
        if (checkSideEffects)
        {
            SaveTransform(transform);
            World.SuppressEffectorUpdates = true;
            World.SuppressSurfaceUpdates = true;
            transform.SetPositionAndRotation(World.GetCellCenter(newOrigin), newRotation);
            InvokeMoved(currentCell, transform.rotation, newOrigin, newRotation);
            IObstaclePlacementResult result = World.CheckValidObstacles(this);
            LoadTransform(transform);
            InvokeMoved(newOrigin, newRotation, currentCell, transform.rotation);
            World.SuppressEffectorUpdates = false;
            World.SuppressSurfaceUpdates = false;

            if (result is ObstaclePlacementFailure failure) return new ObstacleSideEffectFailure(this, failure);
            if (!result.Success)
            {
                throw new Exception($"Class inheritance error: {result}.Success = false but it doesn't inherit from ObstaclePlacementFailure.");
            }
        }

        return new ObstaclePlacementSuccess(this);
    }

    public IObstaclePlacementResult CanBePlacedNoInit(GridWorld world, Vector2Int origin, Quaternion rotation)
    {
        foreach (var occupiedCell in GetOccupiedCells(origin, rotation))
        {
            Obstacle? conflict = world.GetObstacle(occupiedCell);
            if (conflict != null && conflict != this)
            {
                // Might want to introduce localization for this
                return new ObstacleConflict(this, conflict, occupiedCell);
            }
        }

        // Flag-based requirement testing
        IObstaclePlacementResult extraReqTestResult = CheckValidity(origin, rotation);
        if (!extraReqTestResult.Success) return extraReqTestResult;

        return new ObstaclePlacementSuccess(this);
    }

    const float MAX_SURFACE_NORMAL_SNAP = 80;
    public IObstaclePlacementResult CheckValidity()
    {
        return CheckValidity(Cell, transform.rotation);
    }
    public IObstaclePlacementResult CheckValidity(Vector2Int newOrigin, Quaternion newRotation)
    {
        if (Reqs.HasFlag(Requirements.OnSurface))
        {
            Vector2 rotatedNormal = newRotation * Vector2.up;
            Surface? surface = World.GetSurface(newOrigin, rotatedNormal, MAX_SURFACE_NORMAL_SNAP, Surface.Flags.Virtual);
            if (surface == null)
            {
                return new ObstacleMissingSurface(this, newOrigin, rotatedNormal);
            }
        }

        return new ObstaclePlacementSuccess(this);
    }

    public bool TrySnapToSurface([NotNullWhen(true)]out Surface? surface)
    {
        surface = World.GetSurface(Cell, transform.up, MAX_SURFACE_NORMAL_SNAP);
        if (surface == null) { return false; }

        SnapToSurface(surface);
        return true;
    }
    public void SnapToSurface(Surface surface)
    {
        SaveTarget();
        // all obstacles should, by default, be on the center of a cell.
        // So we'll assume the height of an obstacle matches half a cell's side length
        float height = World.GridScale / 2;
        desiredRotation = surface.GetStandingObstacleRotation();
        transform.SetPositionAndRotation(
            surface.GetStandingPosition(World) + (Vector3)surface.normal * height,
            desiredRotation
        );
        LoadTarget();
    }

    [Header("Incompatibility Settings")]
    [SerializeField] private float incompatibilityInitialAlpha = 0.5f;
    private float incompatibilityDuration = 1f;
    private float incompatibilityTimer = 0f;

    public void ShowIncompatibility(float duration, bool allowTimeDecrease = false)
    {
        if (incompatibilityTimer <= 0f)
        {
            // If coroutine is not running, set it up and run it
            incompatibilityDuration = duration;
            incompatibilityTimer = duration;
            foreach (var renderer in incompatibilityDisplays)
            {
                StartCoroutine(ShowIncompatibilityCoroutine(renderer));
            }
        }
        else
        {
            // If it is already running, alter values accordingly
            // We'll ensure that the remaining display time never decreases unless the flag to allow it is set
            incompatibilityTimer = allowTimeDecrease ? duration : Mathf.Max(duration, incompatibilityTimer);
            // And we'll always consider the total duration it as if it had just started when this function is called;
            incompatibilityDuration = incompatibilityTimer;
        }
    }

    public IEnumerator ShowIncompatibilityCoroutine(MeshRenderer renderer)
    {
        Color initialColor = renderer.material.color;
        renderer.enabled = true;
        while (incompatibilityTimer > 0)
        {
            float alphaFactor = incompatibilityTimer / incompatibilityDuration;
            renderer.material.color = new Color(initialColor.r, initialColor.g, initialColor.b, incompatibilityInitialAlpha * alphaFactor * alphaFactor);
            yield return new WaitForEndOfFrame();
            incompatibilityTimer -= Time.deltaTime;
        }
        renderer.enabled = false;
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
    public bool CanBeMoved()
    {
        switch (WorldManager.Instance.GameMode)
        {
            case GameMode.LevelEdit: return true;
            case GameMode.Setup: return !IsFixed;
            case GameMode.Play: return IsLive;
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
        Vector3 initialPosition = moveTarget.position;
        moveTarget.rotation = Quaternion.RotateTowards(moveTarget.rotation, desiredRotation, rotationAngularVelocityDegrees * Time.deltaTime);

        Vector3 desiredPosition = transform.position + desiredOffset;
        Vector3 desiredMovement = desiredPosition - initialPosition;
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
            moveTarget.position = initialPosition + dragVelocity * Time.deltaTime;
        }
    }

    public void BeginDragging()
    {
        if (dragging)
        {
            Debug.LogError($"Dragging on {gameObject.name} started before a previous dragging operation was concluded.");
            return;
        }

        draggingStartCell = Cell;
        draggingStartRotation = transform.rotation;

        // When we start dragging, snap to cell and snap rotation
        SaveTarget();
        transform.SetPositionAndRotation(
            World.GetCellCenter(Cell),
            MathLib.SnapToOrtho(transform.rotation)
        );
        LoadTarget();

        desiredOffset = DragOffset;
        desiredRotation = transform.rotation;
        dragging = true;
        OnDragStart.Invoke();
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

        Vector2Int newCell = World.GetCell(transform.position + desiredOffset);
        dragging = false;
        desiredOffset = Vector3.zero;
        result = CanBePlaced(newCell, desiredRotation, draggingStartCell != newCell || draggingStartRotation != transform.rotation);
        if (result.Success)
        {
            // Unmark volatile if successfully placed
            isVolatile = false;

            // Move to target cell
            SaveTarget();
            Cell = newCell;
            transform.rotation = desiredRotation;
            LoadTarget();

            // if the obstacle is fixed, we need to update our internal obstacle map.
            if (IsFixed)
            {
                World.DeregisterObstacleInCell(this, draggingStartCell);
                World.RegisterObstacle(this);
            }

            InvokeMoved(draggingStartCell, draggingStartRotation, newCell, transform.rotation);
            
            if (Reqs.HasFlag(Requirements.OnSurface) && TrySnapToSurface(out Surface? s))
            {
                Debug.Log($"Snapping {this} to {s}");
            }
        }
        else
        {
            // If a volatile obstacle can't be placed, it is destroyed instead.
            if (Volatile)
            {
                Delete();
                return false;
            }
            else
            {
                // Move back to the old spot, but keep the target in its current position.
                SaveTarget();
                Cell = draggingStartCell; // also moves object
                transform.rotation = draggingStartRotation;
                desiredRotation = transform.rotation;
                LoadTarget();
            }
        }
        Debug.Log($"Finished dragging {this}, success={result.Success}, reason=\"{result.Reason}\"");
        OnDragEnd.Invoke(result.Success);
        return result.Success;
    }

    private Stack<Tuple<Vector3, Quaternion>> transformStack = new();
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Tuple<Vector3, Quaternion> SaveTarget()
    {
        return SaveTransform(moveTarget);
    }
    private Tuple<Vector3, Quaternion> SaveTransform(Transform t)
    {
        Tuple<Vector3, Quaternion> result = new(t.position, t.rotation);
        transformStack.Push(result);
        Debug.Log($"Saving pos-rot pair {result}");
        return result;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Tuple<Vector3, Quaternion> LoadTarget()
    {
        return LoadTransform(moveTarget);
    }
    private Tuple<Vector3, Quaternion> LoadTransform(Transform t)
    {
        Tuple<Vector3, Quaternion> result = transformStack.Pop();
        t.SetPositionAndRotation(result.Item1, result.Item2);
        Debug.Log($"Saving pos-rot pair {result}");
        return result; 
    }

    public void DragToRay(Ray aimRay, bool snapToCellCenter = true)
    {
        if(!World.Raycast(aimRay, out Vector3 point))
        {
            return;
        }

        if (snapToCellCenter)
        {
            point = World.GetCellCenter(World.GetCell(point));
        }

        desiredOffset = point + DragOffset - transform.position;
        Debug.Log($"Dragging {this} to ray, resulting offset = {desiredOffset}");
    }

    public void Rotate(int delta)
    {
        Vector3 newUp = MathLib.RotateVector2(transform.rotation * Vector2.up, delta * (-90));
        Quaternion candidateRotation = Quaternion.LookRotation(Vector3.forward, newUp);
        if (!dragging)
        {
            var placementResult = CanBePlaced(Cell, candidateRotation, true);
            if (!placementResult.Success)
            {
                WorldManager.Instance.HandlePlacementError(placementResult, true);
                // TODO: Consider adding a "wiggle" animation
                return;
            }
        }
        
        SaveTarget();
        desiredRotation = candidateRotation;
        transform.rotation = desiredRotation;
        LoadTarget();
    }

    public void Delete()
    {
        // detach move target to visually destroy
        if (World != null && World.gameObject.activeInHierarchy)
        {
            moveTarget.parent = null;
            World.AnimateDisappearance(moveTarget.gameObject, 0.5f, true);
        }
        // then destroy this object immediately
        Destroy(gameObject);
    }

    public void MarkVolatile()
    {
        isVolatile = true;
    }

    public override string ToString()
    {
        return $"Obstacle {obstacleName} in cell {Cell}, dragging={dragging}";
    }
}
