using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CellElement))]
public class LineEmitter : MonoBehaviour
{
    [SerializeField] Vector2Int propagationVector = Vector2Int.up;
    [SerializeField] Vector2Int startOffset = Vector2Int.zero;
    [Tooltip("The maximum number of cells the emitter line can extend for. Use a negative value to indicate unlimited.")]
    [SerializeField] int maxPropagationDistance = -1;
    [SerializeField] Surface.Flags skipWallFlags;

    [SerializeField] bool propagateColor = true;
    [SerializeField] GameObject headPrefab;
    [SerializeField] GameObject bodyPrefab;
    [SerializeField] GameObject tailPrefab;


    private CellElement cellElement;
    private List<GameObject> spawnedObjects = new();
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        cellElement = GetComponent<CellElement>();
        RespawnLine();
        cellElement.World.OnSurfacesUpdated.AddListener(RespawnLineSafe);
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
            Destroy(go);
        }
    }

    public void RespawnLineSafe()
    {
        cellElement.World.SuppressSurfaceUpdates = true;
        RespawnLine();
        cellElement.World.SuppressSurfaceUpdates = false;
    }

    public void RespawnLine()
    {
        List<Vector2Int> cells = new();

        Vector2 rotatedPropVector = GetRotatedPropagationVector();
        Quaternion spawnRotation = GetSpawnRotation(rotatedPropVector);
        foreach (var cell in GetAffectedCells(MathLib.RoundVector(rotatedPropVector)))
        {
            cells.Add(cell);
        }

        for (int i = 0; i < cells.Count; ++i)
        {
            GameObject prefab = i == 0 ?
                headPrefab : i == cells.Count - 1 ?
                tailPrefab :
                bodyPrefab;

            GameObject newSpawnedInstance = Instantiate(prefab, transform);
            newSpawnedInstance.transform.position = cellElement.World.GetCellCenter(cells[i]);
            newSpawnedInstance.transform.rotation = spawnRotation;
            if (newSpawnedInstance.TryGetComponent(out CellElement spawnedCellElement))
            {
                spawnedCellElement.World = cellElement.World;
                if (propagateColor)
                {
                    spawnedCellElement.Color = cellElement.Color;
                }
            }
            spawnedObjects.Add(newSpawnedInstance);
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
        Vector2Int current = cellElement.Cell + rotatedStartOffset;

        int maxDistance = maxPropagationDistance;
        if (maxDistance < 0) { maxDistance = int.MaxValue; }

        while (maxDistance-- > 0)
        {
            if (!cellElement.World.IsCellValid(current)) { break; }
            yield return current;
            Vector2Int previous = current;
            current += cellElement.World.LimitMotion(correctedPropagationVector, current, Vector2.up, out Surface impactedSurface, skipWallFlags);
            if (impactedSurface != null || current == previous) { break; }
        }

    }

    private void OnDrawGizmosSelected()
    {
        if (cellElement == null)
            cellElement = GetComponent<CellElement>();

        Vector2 correctedForward = GetRotatedPropagationVector();
        Quaternion rotation = GetSpawnRotation(correctedForward);
        Vector3 dirDelta = rotation * Vector3.up;
        Gizmos.color = Color.blue;
        foreach (var cell in GetAffectedCells(MathLib.RoundVector(correctedForward)))
        {
            // Debug.Log($"Drawing line emitter spawn gizmo at {cell}");
            Vector3 pos = cellElement.World.GetCellCenter(cell);
            Gizmos.DrawLine(pos, pos + dirDelta);
        }
    }
}
