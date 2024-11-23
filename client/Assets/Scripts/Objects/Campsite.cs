using UnityEngine;

public class Campsite : CellBehaviour<CellElement>, IEffector
{
    public EffectorFlags Flags => EffectorFlags.RequireSurface;

    public EffectResult OnEntityEnter(CellEntity entity)
    {
        if (entity.Color == CellComponent.Color)
        {
            entity.ReachGoal();
            WorldManager.Instance.AddScore(new ColorScore(1, 0, CellComponent.Color));
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
}
