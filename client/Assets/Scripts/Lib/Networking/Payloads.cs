using Newtonsoft.Json;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

#nullable enable
namespace Networking
{
    public interface IPayload<T>
    {
        public string Serialize()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    public class SessionResponse : IPayload<SessionResponse>
    {
        [JsonProperty(propertyName: "user")]
        public User User { get; set; }
        [JsonProperty(propertyName: "token")]
        public string Token { get; set; }
        [JsonProperty(propertyName: "expires")]
        public DateTime ExpirationDate { get; set; }

        public SessionResponse(User user, string token, DateTime expirationDate)
        {
            User = user;
            Token = token;
            ExpirationDate = expirationDate;
        }
    }

    public class ClientLevel : IPayload<ClientLevel>
    {
        [JsonProperty("levelId")]
        public virtual string? MaybeLevelId { get; set; } = null;
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("private")]
        public bool Privacy { get; set; }
        [JsonProperty("thumbnail")]
        public string? Thumbnail { get; set; }
        [JsonProperty("category")]
        public string? Category { get; set; }
        // This property is managed by a special serializer
        [JsonProperty("world")]
        public WorldSaveData WorldData { get; set; }

        public ClientLevel(string name, bool privacy, string? thumbnail, string? category, WorldSaveData worldData, string? levelId = null)
        {
            MaybeLevelId = levelId;
            Name = name;
            Privacy = privacy;
            Thumbnail = thumbnail;
            Category = category;
            WorldData = worldData;
        }
    }

    public enum VerificationLevel
    {
        Unverified = 0,
        Verified = 1,
        Ranked = 2,
        Spotlight = 3,
        Official = 4
    }

    public interface ILevel
    {
        [JsonIgnore]
        public string LevelId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("private")]
        public bool Privacy { get; set; }
        [JsonProperty("category")]
        public string? Category { get; set; }
        [JsonProperty("authorId")]
        public string AuthorId { get; set; }
        [JsonProperty("uploadDate")]
        public DateTime Created { get; set; }
        [JsonProperty("modifiedDate")]
        public DateTime Updated { get; set; }
        [JsonProperty("reviews")]
        public ReviewData ReviewData { get; set; }
        [JsonProperty("verificationLevel")]
        public VerificationLevel VerificationLevel { get; set; }

        public Task<WorldSaveData> FetchWorldData();
        public Task<Sprite> GetThumbnail();

        public async Task<WorldSaveData> RefreshWorldData()
        {
            string levelId = LevelId;
            var refreshedData = await ServerAPI.GetAsync<Level>($"levels/{levelId}");
            Name = refreshedData.Name;
            Privacy = refreshedData.Privacy;
            Category = refreshedData.Category;
            AuthorId = refreshedData.AuthorId;
            Created = refreshedData.Created;
            Updated = refreshedData.Updated;
            ReviewData = refreshedData.ReviewData;

            return refreshedData.WorldData;
        }
    }

    [JsonObject]
    public class Level : ClientLevel, IPayload<ClientLevel>, ILevel
    {
        public Level(string levelId, string name, bool privacy, string? thumbnail, string? category, WorldSaveData worldData, string authorId, DateTime created, DateTime updated, ReviewData reviewData, VerificationLevel verificationLevel)
            : base(name, privacy, thumbnail, category, worldData, levelId)
        {
            AuthorId = authorId;
            Created = created;
            Updated = updated;
            ReviewData = reviewData;
            VerificationLevel = verificationLevel;
        }

        [JsonIgnore]
        public string LevelId { 
            get
            {
                return MaybeLevelId ?? throw new NullReferenceException($"Instance of {nameof(Level)} with a null LevelId");
            }
            set
            {
                MaybeLevelId = value;
            }
        }

        [JsonProperty("authorId")]
        public string AuthorId { get; set; }
        [JsonProperty("uploadDate")]
        public DateTime Created { get; set; }
        [JsonProperty("modifiedDate")]
        public DateTime Updated { get; set; }
        [JsonProperty("reviews")]
        public ReviewData ReviewData { get; set; }
        [JsonProperty("verificationLevel")]
        public VerificationLevel VerificationLevel { get; set; }

        public Task<WorldSaveData> FetchWorldData()
        {
            return Task.FromResult(WorldData);
        }

        public async Task<Sprite> GetThumbnail()
        {
            if (Thumbnail == null)
            {
                return AssetManager.DefaultLevelThumbnail;
            }
            return await AssetManager.GetSprite(Thumbnail);
        }
    }

    public class AsyncLevel: IPayload<AsyncLevel>, ILevel
    {
        [JsonProperty("levelId")]
        public string LevelId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("private")]
        public bool Privacy { get; set; }
        [JsonProperty("thumbnail")]
        public string? Thumbnail { get; set; }
        [JsonProperty("category")]
        public string? Category { get; set; }
        [JsonProperty("authorId")]
        public string AuthorId { get; set; }
        [JsonProperty("uploadDate")]
        public DateTime Created { get; set; }
        [JsonProperty("modifiedDate")]
        public DateTime Updated { get; set; }
        [JsonProperty("reviews")]
        public ReviewData ReviewData { get; set; }
        [JsonProperty("verificationLevel")]
        public VerificationLevel VerificationLevel { get; set; }

        protected WorldSaveData? CachedData { get; set; } = null;

        public AsyncLevel(string levelId, string name, bool privacy, string? thumbnail, string? category, string authorId, DateTime created, DateTime updated, ReviewData reviewData, VerificationLevel verificationLevel)
        {
            LevelId = levelId;
            Name = name;
            Privacy = privacy;
            Thumbnail = thumbnail;
            Category = category;
            AuthorId = authorId;
            Created = created;
            Updated = updated;
            ReviewData = reviewData;
            VerificationLevel = verificationLevel;
        }

        public async Task<WorldSaveData> FetchWorldData()
        {
            if (CachedData.HasValue) { return CachedData.Value; }

            CachedData = await ((ILevel)this).RefreshWorldData();

            return CachedData.Value;
        }

        public async Task<Sprite> GetThumbnail()
        {
            if (Thumbnail == null)
            {
                return AssetManager.DefaultLevelThumbnail;
            }
            return await AssetManager.GetSprite(Thumbnail);
        }
    }

    [JsonObject]
    public class ReviewData
    {
        [JsonProperty("likes")]
        public int Likes { get; set; } = 0;
        [JsonProperty("completions")]
        public int Completions { get; set; } = 0;
    }
}