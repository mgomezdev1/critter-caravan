using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class CritterMaterialColorUtil : CellBehaviour<CellElement>
{
    public List<MeshRenderer> meshes = new();

    public void UpdateColor()
    {
        foreach (var mesh in meshes)
        {
            foreach (var mat in mesh.materials)
            {
                mat.color = CellComponent.Color.WithAlpha(mat.color.a);
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

    private void Start()
    {
        UpdateColor();
    }
}
