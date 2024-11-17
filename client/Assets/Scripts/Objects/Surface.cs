using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

#nullable enable
public class Surface
{
    private Vector2Int cell;
    public Vector2Int Cell => cell;
    public Vector2 normal;
    public Vector2 offset;
    /// <summary>
    /// Methods that search surfaces should return the eligible surface with the lowest priority value.
    /// </summary>
    public int priority;
    public IEnumerable<IEffector> effectors = new List<Effector>();

    [Flags]
    public enum Properties {
        None = 0,
        SuppressFall = 1,
        Virtual = 2,
        Fatal = 4,
        IgnoreRotationOnLanding = 8,
        Transparent = 16
    }
    public Properties properties;

    public Surface(Vector2Int cell, Vector2 normal, Vector2? offset = null, Properties surfaceProps = Properties.None, int priority = 0)
    {
        this.cell = cell;
        this.normal = normal;
        this.offset = offset ?? Vector2.zero;
        this.properties = surfaceProps;
        this.priority = priority;
    }

    public Vector3 GetStandingPosition(GridWorld grid)
    {
        return grid.CellCenter(cell) + (Vector3)offset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetStandingAngle()
    {
        return Mathf.Rad2Deg * Mathf.Atan2(normal.y, normal.x);
    }
    public Quaternion GetStandingRotation(Quaternion entityRotation, bool flipOrientation = false)
    {
        return MathLib.GetRotationFromAngle(GetStandingAngle(), MathLib.RightSidePointsAway(entityRotation) ^ flipOrientation);
    }
    public Quaternion GetStandingRotation(bool rightSidePointsAway)
    {
        return MathLib.GetRotationFromAngle(GetStandingAngle(), rightSidePointsAway);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasFlag(Properties flags)
    {
        return properties.HasFlag(flags);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasAnyFlagOf(Properties flags)
    {
        return (properties & flags) > 0;
    }

    public void MoveSurface(Vector2Int newCell, GridWorld grid)
    {
        grid.DeregisterSurface(this);
        cell = newCell;
        grid.RegisterSurface(this);
    }

    public void DrawGizmos(GridWorld grid, int numLines = 7)
    {
        Gizmos.color = GetColorFromFlags();
        Vector3 root = GetStandingPosition(grid);
        Vector3 forward = Vector3.forward;
        Vector3 side = Vector3.Cross(normal, forward);
        side.Normalize();
        side *= grid.GridScale / 2;
        forward *= grid.GridDepth / 2;
        float lineGap = grid.GridDepth / (numLines - 1);
        for (float depth = -1; depth <= 1; depth += lineGap)
        {
            Vector3 center = root + depth * forward;
            Gizmos.DrawLine(center - side, center + side);
        }
        Gizmos.color = Color.red;
        Gizmos.DrawLine(grid.CellCenter(cell), root);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(root, root + (Vector3)normal);
    }

    public Color GetColorFromFlags()
    {
        return Surface.GetColorFromFlags(properties);
    }
    public static Color GetColorFromFlags(Properties flags)
    {
        // Main flags that override color entirely
        if (flags.HasFlag(Properties.Fatal))
        {
            return Color.red;
        }

        // Complex color interaction flags
        Color result = Color.green;
        if (flags.HasFlag(Properties.Virtual))
        {
            // Display virtual walls as purple rather than green
            result = new Color(0.5f, 0, 1);
        }
        if (flags.HasFlag(Properties.Transparent))
        {
            // Display transparent walls as cyan rather than green
            result = Color.cyan;
        }
        if (flags.HasFlag(Properties.SuppressFall))
        {
            // darken the existing color
            result = new Color(result.r / 2, result.g / 2, result.b / 2);
        }
        return result;
    }

    public override string ToString()
    {
        return $"C{cell}; N{normal}, P+{offset}";
    }
}