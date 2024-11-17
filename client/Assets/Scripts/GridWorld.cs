using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
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
    [HideInInspector] public UnityEvent OnSurfacesUpdated = new();
    private bool surfacesDirty = false;
    private bool suppressSurfaceUpdates = false;
    public bool SuppressSurfaceUpdates { get => suppressSurfaceUpdates; set => suppressSurfaceUpdates = value; }
    [HideInInspector] public UnityEvent OnEffectorsUpdated = new();
    private bool effectorsDirty = false;
    private bool suppressEffectorUpdates = false;
    public bool SuppressEffectorsUpdates { get => suppressEffectorUpdates; set => suppressEffectorUpdates = value; }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ReadjustCameraPosition();
        foreach (var surf in GetBorderSurfaces())
        {
            RegisterSurface(surf);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!Application.isPlaying)
        {
            ReadjustCameraPosition();
        }

        if (surfacesDirty)
        {
            // maybe do some precalculations?
            surfacesDirty = false;
            OnSurfacesUpdated.Invoke();
        }
        if (effectorsDirty)
        {
            // maybe do some precalculations again
            effectorsDirty = false;
            OnEffectorsUpdated.Invoke();
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

    private readonly Dictionary<Vector2Int, HashSet<Surface>> surfaces = new();
    public void RegisterSurface(Surface surface)
    {
        if (!surfaces.TryGetValue(surface.Cell, out var surfaceList))
        {
            surfaceList = new HashSet<Surface>();
            surfaces[surface.Cell] = surfaceList;
        }
        // maybe check to avoid duplication
        // consider using a different internal structure to a list
        surfaceList.Add(surface);
        if (!suppressSurfaceUpdates) surfacesDirty = true;
    }

    public bool DeregisterSurface(Surface surface)
    {
        if (surface == null) return false;
        if (!surfaces.TryGetValue(surface.Cell, out var surfaceList))
        {
            return false;
        }

        if (surfaceList.Remove(surface))
        {
            if (!suppressSurfaceUpdates) surfacesDirty = true;
            return true;
        }
        return false;
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

    public Vector2Int LimitMotion(Vector2Int motion, Vector2Int cell, Vector2 up, Surface.Properties skipWallFlags = Surface.Properties.Virtual, EffectorFlags effectorFlags = EffectorFlags.All, bool isFalling = false)
    {
        return LimitMotion(motion, cell, up, out _, out _, skipWallFlags, effectorFlags, isFalling);
    }

    public Vector2Int LimitMotion(Vector2Int motion, Vector2Int cell, Vector2 up, out Surface impactedSurface, Surface.Properties skipWallFlags = Surface.Properties.Virtual, EffectorFlags effectorFlags = EffectorFlags.All, bool isFalling = false)
    {
        return LimitMotion(motion, cell, up, out impactedSurface, out _, skipWallFlags, effectorFlags, isFalling);
    }

    public Vector2Int LimitMotion(Vector2Int motion, Vector2Int cell, Vector2 up, out List<IEffector> traversedEffectors, Surface.Properties skipWallFlags = Surface.Properties.Virtual, EffectorFlags effectorFlags = EffectorFlags.All, bool isFalling = false)
    {
        return LimitMotion(motion, cell, up, out _, out traversedEffectors, skipWallFlags, effectorFlags, isFalling);
    }

    public Vector2Int LimitMotion(Vector2Int motion, Vector2Int cell, Vector2 up, out Surface impactedSurface, out List<IEffector> traversedEffectors, Surface.Properties skipWallFlags = Surface.Properties.Virtual, EffectorFlags effectorFlags = EffectorFlags.All, bool isFalling = false)
    {
        Vector2Int current = cell;
        Vector2Int target = cell + motion;
        impactedSurface = null;
        traversedEffectors = new();
        // check at each step and stop early if there's an obstacle
        while (current != cell + motion) {
            Vector2Int step = MathLib.GetOrthoStep(target - current, up);
            bool breakMovement = false;
            foreach (var eff in GetEffectors(current, flags: effectorFlags))
            {
                traversedEffectors.Add(eff);
                if ((isFalling && eff.Flags.HasFlag(EffectorFlags.StopFall)) || eff.Flags.HasFlag(EffectorFlags.BlockMovement))
                {
                    breakMovement = true;
                }
            }
            Surface surf = GetSurface(current, -step, skipWallFlags: skipWallFlags);
            if (surf != null)
            {
                impactedSurface = surf;
                breakMovement = true;
            }

            if (breakMovement)
            {
                return current - cell;
            }

            current += step;
        }
        return motion;
    }

    private readonly Dictionary<Vector2Int, HashSet<IEffector>> effectors = new();
    public void RegisterEffector(IEffector effector)
    {
        if (!effectors.TryGetValue(effector.Cell, out var cellEffectors))
        {
            cellEffectors = new HashSet<IEffector>();
            effectors[effector.Cell] = cellEffectors;
        }
        // maybe check to avoid duplication
        // consider using a different internal structure to a list
        cellEffectors.Add(effector);
        if (!suppressEffectorUpdates) effectorsDirty = true;
    }

    public bool DeregisterEffector(IEffector effector)
    {
        if (effector == null) return false;
        if (!effectors.TryGetValue(effector.Cell, out var cellEffectors))
        {
            return false;
        }

        if (cellEffectors.Remove(effector))
        {
            if (!suppressEffectorUpdates) effectorsDirty = true;
            return true;
        }
        return false;
    }

    public IEnumerable<IEffector> GetEffectors(Vector2Int cell, EffectorFlags flags = EffectorFlags.All)
    {
        if (!effectors.TryGetValue(cell, out var cellEffectors))
        {
            yield break;
        }

        foreach (var eff in cellEffectors)
        {
            if (eff.HasAnyFlag(flags))
            {
                yield return eff;    
            }
        }
    }

    public bool IsCellValid(Vector2Int cell)
    {
        return cell.x < GridSize.x && cell.x >= 0 && cell.y < GridSize.y && cell.y >= 0;
    }

    private void OnDrawGizmos()
    {
        // Draw general size of grid
        Gizmos.color = Color.white;
        Vector3 gridSize3d = new(gridSize.x * gridScale, gridSize.y * gridScale, gridDepth);
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

        if (!Application.isPlaying)
        {
            foreach (var surf in GetBorderSurfaces(false))
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
        float tangentHeight = Mathf.Tan(gridCamera.fieldOfView / 2 * Mathf.Deg2Rad);
        float tangentWidth = tangentHeight * gridCamera.aspect;
        Vector2 requiredSize = (topRightCell - bottomLeftCell + Vector2.one) * gridScale + 2 * margin;
        float requiredDistance = Mathf.Max(
            requiredSize.y / (2 * tangentHeight),
            requiredSize.x / (2 * tangentWidth)
        );
        Vector3 cameraFocusPoint = (CellCenter(bottomLeftCell) + CellCenter(topRightCell)) / 2;
        return cameraFocusPoint + Vector3.back * (requiredDistance + gridDepth / 2);
    }

    List<Surface> borderSurfaceCache = null;
    const Surface.Properties borderSurfaceProperties = Surface.Properties.Fatal | Surface.Properties.IgnoreRotationOnLanding;
    public List<Surface> GetBorderSurfaces(bool useCache = true)
    {
        if (useCache && borderSurfaceCache != null)
        {
            return borderSurfaceCache;
        }

        borderSurfaceCache = new List<Surface>();

        for (int x = 0; x < GridSize.x; x++)
        {
            borderSurfaceCache.Add(new Surface(
                new Vector2Int(x, 0),
                Vector2.up,
                Vector2.down * (GridScale / 2),
                borderSurfaceProperties
            ));
            borderSurfaceCache.Add(new Surface(
                new Vector2Int(x, GridSize.y - 1),
                Vector2.down,
                Vector2.up * (GridScale / 2), 
                borderSurfaceProperties
            ));
        }
        for (int y = 0; y < GridSize.y; y++)
        {
            borderSurfaceCache.Add(new Surface(
                new Vector2Int(0, y),
                Vector2.right,
                Vector2.left * (GridScale / 2),
                borderSurfaceProperties
            ));
            borderSurfaceCache.Add(new Surface(
                new Vector2Int(GridSize.x - 1, y),
                Vector2.left,
                Vector2.right * (GridScale / 2),
                borderSurfaceProperties
            ));
        }
        return borderSurfaceCache;
    }

    public void ResetSurfaces()
    {
        surfaces.Clear();
    }
    public void ResetEffectors()
    {
        effectors.Clear();
    }
}

# if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(GridWorld))]
public class GridWorldEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Reset Surfaces"))
        {
            GridWorld script = (GridWorld)target;
            script.ResetSurfaces();
        }
    }
}
# endif