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
    public HashSet<IEffector> effectors = new();

    [Flags]
    public enum Flags {
        None = 0,
        SuppressFall = 1,
        Virtual = 2,
        Fatal = 4,
        IgnoreRotationOnLanding = 8,
        Transparent = 16,
        All = 0x7fffffff
    }
    public Flags flags;

    public Surface(Vector2Int cell, Vector2 normal, Vector2? offset = null, Flags flags = Flags.None, int priority = 0)
    {
        this.cell = cell;
        this.normal = normal.normalized;
        this.offset = offset ?? Vector2.zero;
        this.flags = flags;
        this.priority = priority;
    }

    public Vector3 GetStandingPosition(GridWorld grid, float height = 0)
    {
        return grid.GetCellCenter(cell) + (Vector3)offset + (height * (Vector3)normal);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetStandingAngle()
    {
        return Mathf.Rad2Deg * Mathf.Atan2(normal.y, normal.x);
    }
    public Quaternion GetStandingEntityRotation(Quaternion entityRotation, bool flipOrientation = false)
    {
        return MathLib.GetRotationFromUpAngle(GetStandingAngle(), MathLib.RightSidePointsAway(entityRotation) ^ flipOrientation);
    }
    public Quaternion GetStandingEntityRotation(bool rightSidePointsAway)
    {
        return MathLib.GetRotationFromUpAngle(GetStandingAngle(), rightSidePointsAway);
    }
    public Quaternion GetStandingObstacleRotation()
    {
        return Quaternion.LookRotation(Vector3.forward, normal);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasFlag(Flags flags)
    {
        return this.flags.HasFlag(flags);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasAnyFlagOf(Flags flags)
    {
        if (flags == Flags.All) return true;
        return (this.flags & flags) > 0;
    }

    public void MoveTo(Vector2Int newCell, GridWorld grid)
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
        Gizmos.DrawLine(grid.GetCellCenter(cell), root);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(root, root + (Vector3)normal);
    }

    public Color GetColorFromFlags()
    {
        return Surface.GetColorFromFlags(flags);
    }
    public static Color GetColorFromFlags(Flags flags)
    {
        // Main flags that override color entirely
        if (flags.HasFlag(Flags.Fatal))
        {
            return Color.red;
        }

        // Complex color interaction flags
        Color result = Color.green;
        if (flags.HasFlag(Flags.Virtual))
        {
            // Display virtual walls as purple rather than green
            result = new Color(0.5f, 0, 1);
        }
        if (flags.HasFlag(Flags.Transparent))
        {
            // Display transparent walls as cyan rather than green
            result = Color.cyan;
        }
        if (flags.HasFlag(Flags.SuppressFall))
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