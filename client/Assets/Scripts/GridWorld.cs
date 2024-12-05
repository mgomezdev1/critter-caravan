using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

#nullable enable
#pragma warning disable CS8618
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
    public Camera GridCamera => gridCamera;

    public UnityEvent OnTimeStep = new();
    [HideInInspector] public UnityEvent OnSurfacesUpdated = new();
    private bool surfacesDirty = false;
    private bool suppressSurfaceUpdates = false;
    public bool SuppressSurfaceUpdates { get => suppressSurfaceUpdates; set => suppressSurfaceUpdates = value; }
    [HideInInspector] public UnityEvent OnEffectorsUpdated = new();
    private bool effectorsDirty = false;
    private bool suppressEffectorUpdates = false;
    public bool SuppressEffectorUpdates { get => suppressEffectorUpdates; set => suppressEffectorUpdates = value; }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ReadjustCameraPosition();
        foreach (var surf in GetBorderSurfaces())
        {
            RegisterSurface(surf);
        }
    }

    public float TimeSpeed { get; set; }
    float timeFragment;
    public float TimeFragment => timeFragment;
    // Update is called once per frame
    void Update()
    {
# if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ReadjustCameraPosition();
        }
# endif

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

        if (WorldManager.Instance.GameMode == GameMode.Play)
        {
            timeFragment += Time.deltaTime * TimeSpeed;
            if (timeFragment >= 1)
            {
                timeFragment -= 1;
                ExternalInvokeTimeStep();
            }
            CheckEntityCollisions();
        }
        else //edit or setup mode
        {
            if (uncheckedObstacles.Count > 0)
            {
                var sideEffectResults = CheckValidObstacles(uncheckedObstacles[^1]);
                if (!sideEffectResults.Success)
                {
                    WorldManager.Instance.HandlePlacementError(sideEffectResults);
                    foreach (var obstacle in uncheckedObstacles) { obstacle.Delete(); }
                }
                uncheckedObstacles.Clear();
            }
        }
    }

    public Vector2Int GetCell(Vector3 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt(Mathf.Clamp((position.x - transform.position.x) / gridScale, 0, gridSize.x - 1)),
            Mathf.FloorToInt(Mathf.Clamp((position.y - transform.position.y) / gridScale, 0, gridSize.y - 1))
        );
    }

    public Vector3 GetCellCenter(Vector2Int cell)
    {
        return transform.position + new Vector3(
            (cell.x + 0.5f) * gridScale,
            (cell.y + 0.5f) * gridScale,
            0
        );
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 GetCellCenter(Vector3 position)
    {
        return GetCellCenter(GetCell(position));
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

    private readonly Dictionary<Vector2Int, Obstacle> fixedObstacles = new();
    private readonly HashSet<Obstacle> dynamicObstacles = new();
    public void RegisterObstacle(Obstacle obstacle)
    {
        if (obstacle.IsFixed)
        {
            foreach (var cell in obstacle.GetOccupiedCells())
            {
                if (fixedObstacles.TryGetValue(cell, out Obstacle existingObstacle) && existingObstacle != obstacle)
                {
                    Debug.LogError($"Attempting to register fixed obstacle ({obstacle.gameObject.name}) on cell {cell}, occupied by {existingObstacle.gameObject.name}");
                }
                fixedObstacles[cell] = obstacle;
            }
        }
        else
        {
            dynamicObstacles.Add(obstacle);
        }
    }
    public bool DeregisterObstacle(Obstacle obstacle)
    {
        return DeregisterObstacleInCell(obstacle, obstacle.Cell);
    }
    public bool DeregisterObstacleInCell(Obstacle obstacle, Vector2Int origin)
    {
        if (obstacle.IsFixed)
        {
            bool anythingRemoved = false;
            foreach (var cell in obstacle.GetOccupiedCells(origin))
            {
                if (fixedObstacles.TryGetValue(cell, out Obstacle existingObstacle) && existingObstacle == obstacle)
                {
                    fixedObstacles.Remove(cell);
                    anythingRemoved = true;
                }
            }
            return anythingRemoved;
        }
        else
        {
            return dynamicObstacles.Remove(obstacle);
        }
    }

    public Obstacle? GetObstacle(Vector2Int cell)
    {
        if (fixedObstacles.TryGetValue(cell, out Obstacle obstacle))
        {
            return obstacle;
        }

        foreach (var dynObstacle in dynamicObstacles)
        {
            foreach (var obstacleCell in dynObstacle.GetOccupiedCells())
            {
                if (obstacleCell == cell)
                {
                    return dynObstacle;
                }
            }
        }

        return null;
    }

    public Surface? GetSurface(Vector2Int cell, Vector2 normal, float maxNormalDeltaDegrees = 60, Surface.Flags skipWallFlags = Surface.Flags.Virtual)
    {
        if (!surfaces.TryGetValue(cell, out var surfaceList))
        {
            return null;
        }

        Surface? bestSurface = null;
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

    public Vector2Int LimitMotion(Vector2Int motion, Vector2Int cell, Vector2 up, Surface.Flags skipWallFlags = Surface.Flags.Virtual, EffectorFlags effectorFlags = EffectorFlags.All, bool isFalling = false)
    {
        return LimitMotion(motion, cell, up, out _, out _, skipWallFlags, effectorFlags, isFalling);
    }

    public Vector2Int LimitMotion(Vector2Int motion, Vector2Int cell, Vector2 up, out Surface? impactedSurface, Surface.Flags skipWallFlags = Surface.Flags.Virtual, EffectorFlags effectorFlags = EffectorFlags.All, bool isFalling = false)
    {
        return LimitMotion(motion, cell, up, out impactedSurface, out _, skipWallFlags, effectorFlags, isFalling);
    }

    public Vector2Int LimitMotion(Vector2Int motion, Vector2Int cell, Vector2 up, out List<IEffector> traversedEffectors, Surface.Flags skipWallFlags = Surface.Flags.Virtual, EffectorFlags effectorFlags = EffectorFlags.All, bool isFalling = false)
    {
        return LimitMotion(motion, cell, up, out _, out traversedEffectors, skipWallFlags, effectorFlags, isFalling);
    }

    public Vector2Int LimitMotion(Vector2Int motion, Vector2Int cell, Vector2 up, out Surface? impactedSurface, out List<IEffector> traversedEffectors, Surface.Flags skipWallFlags = Surface.Flags.Virtual, EffectorFlags effectorFlags = EffectorFlags.All, bool isFalling = false)
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
            Surface? surf = GetSurface(current, -step, skipWallFlags: skipWallFlags);
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
        return DeregisterEffectorInCell(effector, effector.Cell);
    }
    public bool DeregisterEffectorInCell(IEffector effector, Vector2Int cell)
    {
        if (effector == null) return false;
        if (!effectors.TryGetValue(cell, out var cellEffectors))
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

    HashSet<CellEntity> registeredEntities = new HashSet<CellEntity>();
    public void RegisterEntity(CellEntity entity) { registeredEntities.Add(entity); }
    public bool DeregisterEntity(CellEntity entity) { return registeredEntities.Remove(entity); }

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

    private int step = 0;
    public int Step => step;
    public void ExternalInvokeTimeStep()
    {
        timeFragment = 0;
        InvokeTimeStep();
    }
    private void InvokeTimeStep()
    {
        step++;
        OnTimeStep.Invoke();
    }

    public void CheckEntityCollisions()
    {
        // This has O(n^2) complexity, it can be optimized by using spatial storage methods
        CellEntity[] entities = registeredEntities.ToArray();
        for (int i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];
            if (!entity.IsPlaying()) continue;
            if (entity.EntityRadius == 0) continue; // we assume that if the radius is 0, it's not meant to collide
            for (int j = i + 1; j < entities.Length; j++)
            {
                var otherEntity = entities[j];
                if (!otherEntity.IsPlaying()) continue;
                if (entity == otherEntity) continue;
                if (otherEntity.EntityRadius == 0) continue;
                // we're using spherical models

                float reachDistance = entity.EntityRadius + otherEntity.EntityRadius;
                Vector3 delta = entity.Center - otherEntity.Center;
                if (delta.sqrMagnitude >= reachDistance * reachDistance) continue;

                entity.Die();
                otherEntity.Die();
            }
        }
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
        Vector3 cameraFocusPoint = (GetCellCenter(bottomLeftCell) + GetCellCenter(topRightCell)) / 2;
        return cameraFocusPoint + Vector3.back * (requiredDistance + gridDepth / 2);
    }

    List<Surface>? borderSurfaceCache = null;
    const Surface.Flags borderSurfaceProperties = Surface.Flags.Fatal | Surface.Flags.IgnoreRotationOnLanding;
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

    public void SuppressUpdates() {
        SuppressSurfaceUpdates = true;
        SuppressEffectorUpdates = true;
    }
    public void RestoreUpdates() {
        SuppressSurfaceUpdates = false;
        SuppressEffectorUpdates = false;
    }
    private int updateSuppressionDepth = 0;
    public void EnterUpdateSuppressionBlock()
    {
        if (updateSuppressionDepth++ == 0)
        {
            SuppressUpdates();
        }
    }
    public void ExitUpdateSuppressionBlock()
    {
        if (--updateSuppressionDepth == 0)
        {
            RestoreUpdates();
        }
    }

    public bool Raycast(Ray mouseRay, out Vector3 point)
    {
        Plane plane = new(Vector3.back, transform.position + Vector3.back * (GridDepth / 2));
        if (plane.Raycast(mouseRay, out float enter))
        {
            point = mouseRay.GetPoint(enter);
            point.z = transform.position.z;
            return true;
        }

        point = Vector3.zero;
        return false;
    }

    public IEnumerable<Obstacle> GetAllObstacles()
    {
        foreach (var kvp in fixedObstacles)
        {
            yield return kvp.Value;
        }
        foreach (var obstacle in dynamicObstacles)
        {
            yield return obstacle;
        }
    }
    public IEnumerable<Obstacle> GetDynamicObstacles()
    {
        foreach (var obstacle in dynamicObstacles) yield return obstacle;
    }

    public IObstaclePlacementResult CheckValidObstacles(Obstacle? lastChanged)
    {
        foreach (var obstacle in GetAllObstacles())
        {
            //Debug.Log($"Checking side-effect validity of {obstacle}");
            if (obstacle == lastChanged) continue;
            if (obstacle.IsDragging) continue;
            var result = obstacle.CheckValidity();
            if (!result.Success) return result;
        }
        return new ObstaclePlacementSuccess(lastChanged);
    }
    public IObstaclePlacementResult CheckAllValidObstacles()
    {
        IEnumerable<IObstaclePlacementResult> Checks()
        {
            foreach (var obstacle in GetAllObstacles())
            {
                if (obstacle.IsDragging) continue;
                var check = obstacle.CheckValidity();
                if (!check.Success) yield return check;
            }
        };
        return IObstaclePlacementResult.Combine(Checks());
    }

    // This is called when exiting Setup or Play mode.
    public void HandleReset()
    {
        timeFragment = 0;
        TimeSpeed = 1;
        foreach (var resettable in GetComponentsInChildren<IResettable>())
        {
            resettable.HandleReset();
        }
    }

    // This is called when entering Setup mode from Edit mode
    internal void HandleInit()
    {
        
    }

    /* ****************** *
     *         UX         *
     * ****************** */
    [Header("UX")]
    [SerializeField] private GameObject cellHighlightPrefab;
    [SerializeField] private AnimationCurve highlightEntryScaleCurve = new();
    [SerializeField] private AnimationCurve highlightFadeoutScaleCurve = new();
    [SerializeField] private float highlightEntryTime = 0.5f;
    [SerializeField] private float highlightFadeoutTime = 0.25f;
    public void HighlightCell(Vector2Int cell, float duration)
    {
        HighlightPosition(GetCellCenter(cell), duration);
    }
    public void HighlightPosition(Vector3 position, float duration)
    {
        HighlightPosition(position, Quaternion.identity, Vector3.one, duration);
    }
    public void HighlightTransform(Transform t, float duration)
    {
        HighlightPosition(t.position, t.rotation, Vector3.one, duration, t);
    }
    public void HighlightPosition(Vector3 position, Quaternion rotation, Vector3 scale, float duration, Transform? parent = null)
    {
        StartCoroutine(HighlightPositionCoroutine(position, rotation, scale, duration, parent));
    }

    private IEnumerator HighlightPositionCoroutine(Vector3 position, Quaternion rotation, Vector3 scale, float duration, Transform? parent)
    {
        GameObject go = Instantiate(cellHighlightPrefab, parent);
        go.transform.SetPositionAndRotation(position, rotation);
        scale.Scale(go.transform.localScale);
        go.transform.localScale = scale;
        float entryT = highlightEntryTime;
        float fadeoutT = highlightFadeoutTime;
        float totalRefTime = entryT + fadeoutT;
        if (duration < totalRefTime)
        {
            entryT *= duration / totalRefTime;
            fadeoutT *= duration / totalRefTime;
        }
        yield return AnimateAppearance(go, entryT);
        yield return new WaitForSeconds(duration - entryT - fadeoutT);
        yield return AnimateDisappearance(go, fadeoutT, true);
    }

    private class AnimationData
    {
        public Coroutine Coroutine;
        public Vector3 InitialScale;
        public AnimationData(Vector3 initialScale, Coroutine coroutine)
        {
            this.Coroutine = coroutine;
            this.InitialScale = initialScale;
        }
    }
    private readonly Dictionary<GameObject, AnimationData> scaleAnimationMap = new();
    public Coroutine AnimateAppearance(GameObject target, float duration)
    {
        return RegisterAnimation(target, () => StartCoroutine(AnimateAppearanceCoro(target, duration)));
    }
    public Coroutine AnimateDisappearance(GameObject target, float duration, bool destroyAtEnd = true)
    {
        return RegisterAnimation(target, () => StartCoroutine(AnimateDisappearanceCoro(target, duration, destroyAtEnd)));
    }
    private Coroutine RegisterAnimation(GameObject target, Func<Coroutine> nextCoroutineFactory)
    {
        if (scaleAnimationMap.TryGetValue(target, out var anim))
        {
            StopCoroutine(anim.Coroutine);
            target.transform.localScale = anim.InitialScale;
            anim.Coroutine = nextCoroutineFactory();
            return anim.Coroutine;
        }
        else
        {
            // note that target.transform.localScale is instantly modified after calling the factory
            // since these coroutines affect the scale synchronously.
            // We're relying on LTR evaluation here, otherwise we'd have to store the initial scale before.
            AnimationData data = new(target.transform.localScale, nextCoroutineFactory());
            scaleAnimationMap[target] = data;
            return data.Coroutine;
        }
    }
    private void DeregisterAnimation(GameObject target)
    {
        scaleAnimationMap.Remove(target);
    }
    private IEnumerator AnimateAppearanceCoro(GameObject target, float duration)
    {
        float timer = 0;
        Vector3 scale = target.transform.localScale;
        while (timer < duration)
        {
            if (target == null) yield break;
            float t = timer / duration;
            float scaleFactor = highlightEntryScaleCurve.Evaluate(t);
            target.transform.localScale = scale * scaleFactor;
            yield return new WaitForEndOfFrame();
            timer += Time.deltaTime;
        }
        DeregisterAnimation(target);
    }
    private IEnumerator AnimateDisappearanceCoro(GameObject target, float duration, bool destroyAtEnd = true)
    {
        float timer = 0;
        Vector3 scale = target.transform.localScale;
        while (timer < duration)
        {
            float t = timer / duration;
            float scaleFactor = highlightFadeoutScaleCurve.Evaluate(t);
            if (target == null) yield break;
            target.transform.localScale = scale * scaleFactor;
            yield return new WaitForEndOfFrame();
            timer += Time.deltaTime;
        }
        DeregisterAnimation(target);
        if (destroyAtEnd) { Destroy(target); }
    }

    public GameObject HighlightTransform(Transform target)
    {
        GameObject highlight = Instantiate(cellHighlightPrefab, target);
        highlight.transform.SetPositionAndRotation(highlight.transform.position, highlight.transform.rotation);
        AnimateAppearance(highlight, highlightEntryTime);
        return highlight;
    }
    public void EndHighlight(GameObject highlight)
    {
        AnimateDisappearance(highlight, highlightFadeoutTime, true);
    }

    private List<Obstacle> uncheckedObstacles = new();
    public void ScheduleSideEffectCheck(Obstacle newObstacle)
    {
        uncheckedObstacles.Add(newObstacle);
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