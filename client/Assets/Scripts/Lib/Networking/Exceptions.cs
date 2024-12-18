using Newtonsoft.Json;
using System.Collections.Generic;
using System;

#nullable enable
namespace Networking
{
    [System.Serializable]
    public class ServerAPIException : System.Exception
    {
        private static string GetMessage(HttpVerb verb, string endpoint, long responseCode, string error, string? responseBody)
        {
            string bodySnippet = responseBody != null ? $" Response body: {responseBody}" : "";
            return $"{verb.ToString().ToUpper()} {endpoint} failed with code {responseCode}: {error}.{bodySnippet}";
        }

        public readonly HttpVerb Verb;
        public readonly string Endpoint;
        public readonly long ResponseCode;
        public readonly string Error;
        public readonly string? ResponseBody;

        public ServerAPIException(HttpVerb verb, string endpoint, long responseCode, string error, string? responseBody = null)
            : base(GetMessage(verb, endpoint, responseCode, error, responseBody))
        {
            Verb = verb;
            Endpoint = endpoint;
            ResponseCode = responseCode;
            Error = error;
            ResponseBody = responseBody;
        }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        protected ServerAPIException(
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }


    [Serializable]
    public class InternalServerAPIException : Exception
    {
        public InternalServerAPIException() { }
        public InternalServerAPIException(string message) : base(message) { }
        public InternalServerAPIException(string message, Exception inner) : base(message, inner) { }
        protected InternalServerAPIException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    [Serializable]
    public class InvalidTokenException : InternalServerAPIException
    {
        private static string GetMessage(string token)
        {
            return $"Invalid user token: \"{token}\"";
        }

        public InvalidTokenException() { }
        public InvalidTokenException(string token) : base(GetMessage(token)) { }
        public InvalidTokenException(string token, Exception inner) : base(GetMessage(token), inner) { }
        protected InvalidTokenException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    [Serializable]
    public class ServerAPIInvalidFormatException<T> : ServerAPIException
    {
        private static string GetError()
        {
            return $"Payload could not be converted to {typeof(T).Name}";
        }

        public ServerAPIInvalidFormatException(HttpVerb method, string endpoint, string jsonBody)
            : base(method, endpoint, 200L, GetError(), jsonBody) { }
    }

    [Serializable]
    public class ServerAggregateValidationResult
    {
        [JsonProperty("results")]
        public Dictionary<string, ServerValidationResult> Fields { get; set; } = new();
        [JsonProperty("success")]
        public bool Success { get; set; }
    }
    [Serializable]
    public class ServerValidationResult
    {
        [JsonProperty("error")]
        public string Error { get; set; } = string.Empty;
        [JsonProperty("success")]
        public bool Success { get; set; }
    }

    public enum HttpVerb
    {
        Get,
        Post,
        Put,
        Delete,
        Patch
    }
}