using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

#nullable enable
[Flags]
public enum MoveFlags
{
    None = 0,
    Ethereal = 1 << 0,
    Fatal = 1 << 1,
    FaceMotion = 1 << 2
}

public class Move
{
    public readonly Vector3 movePosition;
    public readonly Quaternion moveRotation;
    public readonly MoveFlags flags;
    public readonly float verticalAnchor;
    public readonly float maxFloorSnapAngle;

    public Move? nextMove = null;

    public Move(Vector3 movePosition, Quaternion moveRotation, MoveFlags flags = MoveFlags.None, float verticalAnchor = 0.0f, float maxFloorSnapAngle = 180)
    {
        this.movePosition = movePosition;
        this.moveRotation = moveRotation;
        this.flags = flags;
        this.verticalAnchor = verticalAnchor;
        this.maxFloorSnapAngle = maxFloorSnapAngle;
    }

    public Move(Transform transform, MoveFlags flags = MoveFlags.None, float verticalAnchor = 0f, float maxFloorSnapAngle = 180) :
        this(transform.position, transform.rotation, flags, verticalAnchor, maxFloorSnapAngle)
    { }

    public Vector3 RelativePoint(float verticalReference, float height)
    {
        float heightFromPosition = (verticalReference - verticalAnchor) * height;
        return movePosition + (moveRotation * Vector3.up) * heightFromPosition;
    }

    public Vector2Int GetCell(CellEntity entity)
    {
        Vector3 middlePoint = RelativePoint(0.5f, entity.Height);
        return entity.World.GetCell(middlePoint);
    }
    public Vector2Int GetCell(float entityHeight, GridWorld world)
    {
        Vector3 middlePoint = RelativePoint(0.5f, entityHeight);
        return world.GetCell(middlePoint);
    }
    public EntityMove? Process(CellEntity entity)
    {
        return Process(entity, out _);
    }
    public EntityMove? Process(CellEntity entity, out Surface? blockingSurface)
    {
        Vector3 position = movePosition;
        Quaternion rotation = moveRotation;
        MoveFlags resultFlags = flags;
        Surface? resultSurface = null;
        float resultAnchor = verticalAnchor;
        blockingSurface = null;

        GridWorld world = entity.World;

        if (flags.HasFlag(MoveFlags.Ethereal))
        {
            // Ethereal motion should not try to snap to surfaces.
            // But it will try to be standing on one if possible
            resultSurface = world.GetSurface(world.GetCell(position), rotation * Vector2.up, maxFloorSnapAngle, entity.IntangibleWallFlags);
        }
        else
        {
            // Non-ethereal motion must handle collision detection
            Vector2Int targetCell = GetCell(entity);
            Vector2Int motion = targetCell - entity.Cell;
            Vector2 normal = rotation * Vector2.up;
            // TODO: handle effectors
            Vector2Int limitedMotion = world.LimitMotion(motion, entity.Cell, normal, out blockingSurface, entity.IntangibleWallFlags, isFalling: entity.Falling);

            if (blockingSurface != null)
            {
                // If we can land on that surface (the slope isn't excessive), get on the surface.
                if (Vector2.Angle(normal, blockingSurface.normal) < entity.MaxWalkSlopeAngle)
                {
                    resultSurface = blockingSurface;
                }
                else // otherwise, our motion is halted.
                {
                    // if we are airborne, we'll land on this surface.
                    if (entity.StandingSurface == null)
                    {
                        resultSurface = blockingSurface;
                        targetCell = resultSurface.Cell;
                    }
                    else if (limitedMotion == Vector2Int.zero)
                    {
                        // If we can't move at all and we are not airborne, there is no valid move.
                        return null;
                    }
                    else
                    {
                        // If we aren't airborne, but we still get a fraction of the motion, take that fraction
                        position = entity.Center + ((Vector3)(Vector2)limitedMotion * world.GridScale);
                        resultAnchor = 0.5f;
                        targetCell = entity.Cell + limitedMotion;
                    }
                }

                if (blockingSurface.HasFlag(Surface.Flags.Fatal))
                {
                    resultFlags |= MoveFlags.Fatal;
                }
            }

            // at this point, collision has been handled.
            // if we haven't snapped to a surface yet, check if we should
            resultSurface ??= world.GetSurface(targetCell, normal, maxFloorSnapAngle, entity.IntangibleWallFlags);
            if (resultSurface != null)
            {
                position = resultSurface.GetStandingPosition(world);
                rotation = resultSurface.GetStandingRotation(rotation);
                resultAnchor = 0;
            }

            if (resultSurface != null && (resultSurface.HasFlag(Surface.Flags.Fatal) ||
               (!resultSurface.HasFlag(Surface.Flags.SuppressFall) && entity.FallHeight > entity.MaxFallHeight)
            ))
            {
                resultFlags |= MoveFlags.Fatal;
            }
        }

        // Normalize position to work with entity
        if (resultAnchor < 1)
        {
            // if resultAnchor = 1, reference is already the entity head
            // if resultAnchor = 0, the reference needs to be shifted from the feet to the target head position
            position = Vector3.Lerp(
                entity.GetAdjustedStandingPosition(position, rotation * Vector3.up),
                position,
                resultAnchor
            );
            // verticalAnchor is useless from this point forward
        }

        if (flags.HasFlag(MoveFlags.FaceMotion))
        {
            Vector3 finalMotion = position - entity.transform.position;
            if (resultSurface == null)
            {
                // if there's no surface, ensure entity is looking directly in the direction of motion
                rotation = Quaternion.LookRotation(finalMotion, entity.transform.up);
            }
            else if (Vector3.Angle(entity.transform.forward, finalMotion) > 180)
            {
                // If entity is looking in the opposite direction of motion, flip rotation 180 degrees around its vertical axis.
                rotation = Quaternion.LookRotation(rotation * Vector3.back, rotation * Vector3.up);
            }
        }

        return new EntityMove(entity, position, rotation, resultSurface, resultFlags, nextMove);
    }

    public override string ToString()
    {
        return $"Move to {movePosition} (snap {verticalAnchor * 100}%) w/ rotation {moveRotation.eulerAngles}.";
    }
}

public class EntityMove
{
    public GridWorld World => entity.World;
    public readonly Vector3 movePosition;
    public readonly Quaternion moveRotation;
    public readonly MoveFlags flags;
    public readonly CellEntity entity;
    public readonly Surface? surface;
    public readonly bool isValid;

    public readonly Move? nextMove = null;

    public Vector2Int GetCell()
    {
        Vector3 middlePoint = movePosition + moveRotation * Vector3.up * (0.5f * entity.Height);
        return entity.World.GetCell(middlePoint);
    }

    public EntityMove(CellEntity entity, Vector3 movePosition, Quaternion moveRotation, Surface? surface, MoveFlags flags = MoveFlags.None, Move? nextMove = null)
    {
        this.movePosition = movePosition;
        this.moveRotation = moveRotation;
        this.flags = flags;
        this.entity = entity;
        this.surface = surface;
        this.nextMove = nextMove;
    }

    public IEnumerable<IEffector> GetEffectors()
    {
        List<IEffector> result = new();
        if (surface != null)
        {
            result.AddRange(surface.effectors);
        }
        result.AddRange(World.GetEffectors(GetCell()));
        return result;
    }

    public bool Reached()
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

    public override string ToString()
    {
        string surfaceString = surface?.ToString() ?? "no surface";
        return $"{base.ToString()} ({surfaceString}).";
    }
}