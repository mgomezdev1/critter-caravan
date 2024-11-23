using System;
using System.Collections.Generic;
using UnityEngine;

#nullable enable
public class SingleSurface : CellBehaviour<CellElement>
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
    private float Rotation => transform.rotation.eulerAngles.z;

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

        float gridScale = World.GridScale;

        Vector2Int rotatedCellOffset = MathLib.RoundVector(MathLib.RotateVector2(cellOffset, Rotation));
        Vector2 rotatedNormal = MathLib.Vector2FromAngle(Rotation + wallAngle);
        Vector2 rotatedWallOffset = MathLib.RotateVector2(wallOffset, Rotation);

        Vector3 targetPosition = transform.position + (Vector3)(rotatedWallOffset * (makeWallOffsetRelative ? gridScale / 2 : 1));
        Vector3 cellCenterPosition = World.GetCellCenter(Cell + rotatedCellOffset);
        surface = new Surface(Cell + rotatedCellOffset, rotatedNormal, targetPosition - cellCenterPosition, surfaceProperties, surfacePriority)
        {
            effectors = effectors
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
        GetSurface(false).DrawGizmos(World);
    }
}
