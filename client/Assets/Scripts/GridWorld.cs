using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[ExecuteInEditMode]
public class GridWorld : MonoBehaviour
{
    [SerializeField] private float gridDepth = 1.0f;
    public float GridDepth { get { return gridDepth; } }
    [SerializeField] private float gridScale = 1.0f;
    public float GridScale { get { return gridScale; } }
    [SerializeField] private Vector2Int gridSize = new(3,3);
    public Vector2Int GridSize { get {  return gridSize; } }

    [Header("Camera Settings")]
    [SerializeField] private Camera gridCamera;
    [SerializeField] private Vector2 margin;

    public UnityEvent OnTimeStep = new();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ReadjustCameraPosition();
    }

    // Update is called once per frame
    void Update()
    {
        if (!Application.isPlaying)
        {
            ReadjustCameraPosition();
        }
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


    private Dictionary<Vector2Int, List<Surface>> surfaces = new();
    public void RegisterSurface(Surface surface)
    {
        if (!surfaces.TryGetValue(surface.cell, out var surfaceList))
        {
            surfaceList = new List<Surface>();
        }
        Debug.Log($"Registering surface {surface}");
        // maybe check to avoid duplication
        // consider using a different internal structure to a list
        surfaceList.Add(surface);

        surfaces[surface.cell] = surfaceList;
    }

    public Surface GetSurface(Vector2Int cell, Vector2 normal, float maxNormalDeltaDegrees = 60, Surface.Properties skipWallFlags = Surface.Properties.Virtual)
    {
        if (!surfaces.TryGetValue(cell, out var surfaceList))
        {
            return null;
        }

        Surface bestSurface = null;
        float bestAngle = maxNormalDeltaDegrees;
        int bestPriority = int.MaxValue;
        foreach (var surface in surfaceList)
        {
            // eligibility
            if (surface.priority > bestPriority) { continue; }
            if (surface.HasAnyFlagOf(skipWallFlags)) { continue; }

            float angle = Vector2.Angle(surface.normal, normal);
            if (angle > maxNormalDeltaDegrees) { continue; }

            // optimization
            if (surface.priority < bestPriority || angle <= bestAngle)
            {
                bestSurface = surface;
                bestPriority = surface.priority;
                bestAngle = angle;
            }
        }
        return bestSurface;
    }

    public Vector2Int LimitMotion(Vector2Int motion, Vector2Int cell, Vector2 up, out Surface impactedSurface, Surface.Properties skipWallFlags = Surface.Properties.Virtual)
    {
        Vector2Int current = cell;
        Vector2Int target = cell + motion;
        impactedSurface = null;
        // check at each step and stop early if there's an obstacle
        while (current != cell + motion) {
            Vector2Int step = MathLib.GetOrthoStep(target - current, up);
            Surface surf = GetSurface(current, -step, skipWallFlags: skipWallFlags);
            if (surf != null)
            {
                impactedSurface = surf;
                return current - cell;
            }

            current += step;
        }
        return motion;
    }

    private void OnDrawGizmos()
    {
        // Draw general size of grid
        Gizmos.color = Color.white;
        Vector3 gridSize3d = new Vector3(gridSize.x * gridScale, gridSize.y * gridScale, gridDepth);
        Gizmos.DrawWireCube(transform.position + new Vector3(gridSize3d.x / 2, gridSize3d.y / 2, 0), gridSize3d);
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

        // Surfaces
        Gizmos.color = Color.green;
        foreach (var kvp in surfaces)
        {
            var surfaceList = kvp.Value;
            var cell = kvp.Key;
            foreach (var surf in surfaceList)
            {
                surf.DrawGizmos(this);
            }
        }
    }

    public void InvokeTimeStep()
    {
        OnTimeStep.Invoke();
    }

    // CAMERA LOGIC
    public void ReadjustCameraPosition()
    {
        gridCamera.transform.position = GetCameraPositionForGridVision(Vector2Int.zero, gridSize - Vector2Int.one);
    }

    public Vector3 GetCameraPositionForGridVision(Vector2Int bottomLeftCell, Vector2Int topRightCell)
    {
        float degreesHeight = gridCamera.fieldOfView;
        float degreesWidth = degreesHeight * gridCamera.aspect;
        Vector2 requiredSize = (topRightCell - bottomLeftCell + Vector2.one) * gridScale + 2 * margin;
        float requiredDistance = Mathf.Max(
            requiredSize.y / (2 * Mathf.Tan(degreesHeight / 2 * Mathf.Deg2Rad)),
            requiredSize.x / (2 * Mathf.Tan(degreesWidth  / 2 * Mathf.Deg2Rad))
        );
        Vector3 cameraFocusPoint = (CellCenter(bottomLeftCell) + CellCenter(topRightCell)) / 2;
        return cameraFocusPoint + Vector3.back * (requiredDistance + gridDepth / 2);
    }
}
