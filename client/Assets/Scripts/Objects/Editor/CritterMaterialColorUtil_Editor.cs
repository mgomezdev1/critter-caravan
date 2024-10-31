using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(CritterMaterialColorUtil))]
public class CritterMaterialColorUtil_Editor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Autoassign Meshes"))
        {
            HandleAutoassignMeshesClick();
        }
        /*if (GUILayout.Button("Assign Color"))
        {
            HandleAssignColorClick();
        }*/
    }

    public override VisualElement CreateInspectorGUI()
    {
        /*
        var tree = base.CreateInspectorGUI();
        var meshAssignButton = new Button(HandleAutoassignMeshesClick)
        {
            text = "Autoassign Meshes"
        };
        tree.Add(meshAssignButton);
        var setColorButton = new Button(HandleAssignColor)
        {
            text = "Set Color"
        };
        tree.Add(setColorButton);
        return tree;
        */
        return base.CreateInspectorGUI();
    }

    public void HandleAutoassignMeshesClick()
    {
        var script = (CritterMaterialColorUtil)target;
        script.AutodetectMeshes();
    }

    public void HandleAssignColorClick()
    {
        var script = (CritterMaterialColorUtil)target;
        script.SetColor();
    }

}
