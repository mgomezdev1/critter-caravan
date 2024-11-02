using UnityEngine;

[RequireComponent (typeof(CellElement))]
public class Spawner : MonoBehaviour
{
    [SerializeField] private GameObject spawnObject;
    [SerializeField] private Vector3 spawnOffset;
    [SerializeField] private Vector2Int startMotion;
    [SerializeField] private float startRotation;
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
        if (newObject.TryGetComponent(out CellElement spawnedCellElement))
        {
            if (inheritColor)
            {
                spawnedCellElement.Color = cellElement.Color;
            }

            if (spawnedCellElement is CellEntity spawnedCellEntity)
            {
                spawnedCellEntity.Motion = startMotion;
            }            
        }
        newObject.transform.SetPositionAndRotation(transform.position + spawnOffset, Quaternion.Euler(0, 0, startRotation));
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 startPosition = transform.position + spawnOffset;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(startPosition, startPosition + new Vector3(startMotion.x, startMotion.y, 0));
        float startRotationRadians = startRotation * Mathf.Deg2Rad;
        Vector3 rotatedUp = new Vector3(Mathf.Sin(startRotationRadians), Mathf.Cos(startRotationRadians), 0);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(startPosition, startPosition + rotatedUp);
        Gizmos.DrawWireSphere(startPosition + rotatedUp, 0.3f);
    }
}
