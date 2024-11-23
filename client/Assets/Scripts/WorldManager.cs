using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

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
    [SerializeField] private UIManager ui;
    public UIManager UI => ui;

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

    /* ********************** *
     *     SCORE MANAGER      *
     * ********************** */
    private List<ColorScore> scores = new();
    public void ResetScores()
    {
        scores.Clear();
    }
    public ColorScore AddScore(ColorScore score)
    {
        for (int i = 0; i < scores.Count; i++)
        {
            if (scores[i].color == score.color)
            {
                scores[i] += score;
                return scores[i];
            }
        }

        scores.Add(score);

        return score;
    }
    public List<ColorScore> GetScores()
    {
        return scores;
    }
}
