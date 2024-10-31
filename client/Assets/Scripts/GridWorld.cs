using UnityEngine;
using UnityEngine.Events;

public class GridWorld : MonoBehaviour
{
    [SerializeField] private float gridDepth = 1.0f;
    public float GridDepth { get { return gridDepth; } }
    [SerializeField] private float gridScale = 1.0f;
    public float GridScale { get { return gridScale; } }
    [SerializeField] private Vector2Int gridSize = new(3,3);
    public Vector2Int GridSize { get {  return gridSize; } }

    public UnityEvent OnTimeStep = new();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnDrawGizmos()
    {
        // Draw general size of grid
        Gizmos.color = Color.white;
        Vector3 gridSize3d = new Vector3(gridSize.x * gridScale, gridSize.y * gridScale, gridDepth);
        Gizmos.DrawWireCube(transform.position + new Vector3(gridSize3d.x / 2, gridSize3d.y / 2, 0), gridSize3d);
    }

    public Vector2Int WorldToGrid(Vector3 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt(Mathf.Clamp((position.x - transform.position.x) / gridScale, 0, gridSize.x - 1)),
            Mathf.FloorToInt(Mathf.Clamp((position.y - transform.position.y) / gridScale, 0, gridSize.y - 1))
        );
    }

    public Vector3 CellCenter(Vector2Int cell)
    {
        return transform.position + new Vector3(
            (cell.x + 0.5f) * gridScale,
            (cell.y + 0.5f) * gridScale,
            0
        );
    }

    private void OnDrawGizmosSelected()
    {
        // Draw basic size;
        OnDrawGizmos();
        // Draw lines on the back and front layers of the grid
        for (float z = -gridDepth / 2; z <= gridDepth / 2; z += gridDepth)
        {
            // Draw the back in red and the front in white
            Gizmos.color = z > 0 ? Color.red : Color.white;
            // Draw vertical lines
            float ySize = gridSize.y * gridScale;
            for (float x = 0; x < gridSize.x * gridScale; x += gridScale)
            {
                Gizmos.DrawLine(transform.position + new Vector3(x, 0, z), 
                                transform.position + new Vector3(x, ySize, z));
            }
            // Draw horizontal lines
            float xSize = gridSize.x * gridScale;
            for (float y = 0; y < gridSize.y * gridScale; y += gridScale)
            {
                Gizmos.DrawLine(transform.position + new Vector3(0, y, z),
                                transform.position + new Vector3(xSize, y, z));
            }
        }
    }
}
