using System;
using UnityEngine;

#nullable enable
public class SlimeBrain : EntityBrain
{
    [SerializeField] private Vector2Int fallVelocity = Vector2Int.down;
    [SerializeField] private Vector2Int fallAcceleration = Vector2Int.zero;

    public override EntityMove GetNextMove(CellEntity entity)
    {
        EntityMove result = GetNextMoveInternal(entity);
        Debug.Log($"Calculated move for entity in {entity.Cell} to {result.GetCell()}.");
        Debug.Log($"Resulting move: {result}");
        return result;
    }

    private EntityMove GetNextMoveInternal(CellEntity entity)
    {
        Vector2Int motion;
        EntityMove? result;
        // First, check for whether the entity should fall.
        // assuming it is standing on something
        if (entity.StandingSurface == null)
        {
            motion = fallVelocity + entity.FallHeight * fallAcceleration;
            result = GetMoveFromMotion(motion, entity, out _, MoveFlags.None, 60);
            if (result != null) return result;
        }
        else
        {
            // We'll try to follow a point in the middle of the entity forward by a distance equaling one unit
            result = GetForwardMove(entity, out _);
            if (result != null) return result;

            // If this fails, that means we are running into something, so we'll try to turn around
            Quaternion newRotation = Quaternion.LookRotation(-entity.transform.forward, entity.transform.up);
            return new EntityMove(entity, entity.transform.position, newRotation, entity.StandingSurface);
        }

        Surface? surface = entity.World.GetSurface(entity.Cell, entity.transform.rotation * Vector2.up, 60, entity.IntangibleWallFlags);
        Debug.LogError($"GetNextMove on SlimeBrain failed to find valid motion when standing on no surface. May be due to a prior step failing to set the correct surface. Entity can currently stand on surface: {surface}");
        return new EntityMove(entity, entity.transform.position, entity.transform.rotation, surface);
    }
}
