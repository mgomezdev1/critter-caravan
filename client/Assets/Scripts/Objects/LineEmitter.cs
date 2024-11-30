using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.UI;

public class LineEmitter : CellBehaviour<Obstacle>, IMovable
{
    [SerializeField] Vector2Int propagationVector = Vector2Int.up;
    [SerializeField] Vector2Int startOffset = Vector2Int.zero;
    [Tooltip("The maximum number of cells the emitter line can extend for. Use a negative value to indicate unlimited.")]
    [SerializeField] int maxPropagationDistance = -1;
    [SerializeField] bool checkAccessToStartCell = true;
    [SerializeField] Surface.Flags skipWallFlags;
    [SerializeField] EffectorFlags relevantEffectorFlags = EffectorFlags.None;

    [SerializeField] bool propagateColor = true;
    [SerializeField] GameObject headPrefab;
    [SerializeField] GameObject bodyPrefab;
    [SerializeField] GameObject tailPrefab;

    private List<GameObject> spawnedObjects = new();

    protected override void Awake()
    {
        base.Awake();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        RespawnLine();
        World.OnSurfacesUpdated.AddListener(RespawnLineSafe);
        CellComponent.OnDragStart.AddListener(DestroySpawnedObjects);
        // no need to hook up OnDragEnd because IMovable's AfterMove handles a broader case.
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnDestroy()
    {
        DestroySpawnedObjects();
    }

    public void DestroySpawnedObjects()
    {
        foreach (GameObject go in spawnedObjects)
        {
            DestroySpawnedObject(go);
        }
        spawnedObjects.Clear();
    }
    public void DestroySpawnedObject(GameObject go)
    {
        if (go.TryGetComponent(out Obstacle obstacle))
        {
            obstacle.Delete();
        }
        else
        {
            Destroy(go);
        }
    }

    public void RespawnLineSafe()
    {
        World.SuppressUpdates();
        RespawnLine();
        World.RestoreUpdates();
    }

    public void RespawnLine(bool tryReuseObjects = true)
    {
        if (!tryReuseObjects)
        {
            DestroySpawnedObjects();
        }

        // If we are dragging, consider that no cells are reachable
        if (CellComponent.IsDragging)
        {
            DestroySpawnedObjects();
            return;
        }

        List<Vector2Int> cells = new();

        Vector2 rotatedPropVector = GetRotatedPropagationVector();
        Quaternion spawnRotation = GetSpawnRotation(rotatedPropVector);
        foreach (var cell in GetAffectedCells(MathLib.RoundVector(rotatedPropVector)))
        {
            cells.Add(cell);
        }

        for (int i = 0; i < cells.Count; ++i)
        {
            GameObject target;
            bool spawned = false;
            if (i < spawnedObjects.Count)
            {
                target = spawnedObjects[i];
                target.transform.parent = transform;
            }
            else
            {
                GameObject prefab =
                i == 0 ? headPrefab :
                i == cells.Count - 1 ? tailPrefab :
                bodyPrefab;

                target = Instantiate(prefab, transform);
                spawnedObjects.Add(target);
                spawned = true;
            }

            Vector3 lastPos = target.transform.position;
            if (target.TryGetComponent(out CellElement spawnedCellElement))
            {
                spawnedCellElement.Initialize(World);
                spawnedCellElement.Generated = true;
                if (propagateColor)
                {
                    spawnedCellElement.Color = CellComponent.Color;
                }

                spawnedCellElement.MoveTo(cells[i], spawnRotation);
            }
            else
            {
                target.transform.SetPositionAndRotation(World.GetCellCenter(cells[i]), spawnRotation);
            }

            // only animate if we moved the object or created it anew
            if (spawned || Vector3.Distance(lastPos, target.transform.position) > 0.1f) { 
                World.AnimateAppearance(target, 0.5f); 
            }
        }

        while (spawnedObjects.Count > cells.Count)
        {
            // remove from the end for efficient O(1) removal rather than O(n), assuming ArrayList
            DestroySpawnedObject(spawnedObjects[^1]);
            spawnedObjects.RemoveAt(spawnedObjects.Count - 1);
        }
    }
    public Vector2 GetRotatedPropagationVector()
    {
        return MathLib.RotateVector2(propagationVector, transform.rotation, false);
    }
    public Quaternion GetSpawnRotation(Vector2 correctedPropagationVector)
    {
        Vector3 upVector = correctedPropagationVector;
        // In practice, this should be colinear with (-upVector.y, upVector.x, 0)
        Vector3 forwardVector = Vector3.Cross(upVector, Vector3.forward);
        return Quaternion.LookRotation(forwardVector, upVector);
    }
    public IEnumerable<Vector2Int> GetAffectedCells(Vector2Int correctedPropagationVector)
    {
        if (correctedPropagationVector == Vector2Int.zero)
        {
            Debug.LogWarning($"Propagation vector of line emitter {gameObject.name} is 0. It will never spawn anything.");
            yield break;
        }

        Vector2Int rotatedStartOffset = MathLib.RoundVector(MathLib.RotateVector2(startOffset, transform.rotation, false));
        Vector2Int current = Cell + rotatedStartOffset;

        if (checkAccessToStartCell)
        {
            Vector2Int limitedOffset = World.LimitMotion(rotatedStartOffset, Cell, transform.rotation * Vector2.up, out Surface? impactedSurface, skipWallFlags, relevantEffectorFlags);
            if (limitedOffset != rotatedStartOffset || impactedSurface != null)
            {
                Debug.Log($"LineEmitter {gameObject.name} failed to reach startCell {current} from {Cell}. Offset: {limitedOffset}/{rotatedStartOffset}. Surface: {impactedSurface}");
                yield break;
            }
        }

        int maxDistance = maxPropagationDistance;
        if (maxDistance < 0) { maxDistance = int.MaxValue; }

        while (maxDistance-- > 0)
        {
            if (!World.IsCellValid(current)) { break; }
            yield return current;
            Vector2Int previous = current;
            current += World.LimitMotion(correctedPropagationVector, current, Vector2.up, out Surface impactedSurface, skipWallFlags, relevantEffectorFlags);
            if (impactedSurface != null || current == previous) { break; }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 correctedForward = GetRotatedPropagationVector();
        Quaternion rotation = GetSpawnRotation(correctedForward);
        Vector3 dirDelta = rotation * Vector3.up;
        Gizmos.color = Color.blue;
        foreach (var cell in GetAffectedCells(MathLib.RoundVector(correctedForward)))
        {
            // Debug.Log($"Drawing line emitter spawn gizmo at {cell}");
            Vector3 pos = World.GetCellCenter(cell);
            Gizmos.DrawLine(pos, pos + dirDelta);
        }
    }

    public void AfterMove(Vector2Int originCell, Quaternion originRotation, Vector2Int targetCell, Quaternion targetRotation)
    {
        RespawnLineSafe();
    }
}
