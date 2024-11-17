using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#nullable enable
[Flags]
public enum MoveFlags
{
    None = 0,
    Ethereal = 1 << 0,
    Fatal = 1 << 1,
}

public class EntityMove
{
    public GridWorld world;
    public Vector3 movePosition;
    public Quaternion moveRotation;
    public MoveFlags moveFlags;
    public float verticalSnapPoint = 0f;

    public Surface? surface;
    public Surface.Properties ignoreWallFlags = Surface.Properties.None;

    public Vector2Int MoveCell => surface?.Cell ?? world.WorldToGrid(movePosition + moveRotation * Vector3.up * 0.5f);

    public EntityMove(GridWorld world, Transform moveToTransform, MoveFlags flags = MoveFlags.None)
    {
        this.world = world;
        this.movePosition = moveToTransform.position;
        this.moveRotation = moveToTransform.rotation;
        this.moveFlags = flags;
    }

    public EntityMove(GridWorld world, Vector3 movePosition, Quaternion moveRotation, MoveFlags flags = MoveFlags.None)
    {
        this.world = world;
        this.movePosition = movePosition;
        this.moveRotation = moveRotation;
        this.moveFlags = flags;
    }

    public EntityMove(GridWorld world, Surface surface, bool rightSidePointsAway, MoveFlags flags = MoveFlags.None)
    {
        this.world = world;
        this.surface = surface;
        this.movePosition = surface.GetStandingPosition(world);
        this.moveRotation = surface.GetStandingRotation(rightSidePointsAway);
        this.moveFlags = flags;
    }

    public EntityMove(GridWorld world, Surface surface, Quaternion entityRotation, MoveFlags flags = MoveFlags.None)
    {
        this.world = world;
        this.surface = surface;
        this.movePosition = surface.GetStandingPosition(world);
        this.moveRotation = surface.GetStandingRotation(entityRotation);
        this.moveFlags = flags;
    }

    public void InferSurface(float maxNormalDeltaDegrees = 60, Surface.Properties skipWallFlags = Surface.Properties.Virtual, bool adjustPositionAndRotation = true)
    {
        surface = this.world.GetSurface(MoveCell, moveRotation * Vector2.up, maxNormalDeltaDegrees, skipWallFlags);
        if (surface != null && adjustPositionAndRotation)
        {
            this.movePosition = surface.GetStandingPosition(world);
            this.moveRotation = surface.GetStandingRotation(this.moveRotation);
        }
    }

    public IEnumerable<IEffector> GetEffectors()
    {
        List<IEffector> result = new();
        if (surface != null)
        {
            result.AddRange(surface.effectors);
        }
        result.AddRange(world.GetEffectors(MoveCell));
        return result;
    }

    public bool Reached(CellEntity entity)
    {
        return Reached(entity.transform.position, entity.transform.rotation);
    }
    public bool Reached(Transform transform)
    {
        return Reached(transform.position, transform.rotation);
    }
    const float POSITION_EPSILON = 0.025f; // meters
    const float ROTATION_EPSILON = 2.5f; // degrees
    public bool Reached(Vector3 position, Quaternion rotation)
    {
        return (movePosition - position).sqrMagnitude < (POSITION_EPSILON * POSITION_EPSILON)
            && Quaternion.Angle(moveRotation, rotation) < ROTATION_EPSILON;
    }

    public bool IsMoveValid(Vector3 sourcePosition)
    {
        return IsMoveValid(world.WorldToGrid(sourcePosition));
    }
    public bool IsMoveValid(Vector2Int sourceCell)
    {
        if (!moveFlags.HasFlag(MoveFlags.Ethereal))
        {
            // could have issues when moving to liminar surfaces with no reference to the surface
            Vector2Int targetCell = MoveCell;
            Vector2Int motion = targetCell - sourceCell;
            Vector2Int limitedMotion = world.LimitMotion(motion, targetCell, moveRotation * Vector2.up, ignoreWallFlags);
            if (limitedMotion != motion)
            {
                return false;
            }
        }
        return true;
    }

    public void AdjustMovePosition(CellEntity entity)
    {
        this.movePosition = Vector3.Lerp(
            entity.GetAdjustedStandingPosition(this.movePosition, this.moveRotation * Vector3.up),
            this.movePosition,
            verticalSnapPoint
        );
    }

    public void ReadjustForRotation(Quaternion rotation, float height)
    {
        float effectiveHeight = (1 - verticalSnapPoint) * height;
        Vector3 center = movePosition + moveRotation * Vector3.up * effectiveHeight;
        moveRotation = rotation;
        movePosition = center + rotation * Vector3.down * effectiveHeight;
    }

    public override string ToString()
    {
        string surfaceString = surface?.ToString() ?? "no surface";
        return $"Move to {movePosition} (snap {verticalSnapPoint * 50}%) w/ rotation {moveRotation.eulerAngles}. ({surfaceString}).";
    }
}