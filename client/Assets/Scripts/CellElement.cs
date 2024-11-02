using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

[ExecuteInEditMode]
public class CellElement : MonoBehaviour
{
    [SerializeField] protected GridWorld grid;
    public GridWorld Grid { get { return grid; } }
    [SerializeField] protected Vector2Int cell;
    public Vector2Int Cell { get { return cell; } }
    [SerializeField] protected bool snapping;
    public bool Snapping
    {
        get { return snapping; }
        set { snapping = value; }
    }
    [SerializeField] protected Vector3 snapOffset;

    // Color
    public UnityEvent<CritterColor> OnColorChanged = new();
    [SerializeField] protected CritterColor color;
    public CritterColor Color {
        get { return color; }
        set { color = value; OnColorChanged.Invoke(value); }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected virtual void Start()
    {
        if (grid == null)
        {
            Debug.LogWarning($"Cell element {gameObject.name} has no grid reference.");
            return;
        }
        cell = grid.WorldToGrid(transform.position);

        OnColorChanged.Invoke(color);
        grid.OnTimeStep.AddListener(HandleTimeStep);
    }

    // Update is called once per frame
    protected virtual void Update()
    {
        if (grid == null)
        {
            Debug.LogWarning($"Cell element {gameObject.name} has no grid reference.");
            return;
        }

        if (snapping)
        {
            transform.position = grid.CellCenter(cell) + snapOffset;
        }
        else
        {
            cell = grid.WorldToGrid(transform.position - snapOffset);
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
