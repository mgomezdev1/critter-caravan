using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using System.Runtime.CompilerServices;

#nullable enable
namespace Networking
{
    public static partial class ServerAPI
    {
        public static string BASE_URL => Environment.GetEnvironmentVariable("BASE_URL") ?? "https://127.0.0.1/";
        public static string BASE_URL_API => Environment.GetEnvironmentVariable("BASE_URL_API") ?? "https://127.0.0.1:4000/api/";
        public static int TIMEOUT { get; set; } = 5;

        public static async Task<string?> TryPostAsync(string endpoint, string jsonPayload)
        {
            try
            {
                return await PostAsync(endpoint, jsonPayload);
            }
            catch
            {
                return null;
            }
        }

        public static async Task<string> PostAsync(string endpoint, string jsonPayload)
        {
            using UnityWebRequest request = UnityWebRequest.Post(BASE_URL_API + endpoint, jsonPayload, "application/json");
            await PreprocessRequest(request);

            await request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                return request.downloadHandler.text;
            }
            else
            {
                string? responseBody = request.downloadHandler?.text;
                throw new ServerAPIException(HttpVerb.Post, endpoint, request.responseCode, request.error, responseBody);
            }
        }

        public static async Task<string?> TryGetAsync(string endpoint)
        {
            try
            {
                return await GetAsync(endpoint);
            }
            catch
            {
                return null;
            }
        }

        public static async Task<string> GetAsync(string endpoint)
        {
            using UnityWebRequest request = UnityWebRequest.Get(BASE_URL_API + endpoint);
            await PreprocessRequest(request);

            await request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                return request.downloadHandler.text;
            }
            else
            {
                string? responseBody = request.downloadHandler?.text;
                throw new ServerAPIException(HttpVerb.Get, endpoint, request.responseCode, request.error, responseBody);
            }
        }

        public static async Task<string?> TryDeleteAsync(string endpoint)
        {
            try
            {
                return await DeleteAsync(endpoint);
            }
            catch
            {
                return null;
            }
        }
        public static async Task<string> DeleteAsync(string endpoint)
        {
            using UnityWebRequest request = UnityWebRequest.Delete(BASE_URL_API + endpoint);
            await PreprocessRequest(request);

            await request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                return request.downloadHandler.text;
            }
            else
            {
                string? responseBody = request.downloadHandler?.text;
                throw new ServerAPIException(HttpVerb.Get, endpoint, request.responseCode, request.error, responseBody);
            }
        }

        public static async Task<T> GetAsync<T>(string endpoint)
        {
            string response = await GetAsync(endpoint);
            return JsonConvert.DeserializeObject<T>(response)
                ?? throw new ServerAPIInvalidFormatException<T>(HttpVerb.Get, endpoint, response);
        }
        public static async Task<T> PostAsync<T>(string endpoint, string jsonPayload)
        {
            string response = await PostAsync(endpoint, jsonPayload);
            return JsonConvert.DeserializeObject<T>(response)
                ?? throw new ServerAPIInvalidFormatException<T>(HttpVerb.Post, endpoint, response);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async Task<T> PostAsync<T>(string endpoint, object payload)
        {
            return await PostAsync<T>(endpoint, JsonConvert.SerializeObject(payload));
        }

        private static async Task<bool> PreprocessRequest(UnityWebRequest request)
        {
            request.timeout = TIMEOUT;
            request.SetRequestHeader("Accept", "application/json");
            return await PopulateSessionHeaders(request);
        }

        public static async Task<bool> PopulateSessionHeaders(UnityWebRequest request)
        {
            if (SessionManager.ShouldRefreshToken())
            {
                await SessionManager.RefreshTokenAsync();
            }

            if (SessionManager.AuthToken != null)
            {
                request.SetRequestHeader("Authorization", "Bearer " + SessionManager.AuthToken);
                return true;
            }
            return false;
        }

        public static void ShowServerFormErrors(ServerAggregateValidationResult validationResult, Dictionary<string, DataField> fields, Label? globalErrorLabel, bool clearPreviousErrors = true, bool treatUnexpectedFieldNamesAsGlobal = false)
        {
            if (clearPreviousErrors)
            {
                foreach (var field in fields.Values)
                {
                    field.ErrorText = string.Empty;
                }
                if (globalErrorLabel != null)
                {
                    globalErrorLabel.text = string.Empty;
                    globalErrorLabel.style.display = DisplayStyle.None;
                }
            }
            foreach (var fieldValidation in validationResult.Fields)
            {
                if (fieldValidation.Value.Success) continue;
                if (fields.TryGetValue(fieldValidation.Key, out var field))
                {
                    field.ErrorText = fieldValidation.Value.Error;
                    continue;
                }
                if (fieldValidation.Key == "global" || treatUnexpectedFieldNamesAsGlobal)
                {
                    if (globalErrorLabel == null)
                    {
                        Debug.LogWarning($"Received global error in field validation {fieldValidation.Value.Error}, but no global errors were expected.");
                        continue;
                    }
                    string newGlobalText = globalErrorLabel.text;
                    if (!string.IsNullOrEmpty(newGlobalText)) { newGlobalText += "<br>"; }
                    newGlobalText += fieldValidation.Value.Error;
                    globalErrorLabel.text = newGlobalText;
                    // show label
                    globalErrorLabel.style.display = DisplayStyle.Flex;
                }
                throw new InternalServerAPIException($"Found unexpected field key \"{fieldValidation.Key}\" while showing validation error. Expected fields are {string.Join(", ", fields.Keys)}.");
            }
        }
    }
}