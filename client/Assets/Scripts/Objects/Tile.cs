using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CellElement))]
public class Tile : MonoBehaviour
{
    private CellElement cellElement;

    [SerializeField] private bool topWall = true;
    [SerializeField] private bool bottomWall = true;
    [SerializeField] private bool leftWall = true;
    [SerializeField] private bool rightWall = true;

    [Header("Wall Properties")]
    [SerializeField] private Surface.Flags surfaceProperties = Surface.Flags.None;
    [SerializeField] private bool separateSurfaceProperties = false;
    [SerializeField] private Surface.Flags topProperties = Surface.Flags.None;
    [SerializeField] private Surface.Flags bottomProperties = Surface.Flags.None;
    [SerializeField] private Surface.Flags leftProperties = Surface.Flags.None;
    [SerializeField] private Surface.Flags rightProperties = Surface.Flags.None;
    [SerializeField] private int surfacePriority = 0;

    private List<Surface> surfaces = null;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        cellElement = GetComponent<CellElement>();
        foreach (Surface surf in GetSurfaces())
        {
            cellElement.World.RegisterSurface(surf);
        }
    }

    public List<Surface> GetSurfaces(bool useCache = true)
    {
        if (useCache && surfaces != null)
        {
            return surfaces;
        }

        surfaces = new List<Surface>();
        float gridScale = cellElement.World.GridScale;
        foreach (var (vec, wall, props) in new Tuple<Vector2Int, bool, Surface.Flags>[] {
            new(Vector2Int.up, topWall, topProperties),
            new(Vector2Int.down, bottomWall, bottomProperties),
            new(Vector2Int.right, rightWall, rightProperties),
            new(Vector2Int.left, leftWall, leftProperties)
        })
        {
            if (wall)
            {
                Vector2 rotatedVector = MathLib.Vector2FromAngle(MathLib.AngleFromVector2(vec) + transform.rotation.eulerAngles.z);
                Vector2Int rotatedOffset = MathLib.RoundUnitVector(rotatedVector);
                Vector2Int wallCell = cellElement.Cell + rotatedOffset;

                var actualProps = separateSurfaceProperties ? props : surfaceProperties;
                Vector3 targetPosition = transform.position + (Vector3)(rotatedVector * (gridScale / 2));
                Vector3 cellCenterPosition = cellElement.World.GetCellCenter(wallCell);
                surfaces.Add(new Surface(wallCell, rotatedVector, targetPosition - cellCenterPosition, actualProps, surfacePriority));
            }
        }
        return surfaces;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnDestroy()
    {
        if (surfaces != null && cellElement != null)
        {
            foreach (var surface in surfaces)
            {
                cellElement.World.DeregisterSurface(surface);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (cellElement == null)
        {
            cellElement = GetComponent<CellElement>();
        }
        foreach (var surface in GetSurfaces(false))
        {
            surface.DrawGizmos(cellElement.World);
        }
    }
}
