using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

#nullable enable
namespace Networking
{
    [Serializable]
    [JsonObject]
    public class Paginated<T> : IAsyncEnumerable<T>
    {
        [JsonProperty("total")]
        public int Total { get; set; }
        [JsonProperty("per_page")]
        public int PerPage { get; set; }
        [JsonProperty("current_page")]
        public int CurrentPage { get; set; }
        [JsonProperty("last_page")]
        public int PageCount { get; set; }
        [JsonProperty("first_page_url")]
        public string FirstPageUrl { get; set; }
        [JsonProperty("last_page_url")]
        public string LastPageUrl { get; set; }
        [JsonProperty("prev_page_url")]
        public string? PrevPageUrl { get; set; } = null;
        [JsonProperty("next_page_url")]
        public string? NextPageUrl { get; set; } = null;
        [JsonProperty("path")]
        public string Path { get; set; }
        [JsonProperty("from")]
        public int From { get; set; }
        [JsonProperty("to")]
        public int To { get; set; }

        [JsonProperty("data")]
        public T[] Data { get; set; }

        [JsonIgnore]
        public Paginated<T>? NextPage { get; protected set; } = null;

        public Paginated(int total, int perPage, int currentPage, int pageCount, string firstPageUrl, string lastPageUrl, string? prevPageUrl, string? nextPageUrl, string path, int from, int to, T[] data)
        {
            Total = total;
            PerPage = perPage;
            CurrentPage = currentPage;
            PageCount = pageCount;
            FirstPageUrl = firstPageUrl;
            LastPageUrl = lastPageUrl;
            PrevPageUrl = prevPageUrl;
            NextPageUrl = nextPageUrl;
            Path = path;
            From = from;
            To = to;
            Data = data;
        }

        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            foreach (var item in Data ?? Array.Empty<T>())
            {
                yield return item;
            }
            if (CurrentPage >= PageCount || NextPageUrl == null)
            {
                yield break;
            }

            NextPage ??= await ServerAPI.GetAsync<Paginated<T>>(NextPageUrl);

            await foreach (var item in NextPage)
            {
                yield return item;
            }
        }
    }
}