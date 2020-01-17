using System;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace OrangeBot.Helpers
{
    // for json data
    public class FeedData
    {
        public ulong id { get; set; }
        public string id_str { get; set; }
    }
    // for json data
    public class TokenData
    {
        public string token_type { get; set; }
        public string access_token { get; set; }
    }


    public static class TwitterHelper
    {
        public static string getLatestTweetUrl(HttpClient client, string user, string apiKey, string apiSecret)
        {
            string url = $"https://api.twitter.com/1.1/statuses/user_timeline.json?screen_name={user}&count=1&exclude_replies=1";

            // only add Bearer token when it hasn't been added yet
            if (!client.DefaultRequestHeaders.Contains("Authorization"))
            {
                // add access token to Authorization
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {getAccessToken(client, apiKey, apiSecret)}");
            }

            FeedData[] feed =
                JsonConvert.DeserializeObject<FeedData[]>(client.GetStringAsync(url).Result);

            // we only request 1 tweet, 
            // so always select first item
            return $"https://twitter.com/{user}/status/{feed[0].id}";
        }

        // get the OAuth2 Bearer token
        private static string getAccessToken(HttpClient client, string apiKey, string apiSecret)
        {
            string AuthBase64Data =
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:{apiSecret}"));

            // POST '/oauth2/token' with 'grant_type=client_credentials'
            // and use key + secret for Authorization (formatted as 'key:secret' in base64)
            HttpRequestMessage request =
                new HttpRequestMessage(HttpMethod.Post, "https://api.twitter.com/oauth2/token");

            request.Content =
                new StringContent("grant_type=client_credentials",
                                    Encoding.UTF8,
                                    "application/x-www-form-urlencoded");

            request.Headers.Add("Authorization", $"Basic {AuthBase64Data}");

            HttpResponseMessage response = client.SendAsync(request).Result;

            string content = response.Content.ReadAsStringAsync().Result;

            TokenData tokenData =
                JsonConvert.DeserializeObject<TokenData>(content);

            return tokenData.access_token;
        }

    }
}