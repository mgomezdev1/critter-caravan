using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

public class CellElement : MonoBehaviour
{
    [SerializeField] protected GridWorld world;
    public GridWorld World { 
        get { return world; }
        set { 
            if (didStart || world != null) { Debug.LogWarning($"Grid replaced from existing value or after start was run on cell element {gameObject.name}. Time step events will be incorrectly hooked."); }
            world = value; 
        } 
    }
    protected Vector2Int cell;
    public virtual Vector2Int Cell { 
        get
        {
            if (cellValueDirty)
            {
                cellValueDirty = false;
                cell = world.GetCell(transform.position);
            }
            return cell;
        }
        set
        {
            cell = value;
            cellValueDirty = false;
            transform.position = world.GetCellCenter(cell);
        }
    }
    protected bool cellValueDirty = true;

    public bool Generated { get; set; } = false;
    private bool initialized = false;

    // Color
    public UnityEvent<CritterColor> OnColorChanged = new();
    [SerializeField] protected CritterColor color;
    public CritterColor Color {
        get { return color; }
        set { color = value; OnColorChanged.Invoke(value); }
    }

    protected virtual void Awake()
    {
        // do nothing
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected virtual void Start()
    {
        if (world == null)
        {
            Debug.LogWarning($"Cell element {gameObject.name} has no grid reference.");
            return;
        }
        if (!initialized)
        {
            HandleInit();
            initialized = true;
        }
    }

    protected virtual void OnDestroy()
    {
        if (world != null && initialized)
        {
            HandleDeinit();
            initialized = false;
        }
    }

    public void Initialize(GridWorld world)
    {
        if (didStart)
        {
            Debug.Log($"Initialization of {gameObject.name} done late.");
        }

        if (initialized)
        {
            Reinitialize(world);
            return;
        }

        this.world = world;
        HandleInit();
        initialized = true;
    }
    protected void Reinitialize(GridWorld world)
    {
        if (world == null)
            throw new Exception($"Invalid Initialization for {gameObject.name}, no world provided.");

        if (!initialized || this.world == null)
        {
            this.world = world;
            HandleInit();
        }
        else if (this.world != world)
        {
            HandleDeinit();
            initialized = false;
            this.world = world;
            HandleInit();
        }
        initialized = true;
    }
    protected virtual void HandleDeinit()
    {
        world.OnTimeStep.RemoveListener(HandleTimeStep);
        foreach (var surfaceComponent in GetComponentsInChildren<IHasSurfaces>())
        {
            foreach (var surf in surfaceComponent.GetSurfaces())
            {
                world.DeregisterSurface(surf);
            }
        }
        foreach (var effector in GetComponentsInChildren<IEffector>())
        {
            world.DeregisterEffector(effector);
        }
    }
    protected virtual void HandleInit()
    {
        foreach (var effector in GetComponentsInChildren<IEffector>())
        {
            world.RegisterEffector(effector);
        }
        foreach (var surfaceComponent in GetComponentsInChildren<IHasSurfaces>())
        {
            foreach (var surf in surfaceComponent.GetSurfaces())
            {
                world.RegisterSurface(surf);
            }
        }
        world.OnTimeStep.AddListener(HandleTimeStep);
        OnColorChanged.Invoke(color);
    }

    // Update is called once per frame
    protected virtual void Update()
    {
        if (world == null)
        {
            Debug.LogWarning($"Cell element {gameObject.name} has no grid reference.");
            return;
        }
        if (!Application.isPlaying)
        {
            this.cell = world.GetCell(transform.position);
        }
    }

    public virtual void HandleTimeStep()
    {
        // nothing here so far
        // might be useful in subclasses
    }
    public virtual void AfterTimeStep()
    {
        // nothing here so far
        // might be useful in subclasses
    }

    public void MarkPositionDirty()
    {
        cellValueDirty = true;
    }

    public void MoveTo(Vector2Int newCell, Quaternion newRotation, bool suppressEvent = false)
    {
        Vector3 newPosition = World.GetCellCenter(newCell);
        MoveTo(newCell, newPosition, newRotation);
    }
    public void MoveTo(Vector3 newPosition, Quaternion newRotation, bool suppressEvent = false)
    {
        Vector2Int newCell = World.GetCell(newPosition);
        MoveTo(newCell, newPosition, newRotation);
    }
    public void MoveTo(Vector2Int newCell, Vector3 newPosition, Quaternion newRotation, bool suppressEvent = false)
    {
        Vector2Int oldCell = Cell;
        Quaternion oldRotation = transform.rotation;
        cell = newCell;
        transform.SetPositionAndRotation(newPosition, newRotation);
        if (suppressEvent)
        {
            MarkPositionDirty();
        }
        else
        {
            InvokeMoved(oldCell, oldRotation, newCell, newRotation);
        }
    }

    public virtual void InvokeMoved(Vector2Int originCell, Quaternion originRotation, Vector2Int targetCell, Quaternion targetRotation)
    {
        MarkPositionDirty();
        if (initialized)
        {
            foreach (IMovable movable in GetComponents<IMovable>())
            {
                movable.AfterMove(originCell, originRotation, targetCell, targetRotation);
            }
        }
    }

    public virtual void AnimateTo(Vector2Int cell, Quaternion rotation)
    {
        MoveTo(cell, rotation);
    }
}
