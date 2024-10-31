using System.Collections;
using UnityEngine;

public class WorldManager : MonoBehaviour
{
    private static WorldManager instance;
    public static WorldManager Instance
    {
        get { 
            if (instance == null)
            {
                instance = FindAnyObjectByType<WorldManager>();
            }
            return instance;
        }
    }

    [SerializeField] private CritterColor[] colors; 

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        instance = this;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    /// <summary>
    /// <para>Obtains a list of all valid critter colors that have been registered with the world manager.</para>
    /// <para>Do not modify the returned array. If you must make modifications, create a copy first.</para>
    /// </summary>
    /// <returns>The array of registered critter colors</returns>
    public CritterColor[] GetColors()
    {
        return colors;
    }
    public CritterColor GetColorByName(string name)
    {
        string nameLower = name.ToLower();
        foreach (var c in colors)
        {
            if (c.name.ToLower() == nameLower) return c;
        }
        return null;
    }
}
