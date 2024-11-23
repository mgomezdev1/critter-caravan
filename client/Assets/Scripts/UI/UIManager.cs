using UnityEngine;
using UnityEngine.UIElements;

public struct ColorScore
{
    public int score;
    public int maxScore;
    public CritterColor color;
    public ColorScore(int score, int maxScore, CritterColor color)
    {
        this.score = score;
        this.maxScore = maxScore;
        this.color = color;
    }

    public static ColorScore operator +(ColorScore left, ColorScore right)
    {
        return new ColorScore(left.score + right.score, left.maxScore + right.maxScore, left.color);
    }
}

public class UIManager : MonoBehaviour
{
    [SerializeField] private UIDocument _document;

    private void Awake()
    {
        _document = GetComponent<UIDocument>();
    }

    private VisualElement BuildScoreVisualizer(ColorScore score)
    {
        Label label = new($"{score.score} / {score.maxScore}");
        label.style.color = score.color.color;
        return label;
    }

    public void SetScores(ColorScore[] scores)
    {
        VisualElement holder = _document.rootVisualElement.Q("ScoreHolder");
        foreach (var child in holder.Children())
        {
            holder.Remove(child);
        }
        foreach (var score in scores)
        {
            var component = BuildScoreVisualizer(score);
            holder.Add(component);
        }
    }
}
