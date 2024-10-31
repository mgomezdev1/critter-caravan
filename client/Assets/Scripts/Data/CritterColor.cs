using UnityEngine;

[CreateAssetMenu(fileName = "CritterColor", menuName = "Scriptable Objects/Critter Color")]
public class CritterColor : ScriptableObject
{
    public Color color = Color.gray;
    public string colorName = "Unnamed Color";

    public override bool Equals(object other)
    {
        if (other is not CritterColor c) { return false; }
        return c.colorName == colorName;
    }
    public override int GetHashCode()
    {
        return colorName.GetHashCode();
    }
}
