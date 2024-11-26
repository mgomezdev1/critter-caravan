using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

public class Spawner : CellBehaviour<CellElement>, IResettable
{
    [SerializeField] private GameObject spawnObject;
    [SerializeField] private Transform spawnRoot;
    [SerializeField] private List<int> spawnDelays = new();
    [SerializeField] private TMP_Text spawnCountLabel;
    [SerializeField] private bool isCritterSpawner = true;
    [SerializeField] private int spawnIterations = 1;
    [SerializeField] private int prewarmSteps = 0;
    private Queue<int> remainingSpawnDelays = new();
    private int remainingSpawnIterations = 0;
    public int TotalSpawnCount => spawnIterations < 0 ? int.MaxValue : spawnDelays.Count * spawnIterations;
    public int RemainingSpawnCount => spawnIterations < 0 ? int.MaxValue : remainingSpawnDelays.Count + spawnDelays.Count * remainingSpawnIterations;

    [SerializeField] private bool inheritColor;

    protected override void Awake()
    {
        base.Awake();
        remainingSpawnIterations = spawnIterations;
        elapsedDelay = prewarmSteps;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        int totalSpawnCount = TotalSpawnCount;
        SetSpawnLabel(totalSpawnCount);
        World.OnTimeStep.AddListener(OnTimeStep);
        HandleReset();
    }

    public void HandleReset()
    {
        int totalSpawnCount = TotalSpawnCount;
        if (isCritterSpawner)
        {
            WorldManager.Instance.AddScore(new(0, totalSpawnCount, CellComponent.Color));
        }
        remainingSpawnIterations = spawnIterations;
        remainingSpawnDelays = new();
        elapsedDelay = prewarmSteps;
        SetSpawnLabel(totalSpawnCount);
    }

    int elapsedDelay = 0;
    public void OnTimeStep()
    {
        // we will at most refill the spawn delay queue once per time step.
        if (remainingSpawnIterations != 0 && remainingSpawnDelays.Count == 0)
        {
            remainingSpawnIterations--;
            foreach (var d in spawnDelays)
            {
                remainingSpawnDelays.Enqueue(d);
            }
            Debug.Log($"Refilled Spawner {gameObject.name} queue with {remainingSpawnDelays.Count} elements, {remainingSpawnIterations} iterations left.");
        }

        elapsedDelay++;
        while (remainingSpawnDelays.Count > 0)
        {
            if (elapsedDelay >= remainingSpawnDelays.Peek())
            {
                elapsedDelay -= remainingSpawnDelays.Dequeue();
                Spawn();
                SetSpawnLabel(RemainingSpawnCount);
                continue;
            }
            break;
        }
    }

    public void SetSpawnLabel(int count)
    {
        if (count >= int.MaxValue)
        {
            spawnCountLabel.text = "inf";
            return;
        }
        spawnCountLabel.text = count.ToString();
    }

    public void Spawn()
    {
        Transform parent = World.transform;
        GameObject newObject = Instantiate(spawnObject, parent);
        Vector3 spawnPosition = spawnRoot.position;
        Quaternion spawnRotation = spawnRoot.rotation;

        if (newObject.TryGetComponent(out CellElement spawnedCellElement))
        {
            spawnedCellElement.Initialize(World);
            if (inheritColor)
            {
                spawnedCellElement.Color = CellComponent.Color;
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
        DrawingLib.DrawCritterGizmo(spawnRoot);
    }
}
