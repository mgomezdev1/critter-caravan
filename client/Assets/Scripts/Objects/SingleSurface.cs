using System;
using System.Collections.Generic;
using UnityEngine;

#nullable enable
#pragma warning disable CS8618
public class SingleSurface : CellBehaviour<CellElement>, IMovable
{
    [SerializeField] [Range(0, 360)] private float wallAngle;
    [SerializeField] private Vector2 wallOffset = Vector2.zero;
    [SerializeField] private bool makeWallOffsetRelative = true;
    [SerializeField] private Vector2Int cellOffset = Vector2Int.zero;

    [Header("Wall Properties")]
    [SerializeField] private Surface.Flags surfaceProperties = Surface.Flags.None;
    [SerializeField] private int surfacePriority = 0;
    [SerializeField] private List<Effector> effectors = new();

    private Surface? surface = null;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        World.RegisterSurface(GetSurface());
    }

    public Surface GetSurface(bool useCache = true)
    {
        if (useCache && surface != null)
        {
            return surface;
        }
        if (surface != null)
        {
            // ensure to avoid leaving trash surfaces registered
            World.DeregisterSurface(surface);
        }

        Vector2Int rotatedCellOffset = MathLib.RoundVector(transform.rotation * (Vector2)cellOffset);
        Vector2 rotatedNormal = transform.rotation * MathLib.Vector2FromAngle(wallAngle);
        Vector2 rotatedWallOffset = transform.rotation * wallOffset;
        Vector2Int finalCell = Cell + rotatedCellOffset;

        surface = new Surface(finalCell, rotatedNormal, ProcessOffset(finalCell, rotatedWallOffset), surfaceProperties, surfacePriority)
        {
            effectors = new HashSet<IEffector>(effectors)
        };

        return surface;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnDestroy()
    {
        if (surface != null)
        {
            World.DeregisterSurface(surface);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
        {
            GetSurface(false).DrawGizmos(World);
        }
    }

    private Vector2 ProcessOffset(Vector2Int targetCell, Vector2 rawOffset)
    {
        Vector3 targetPosition = transform.position + (Vector3)(rawOffset * (makeWallOffsetRelative ? World.GridScale / 2 : 1));
        Vector3 cellCenterPosition = World.GetCellCenter(targetCell);
        return targetPosition - cellCenterPosition;
    }

    public void AfterMove(Vector2Int originCell, Quaternion originRotation, Vector2Int targetCell, Quaternion targetRotation)
    {
        Vector2Int newCell = targetCell + MathLib.RoundVector(targetRotation * (Vector2)cellOffset);
        if (surface != null)
        {
            surface.MoveTo(newCell, World);
        }
        else
        {
            surface = GetSurface(false);
            World.RegisterSurface(surface);
        }
        surface.normal = targetRotation * MathLib.Vector2FromAngle(wallAngle);
        surface.offset = ProcessOffset(newCell, targetRotation * wallOffset);
    }
}
