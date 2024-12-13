using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.LightTransport;

#nullable enable
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

    [SerializeField] private List<Vector2Int> occupiedOffsets;
    [SerializeField] private List<MeshRenderer> incompatibilityDisplays = new();
    [SerializeField] private MoveType moveType = MoveType.Fixed;
    [SerializeField] private ObstacleData? staticData;
    [SerializeField] private Requirements requirements = Requirements.None;
    [SerializeField] private Transform moveTarget;
    [SerializeField] private List<Effector> autoAssignSurfaceEffectors;
    public string ObstacleName => staticData != null ? staticData.obstacleName : "Obstacle";
    public bool IsFixed => moveType == MoveType.Fixed;
    public bool IsFree => moveType == MoveType.Free;
    public bool IsLive => moveType == MoveType.Live;
    public Requirements Reqs => requirements;
    public Transform MoveTarget => moveTarget;
    public MoveType MoveMode
    {
        get => moveType;
        set => moveType = value;
    }

    private bool isVolatile = false;
    public bool Volatile => isVolatile;

    // EVENTS
    public UnityEvent OnDragStart = new();
    public UnityEvent<bool> OnDragEnd = new();

    protected override void Awake()
    {
        base.Awake();
    }

    protected override void Start()
    {
        base.Start();
    }

    protected override void Update()
    {
        base.Update();
        MoveToOffset();
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

    protected override void HandleDeinit()
    {
        if (this.world == null) return;
        base.HandleDeinit();
        this.world.DeregisterObstacle(this);
    }
    protected override void HandleInit()
    {
        if (this.world == null) return;
        this.world.RegisterObstacle(this);
        this.desiredRotation = transform.rotation;
        base.HandleInit();
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
        IObstaclePlacementResult extraReqTestResult = CheckValidityNoInit(world, origin, rotation);
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
        return CheckValidityNoInit(World, newOrigin, newRotation);
    }
    public IObstaclePlacementResult CheckValidityNoInit(GridWorld world, Vector2Int newOrigin, Quaternion newRotation)
    {
        if (Reqs.HasFlag(Requirements.OnSurface))
        {
            Vector2 rotatedNormal = newRotation * Vector2.up;
            Surface? surface = world.GetSurface(newOrigin, rotatedNormal, MAX_SURFACE_NORMAL_SNAP, Surface.Flags.Virtual);
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
        return WorldManager.Instance.GameMode switch
        {
            GameMode.LevelEdit => true,
            GameMode.Setup => !IsFixed,
            GameMode.Play => IsLive,
            _ => false,
        };
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

    public bool OccupiesCells => occupiedOffsets.Count > 0;

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
        bool checkSideEffects = WorldManager.Instance.GameMode != GameMode.LevelEdit &&
            (draggingStartCell != newCell || draggingStartRotation != transform.rotation);

        result = CanBePlaced(newCell, desiredRotation, checkSideEffects);
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
        // Debug.Log($"Saving pos-rot pair {result}");
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
        // Debug.Log($"Saving pos-rot pair {result}");
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

    public bool TryRotate(int delta)
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
                return false;
            }
        }

        AnimateTo(Cell, candidateRotation);
        return true;
    }

    public void Delete()
    {
        // detach move target to visually destroy
        if (World != null && World.gameObject.activeInHierarchy)
        {
            moveTarget.parent = null;
            moveTarget.localScale = Vector3.one;
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
        return $"Obstacle {ObstacleName} in cell {Cell}, dragging={dragging}";
    }

    public override void InvokeMoved(Vector2Int originCell, Quaternion originRotation, Vector2Int targetCell, Quaternion targetRotation) { 
        base.InvokeMoved(originCell, originRotation, targetCell, targetRotation);
        this.desiredRotation = targetRotation;
    }

    public override void AnimateTo(Vector2Int cell, Quaternion rotation)
    {
        SaveTarget();
        MoveTo(cell, rotation);
        LoadTarget();
    }
}
