using Networking;
using System;
using System.Threading.Tasks;
using UnityEngine;

#nullable enable
[CreateAssetMenu(fileName = "Level", menuName = "Scriptable Objects/Level")]
public class InbuiltLevel : ScriptableObject, ILevel
{
    public string LevelId { get => levelId; set => levelId = value; }
    public string Name { get => levelName; set => levelName = value; }
    public bool Privacy { get => privacy; set => privacy = value; }
    public string? Category { get => category; set => category = value; }
    public string AuthorId { get => authorId; set => authorId = value; }
    public DateTime Created { get => created; set => created = value; }
    public DateTime Updated { get => updated; set => updated = value; }
    public ReviewData ReviewData { get => reviewData; set => reviewData = value; }
    public VerificationLevel VerificationLevel { get => verificationLevel; set => verificationLevel = value; }

    [SerializeField] private string levelId;
    [SerializeField] private string levelName;
    [SerializeField] private bool privacy = false;
    [SerializeField] private string? category = string.Empty;
    [SerializeField] private string authorId = "1";
    [SerializeField] private DateTime created = DateTime.Now;
    [SerializeField] private DateTime updated = DateTime.Now;
    [SerializeField] private ReviewData reviewData = new();
    [SerializeField] private VerificationLevel verificationLevel = VerificationLevel.Official;
    [SerializeField][TextArea] private string rawWorldData;

    [SerializeField] private Sprite previewSprite;

    public Task<WorldSaveData> FetchWorldData()
    {
        return Task.FromResult(WorldSaveData.Deserialize(rawWorldData, out _));
    }

    public Task<Sprite> GetThumbnail()
    {
        return Task.FromResult(previewSprite);
    }
}
