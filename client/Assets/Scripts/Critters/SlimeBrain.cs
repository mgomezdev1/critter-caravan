using System;
using UnityEngine;

#nullable enable
public class SlimeBrain : EntityBrain
{
    [SerializeField] private float maxWalkSlopeAngle = 60;
    [SerializeField] private Vector2Int fallVelocity = Vector2Int.down;

    public override EntityMove GetNextMove(CellEntity entity)
    {
        Vector2Int motion;
        Surface? newStandSurface;
        // First, check for whether the entity should fall.
        // assuming it is standing on something
        if (entity.Falling)
        {
            motion = fallVelocity;
        }
        else
        {
            // We'll try to follow a point in the middle of the entity forward by a distance equaling one unit
            Vector2Int targetCell = entity.Grid.WorldToGrid(entity.Center + entity.transform.forward * entity.Grid.GridScale);
            motion = targetCell - entity.Cell;
        }

        // Use motion logic to move with physical constraints.
        motion = entity.Grid.LimitMotion(motion, entity.Cell, entity.transform.up, out Surface? hitSurface);
        Vector3 targetPosition;
        Quaternion targetRotation;
        bool shouldFlip = false;
        if (hitSurface != null)
        {
            Debug.Log($"Motion of {gameObject.name} blocked by surface {hitSurface}; set to {motion}");
            if (!entity.Falling && Vector2.Angle(hitSurface.normal, entity.StandingSurface!.normal) > maxWalkSlopeAngle)
            {
                // effectively would slam into a wall. Try to turn around instead.
                newStandSurface = entity.StandingSurface;
                shouldFlip = true;
            }
            else
            {
                // otherwise, try to latch onto the surface.
                newStandSurface = hitSurface;
            }
        }
        else
        {
            // Nothing was impacted, move in the direction of motion!
            // Then check if we would be able to stand somewhere
            newStandSurface = entity.Grid.GetSurface(entity.Cell + motion, transform.up);
        }

        // If we have a surface to stand on, simply try to get on it
        if (newStandSurface != null)
        {
            targetPosition = newStandSurface.GetStandingPosition(entity.Grid);
            // If we didn't walk onto the surface, but rather fell onto it, check whether to correct rotation.
            if (hitSurface == newStandSurface && hitSurface.HasFlag(Surface.Properties.IgnoreRotationOnLanding))
            {
                targetRotation = entity.GetRotationFromMotion(motion);
            }
            else
            {
                targetRotation = newStandSurface.GetStandingRotation(entity.transform.rotation, shouldFlip);
            }
        }
        else
        {
            targetPosition = entity.FootPosition + new Vector3(motion.x * entity.Grid.GridScale, motion.y * entity.Grid.GridScale, 0);
            targetRotation = entity.GetRotationFromMotion(motion);
        }

        // motion calculated, now calculate side effects
        MoveFlags flags = GetMoveFlags(entity, newStandSurface);

        EntityMove result = new(entity.Grid, targetPosition, targetRotation, flags)
        {
            surface = newStandSurface,
        };
        Debug.Log($"Calculated move for entity in {entity.Cell} to {result.MoveCell}.");
        Debug.Log($"Resulting move: {result}");
        return result;
    }
}
