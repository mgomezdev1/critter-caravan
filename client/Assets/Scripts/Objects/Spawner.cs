using UnityEditor;
using UnityEngine;

[RequireComponent (typeof(CellElement))]
public class Spawner : MonoBehaviour
{
    [SerializeField] private GameObject spawnObject;
    [SerializeField] private Vector3 spawnOffset;
    [SerializeField] private float startRotation;
    [Tooltip("Whether the spawned entity's 'right-hand side' should point away from the camera or towards it. This effectively flips the spawned object.")]
    [SerializeField] private bool rightSidePointsAway = false;
    [SerializeField] private float[] spawnTimes;

    [SerializeField] private bool inheritColor;

    private CellElement cellElement;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        cellElement = GetComponent<CellElement>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Spawn()
    {
        GameObject newObject = Instantiate(spawnObject);
        Vector3 spawnPosition = transform.position + spawnOffset;
        Quaternion spawnRotation = MathLib.GetRotationFromAngle(startRotation, rightSidePointsAway);

        if (newObject.TryGetComponent(out CellElement spawnedCellElement))
        {
            if (inheritColor)
            {
                spawnedCellElement.Color = cellElement.Color;
            }

            if (spawnedCellElement is CellEntity spawnedCellEntity)
            {
                // readjust entities so that they *stand* on the spawn point
                spawnPosition = spawnedCellEntity.GetAdjustedStandingPosition(spawnPosition, spawnRotation * Vector3.up);
            }
        }
        newObject.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 startPosition = transform.position + spawnOffset;
        DrawingLib.DrawCritterGizmo(startPosition, startRotation, rightSidePointsAway);
    }
}
