using Newtonsoft.Json;
using System;

[Serializable]
public class LevelSaveData
{
    public string authorId;
    public string levelId;
    public string levelName;
    public string thumbnailUrl;
    public WorldSaveData worldData;

    public static LevelSaveData FromJson(string json)
    {
        return JsonConvert.DeserializeObject<LevelSaveData>(json);
    }
    public string ToJson() 
    { 
        return JsonConvert.SerializeObject(this); 
    }
}