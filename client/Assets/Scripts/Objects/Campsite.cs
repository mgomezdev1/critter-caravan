using UnityEngine;

public class Campsite : CellBehaviour<CellElement>, IEffector, IMovable
{
    public EffectorFlags Flags => EffectorFlags.RequireSurface;

    public EffectResult OnEntityEnter(CellEntity entity)
    {
        if (entity.Color == CellComponent.Color)
        {
            entity.ReachGoal();
            return new EffectResult() { executed = true };
        }
        return new EffectResult() { executed = false };
    }

    protected override void Awake()
    {
        base.Awake();
    }

    private void Start()
    {
        World.RegisterEffector(this);
    }

    public void AfterMove(Vector2Int originCell, Quaternion originRotation, Vector2Int targetCell, Quaternion targetRotation)
    {
        World.DeregisterEffectorInCell(this, originCell);
        World.RegisterEffector(this);
    }
}
