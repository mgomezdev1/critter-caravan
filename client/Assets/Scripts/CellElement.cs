using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

[ExecuteInEditMode]
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
        OnColorChanged.Invoke(color);
        world.OnTimeStep.AddListener(HandleTimeStep);
    }

    protected virtual void OnDestroy()
    {
    }

    public void Initialize(GridWorld world)
    {
        if (didStart)
        {
            Debug.Log($"Initialization of {gameObject.name} done late.");
        }
        this.world = world;
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

    public void MoveTo(Vector2Int newCell, Quaternion newRotation)
    {
        Vector3 newPosition = World.GetCellCenter(newCell);
        MoveTo(newCell, newPosition, newRotation);
    }
    public void MoveTo(Vector3 newPosition, Quaternion newRotation)
    {
        Vector2Int newCell = World.GetCell(newPosition);
        MoveTo(newCell, newPosition, newRotation);
    }
    public void MoveTo(Vector2Int newCell, Vector3 newPosition, Quaternion newRotation)
    {
        Vector2Int oldCell = Cell;
        Quaternion oldRotation = transform.rotation;
        cell = newCell;
        transform.SetPositionAndRotation(newPosition, newRotation);
        InvokeMoved(oldCell, oldRotation, newCell, newRotation);
    }

    public virtual void InvokeMoved(Vector2Int originCell, Quaternion originRotation, Vector2Int targetCell, Quaternion targetRotation)
    {
        MarkPositionDirty();
        if (didStart)
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
