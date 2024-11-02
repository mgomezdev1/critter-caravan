using UnityEngine;

public class EffectResult
{
    public EffectResult() { }
}

public interface IEffector
{
    public EffectResult OnEntityEnter(CellEntity entity);
    public EffectResult OnEntityExit(CellEntity entity);
}

public class Effector : MonoBehaviour
{
    [SerializeField] private bool stopFall;

}