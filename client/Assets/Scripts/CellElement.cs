using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

[ExecuteInEditMode]
public class CellElement : MonoBehaviour
{
    [SerializeField] private GridWorld grid;
    [SerializeField] private Vector2Int cell;
    [SerializeField] private bool snapping;
    public bool Snapping
    {
        get { return snapping; }
        set { snapping = value; }
    }
    public GridWorld Grid { get { return grid; } }

    // Color
    public UnityEvent<CritterColor> OnColorChanged = new();
    [SerializeField] private CritterColor color;
    public CritterColor Color {
        get { return color; }
        set { color = value; OnColorChanged.Invoke(value); }
    }

    // Logic
    [SerializeField] private Vector3 motion;
    public Vector3 Motion
    {
        get { return motion; }
        set { motion = value; }
    }
    [SerializeField] private bool interpolateMotion;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected void Start()
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
    protected void Update()
    {
        if (grid == null)
        {
            Debug.LogWarning($"Cell element {gameObject.name} has no grid reference.");
            return;
        }

        if (snapping)
        {
            transform.position = grid.CellCenter(cell);
        }
        else
        {
            cell = grid.WorldToGrid(transform.position);
        }
    }

    public void HandleTimeStep()
    {
        // might get speed from an external source later
        StartCoroutine(MotionCoroutine(1));
    }
    public IEnumerator MotionCoroutine(float speed)
    {
        Vector3 targetPosition = transform.position + motion * grid.GridScale;
        float duration = 1 / speed;        
        if (interpolateMotion)
        {
            float t = 0;
            Vector3 initialPosition = transform.position;
            while (t < duration)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(initialPosition, targetPosition, t * speed);
                yield return new WaitForEndOfFrame();
            }
        }
        else
        {
            yield return new WaitForSeconds(duration);
        }
        transform.position = targetPosition;
    }
}
