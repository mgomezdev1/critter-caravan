using System;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;

#nullable enable
public abstract class EntityBrain : MonoBehaviour
{
    public abstract EntityMove GetNextMove(CellEntity entity);

    public MoveFlags GetMoveFlags(CellEntity entity, Surface? surface)
    {
        MoveFlags flags = MoveFlags.None;
        if (surface != null)
        {
            // handle death upon falling and touching fatal surfaces
            if (
            (surface.HasFlag(Surface.Properties.Fatal)) ||
                (entity.Falling && !surface.HasFlag(Surface.Properties.SuppressFall))
            )
            {
                flags |= MoveFlags.Fatal;
            }
        }
        return flags;
    }
}
