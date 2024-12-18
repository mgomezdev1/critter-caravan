using Extensions;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Networking.ServerAPI;

#nullable enable
namespace Networking
{
    public static partial class ServerAPI
    {
        public static class Levels
        {
            public enum SortCriterion
            {
                CreationTime = 0,
                UpdateTime = 1,
                Likes = 2,
                Completions = 3,
                Name = 4
            }
            public static string SortCriterionToString(SortCriterion criterion)
            {
                return criterion switch
                {
                    SortCriterion.CreationTime => "upload_date",
                    SortCriterion.UpdateTime => "modified_date",
                    SortCriterion.Likes => "likes",
                    SortCriterion.Completions => "completions",
                    SortCriterion.Name => "name",
                    _ => throw new NotImplementedException($"Unknown sort criterion conversion to string {criterion}.")
                };
            }

            public struct QueryParams
            {
                public int PerPage { get; set; }
                public string? Category { get; set; }
                public string? Author { get; set; }
                public bool SortAscending { get; set; }
                public SortCriterion SortCriterion { get; set; }
                public VerificationLevel? MinVerificationLevel { get; set; }
                public VerificationLevel? MaxVerificationLevel { get; set; }

                public override readonly string ToString()
                {
                    List<string> segments = new();

                    if (SortAscending != default) segments.Add("sort_asc=true");
                    if (SortCriterion != default) segments.Add($"sort={SortCriterionToString(SortCriterion)}");
                    if (Author != default) segments.Add($"author={Author}");
                    if (Category != default) segments.Add($"category={Category}");
                    if (PerPage != default) segments.Add($"per_page={PerPage}");
                    if (MinVerificationLevel.HasValue) segments.Add($"min_verification={(int)MinVerificationLevel.Value}");
                    if (MaxVerificationLevel.HasValue) segments.Add($"max_verification={(int)MaxVerificationLevel.Value}");

                    if (segments.Count == 0)
                    {
                        return "";
                    }
                    return $"?{string.Join('&', segments)}";
                }
            }

            public static async IAsyncEnumerable<ILevel> FetchLevels(QueryParams? queryParams = null)
            {
                var qParams = queryParams.HasValue ? queryParams.Value.ToString() : "";
                var endpoint = $"/levels{qParams}";
                var rawPages = await GetAsync<Paginated<JObject>>(endpoint);

                await foreach (var rawLevel in rawPages)
                {
                    if (rawLevel.ContainsKey("world"))
                    {
                        Level? levelResult = rawLevel.ToObject<Level>();
                        if (levelResult != null && levelResult.WorldData.obstacles != null)
                        {
                            yield return levelResult;
                            continue;
                        }
                    }

                    AsyncLevel? asyncLevelResult = rawLevel.ToObject<AsyncLevel>();
                    if (asyncLevelResult != null && asyncLevelResult.LevelId != null)
                    {
                        yield return asyncLevelResult;
                        continue;
                    }

                    throw new ServerAPIInvalidFormatException<ILevel>(HttpVerb.Get, endpoint, rawLevel.ToString());
                }
            }

            public static readonly int LEVEL_FETCH_REQUEST_SIZE = 50;
            public static async IAsyncEnumerable<LevelCompletionResult> FetchAllCompletions()
            {
                await foreach (var result in await InternalCompletionFetch("stats/levels"))
                {
                    result.MarkDownloaded();
                    yield return result;
                }
            }
            public static async IAsyncEnumerable<LevelCompletionResult> FetchCompletions(IEnumerable<string> levelIds)
            {
                foreach (var idSet in levelIds.InSetsOf(LEVEL_FETCH_REQUEST_SIZE))
                {
                    string uri = GetLevelCompletionFetchUri(idSet);
                    await foreach (var result in await InternalCompletionFetch(uri))
                    {
                        result.MarkDownloaded();
                        yield return result;
                    }
                }
            }

            private static string GetLevelCompletionFetchUri(IEnumerable<string> levelIds)
            {
                return $"stats/levels?{string.Join('&', levelIds.Select(s => $"levelId={s}"))}";
            }
            private static async Task<Paginated<LevelCompletionResult>> InternalCompletionFetch(string uri)
            {
                return await GetAsync<Paginated<LevelCompletionResult>>(uri);
            }

            public static async Task<LevelCompletionResult> UploadCompletion(LevelCompletionResult completion)
            {
                var result = await PostAsync<LevelCompletionResult>($"stats/levels", completion);
                result.MarkDownloaded();
                return result;
            }
            public static async Task<IEnumerable<LevelCompletionResult>> UploadCompletions(IEnumerable<LevelCompletionResult> completions)
            {
                var result = await PostAsync<LevelCompletionResult[]>($"stats/levels/bulk", completions.ToArray());
                DateTime downloadTime = DateTime.Now;
                foreach (var item in result)
                {
                    item.MarkDownloaded(downloadTime);
                }
                return result;
            }
        }
    }
}