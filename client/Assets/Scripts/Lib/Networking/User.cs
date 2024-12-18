using System;
using System.Threading.Tasks;
using Newtonsoft.Json;

#nullable enable
namespace Networking {
    public class User : IPayload<User>
    {
        [JsonProperty("id")]
        public string Id { get; private set; }
        [JsonProperty("username")]
        public string Username { get; private set; }
        [JsonProperty("email")]
        public string Email { get; private set; }
        [JsonProperty("avatar")]
        public string? AvatarUrl { get; private set; }

        public User(string id, string username, string email, string? avatarUrl = null)
        {
            Id = id;
            Username = username;
            Email = email;
            AvatarUrl = avatarUrl;
        }

        public string Serialize()
        {
            return JsonConvert.SerializeObject(this);
        }

        public override string ToString()
        {
            return $"{Username} ({Id})";
        }

        public static readonly User GuestUser = new("guest", "guest", "", null);
    }

    public class UserToRegister : User, IPayload<UserToRegister>
    {
        [JsonProperty(propertyName: "password")]
        public string Password { get; private set; }

        public UserToRegister(string id, string username, string email, string password, string? avatarUrl = null)
            : base(id, username, email, avatarUrl)
        {
            Password = password;
        }
    }

    public class UserLogin : IPayload<UserLogin>
    {
        [JsonProperty(propertyName: "username")]
        public string Username { get; set; }
        [JsonProperty(propertyName: "password")]
        public string Password { get; set; }

        public UserLogin(string username, string password)
        {
            Username = username;
            Password = password;
        }
    }
}