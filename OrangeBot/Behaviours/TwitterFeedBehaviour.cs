using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using OrangeBot.Configuration;
using OrangeBot.Helpers;

namespace OrangeBot.Behaviours
{
    public class TwitterFeedBehaviour : IBotBehaviour
    {
        private DiscordSocketClient _Client { get; set; }
        private BotConfiguration _Configuration { get; set; }
        private HttpClient _HttpClient { get; set; }
        private string _TwitterKey { get; set; }
        private string _TwitterSecret { get; set; }
        private ConcurrentDictionary<string[], ulong> _TwitterUsers { get; set; }
        private ConcurrentDictionary<ulong, List<string>> _Tweets { get; set; }
        private ConcurrentDictionary<ulong, IMessageChannel> _FeedChannel { get; set; }
        private int _TweetLimitPerGuild { get; set; }
        private string _TwitterApiKey { get; set; }
        private string _TwitterApiSecret { get; set; }
        public TwitterFeedBehaviour(DiscordSocketClient client, BotConfiguration config)
        {
            this._Client = client;
            this._Configuration = config;

            this._TwitterUsers = new ConcurrentDictionary<string[], ulong>();
            this._Tweets = new ConcurrentDictionary<ulong, List<string>>();
            this._FeedChannel = new ConcurrentDictionary<ulong, IMessageChannel>();

            // max stored messages per guild
            this._TweetLimitPerGuild = 10;
        }

        public Task OnReady()
        {
            _TwitterApiKey = _Configuration.TwitterApiKey;
            _TwitterApiSecret = _Configuration.TwitterApiSecret;

            foreach (DiscordServer server in _Configuration.Servers)
            {
                var feedChannel = _Client.GetChannel(server.TwitterFeedChannel) as IMessageChannel;

                // skip server when nothing configured
                if (feedChannel == null || server.TwitterUsers == null)
                    continue;

                _TwitterUsers[server.TwitterUsers] = server.Guild;
                _FeedChannel[server.Guild] = feedChannel;
                _Tweets[server.Guild] = new List<string>();
            }

            // import existing 'tweets'
            _FeedChannel.ToList().ForEach(c =>
            {
                if (c.Value != null)
                {
                    c.Value.GetMessagesAsync(10).FlattenAsync().Result.ToList().ForEach(m =>
                    {
                        // make sure message is twitter URL
                        if (m.Content.StartsWith("https://twitter.com/") &&
                            m.Content.Contains("/status/"))
                        {
                            _Tweets[c.Key].Add(m.Content);
                        }
                    });
                }
            });

            // reverse all lists
            // because we added them from new -> old
            // and we need old -> new
            _Tweets.ToList().ForEach(t => t.Value.Reverse());

            // init _Client & start watch feed thread
            if (_FeedChannel.Count > 0
                && _TwitterUsers.Count > 0
                && _TwitterApiKey != null
                && _TwitterApiSecret != null)
            {
                _HttpClient = new HttpClient();
                Task.Run(() => _WatchTwitterFeeds());
            }

            return Task.CompletedTask;
        }

        private async Task _WatchTwitterFeeds()
        {
            while (true)
            {
                // loop over each 'twitter user'
                // and check whether we already posted it
                // if not, send it to the configured channel
                foreach (KeyValuePair<string[], ulong> user in _TwitterUsers)
                {
                    foreach (string twitterUser in user.Key)
                    {
                        string tweetUrl =
                            TwitterHelper.getLatestTweetUrl(_HttpClient, twitterUser,
                                                            _TwitterApiKey,
                                                            _TwitterApiSecret);

                        if (_Tweets.ContainsKey(user.Value))
                        {
                            // if we already have it, skip
                            if (_Tweets[user.Value].Contains(tweetUrl))
                                continue;
                        }

                        // send tweet url to channel
                        await (_FeedChannel[user.Value]).SendMessageAsync(tweetUrl);

                        // add tweet url to list
                        _Tweets[user.Value].Add(tweetUrl);

                        // strip _Tweets
                        while (_Tweets[user.Value].Count > _TweetLimitPerGuild)
                        {
                            _Tweets[user.Value].RemoveAt(0);
                        }
                    }
                }

                // wait 1 minute
                // do note that there's an API limit
                // we can only access this API 100k times per day
                Thread.Sleep(1000 * 60);
            }
        }
    }
}
