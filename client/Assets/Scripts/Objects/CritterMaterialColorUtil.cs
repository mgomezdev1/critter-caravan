using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class CritterMaterialColorUtil : MonoBehaviour
{
    public CritterColor color;
    public List<MeshRenderer> meshes = new();

    public void SetColor()
    {
        SetColor(color);
    }
    public void SetColor(CritterColor color)
    {
        foreach (var mesh in meshes)
        {
            foreach (var mat in mesh.materials)
            {
                mat.color = color.WithAlpha(mat.color.a);
            }
        }
    }

    public void AutodetectMeshes()
    {
        foreach (var childMesh in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (meshes.Contains(childMesh)) continue;
            meshes.Add(childMesh);
        }
    }
}
