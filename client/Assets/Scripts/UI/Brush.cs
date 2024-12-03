#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class Brush
{
    public abstract void Activate();
    public abstract void Deactivate();

    public abstract void HandleClick();
    public abstract void HandleDrag();
    public abstract void HandleDragEnd();
    public abstract void HandleSelect(ObstacleData selected);

    protected readonly UIManager ui;
    public Brush(UIManager ui)
    {
        this.ui = ui;
    }
}

public class DragBrush : Brush
{
    public DragBrush(UIManager ui) : base(ui)
    {
    }

    public override void Activate()
    {
        // do nothing
    }

    public override void Deactivate()
    {
        WorldManager.Instance.DropHeldObstacle();
    }

    public override void HandleClick()
    {
        WorldManager.Instance.TryGrabObstacleAtPointer();
    }

    public override void HandleDrag()
    {
        Obstacle? held = WorldManager.Instance.HeldObstacle;
        if (held != null)
        {
            held.DragToRay(WorldManager.Instance.GetCameraRay(), true);
        }
    }

    public override void HandleDragEnd()
    {
        WorldManager.Instance.DropHeldObstacle();
    }

    public override void HandleSelect(ObstacleData selected)
    {
        if (selected == null) { return; }
        WorldManager.Instance.RaycastCameraRay(out Vector3 position);
        WorldManager.Instance.SpawnAndGrabObstacle(selected, position, ui.ObstacleSpawnRotation);
        ui.PretendDragStartedInWorldSpace();
    }
}

public class PlaceBrush : Brush
{
    private DateTime lastPlaceAttemptTime;
    private Vector2Int lastCell;
    public const float PLACEMENT_ATTEMPT_COOLDOWN = 0.25f;

    public PlaceBrush(UIManager ui) : base(ui)
    {
        lastPlaceAttemptTime = DateTime.Now;
    }

    public override void Activate()
    {
        // do nothing
    }

    public override void Deactivate()
    {
        ui.BrushObstacle = null;
    }

    public override void HandleClick()
    {
        TryPlaceObstacleAtCursor(true);
    }

    public override void HandleDrag()
    {
        TryPlaceObstacleAtCursor(false);
    }

    private void TryPlaceObstacleAtCursor(bool highlightOnError = true)
    {
        ObstacleData? obstacleData = ui.BrushObstacle;
        if (obstacleData == null) {
            return;
        }
        if (!WorldManager.Instance.RaycastCameraRay(out Vector3 castPos)) { return; }
        var cell = WorldManager.Instance.World.GetCell(castPos);
        if (!CheckForCooldown(cell)) return;
        var position = WorldManager.Instance.World.GetCellCenter(cell);

        var placementResult = WorldManager.Instance.TrySpawnAndPlaceObstacle(obstacleData, position, ui.ObstacleSpawnRotation);

        if (placementResult.Success)
        {
            WorldManager.Instance.World.AnimateAppearance(placementResult.Obstacle!.gameObject, 0.5f);
        }
        else if(highlightOnError)
        {
            WorldManager.Instance.HandlePlacementError(placementResult, false, false);
        }
    }

    public bool CheckForCooldown(Vector2Int cell)
    {
        if (lastCell != cell)
        {
            lastCell = cell;
            return true;
        }
        DateTime now = DateTime.Now;
        if ((now - lastPlaceAttemptTime).TotalSeconds > PLACEMENT_ATTEMPT_COOLDOWN)
        {
            lastPlaceAttemptTime = DateTime.Now;
            return true;
        }
        return false;
    }

    public override void HandleDragEnd()
    {
        // do nothing
    }

    public override void HandleSelect(ObstacleData selected)
    {
        ui.BrushObstacle = selected;
    }
}

public class DeleteBrush : Brush
{
    private readonly HashSet<string> obstacleNameFilter = new();

    public DeleteBrush(UIManager ui) : base(ui)
    {
    }

    public override void Activate()
    {
        obstacleNameFilter.Clear();
        ui.CompendiumView.ClearHighlights();
    }

    public override void Deactivate()
    {
        // do nothing
    }

    public override void HandleClick()
    {
        TryDeleteAtPointer();
    }

    public override void HandleDrag()
    {
        TryDeleteAtPointer();
    }

    private void TryDeleteAtPointer()
    {
        Obstacle? obstacle = WorldManager.Instance.GetObstacleAtPointer();
        if (obstacle == null) return;

        if (obstacleNameFilter.Count == 0 || obstacleNameFilter.Contains(obstacle.ObstacleName))
        {
            obstacle.Delete();
        }
    }

    public override void HandleDragEnd()
    {
        // do nothing
    }

    public override void HandleSelect(ObstacleData selected)
    {
        bool present = ui.CompendiumView.ToggleHighlight(selected);
        string obstacleName = selected.GetPrefabObstacleName();
        if (present)
        {
            obstacleNameFilter.Add(obstacleName);
        }
        else
        {
            obstacleNameFilter.Remove(obstacleName);
        }
    }
}