using System;
using System.Collections;
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
    [SerializeField] protected Vector2Int cell;
    public virtual Vector2Int Cell { get
        {
            if (cellValueDirty)
            {
                cellValueDirty = false;
                cell = world.GetCell(transform.position - snapOffset);
            }
            return cell;
        }
    }
    [SerializeField] protected bool snapping;
    public bool Snapping
    {
        get { return snapping; }
        set { snapping = value; }
    }
    [SerializeField] protected Vector3 snapOffset;
    protected bool cellValueDirty = true;

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

    // Update is called once per frame
    protected virtual void Update()
    {
        if (world == null)
        {
            Debug.LogWarning($"Cell element {gameObject.name} has no grid reference.");
            return;
        }

        if (snapping)
        {
            transform.position = world.GetCellCenter(Cell) + snapOffset;
        }
        else
        {
            cell = world.GetCell(transform.position - snapOffset);
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
}
