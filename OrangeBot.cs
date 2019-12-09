using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace OrangeBot
{
    public class OrangeBot
    {
        private DiscordSocketClient _Client { get; set; }
        private Emoji _PinEmote { get; set; }
        private List<ulong> _PinnedMessages { get; set; }
        private object _PinnedMessagesLock { get; set; }
        private int _PinEmoteCount { get; set; }

        private OrangeBotConfiguration _Configuration { get; set; }
        private ConcurrentDictionary<ulong, SocketGuild> _Guilds { get; set; }
        private ConcurrentDictionary<ulong, IMessageChannel> _PinMessageChannels { get; set; }
        private ConcurrentDictionary<ulong, IMessageChannel> _AuditLogChannels { get; set; }

        private int _DictionaryLimit { get; set; }

        private ConcurrentDictionary<ulong, IMessage> _Messages { get; set; }
        public OrangeBot(string configuration)
        {
            _Client = new DiscordSocketClient();
            _PinEmote = new Emoji("📌");
            _PinnedMessages = new List<ulong>();
            _PinnedMessagesLock = new object();
            _PinEmoteCount = 4;

            _Configuration =
                JsonConvert.DeserializeObject<OrangeBotConfiguration>
                    (File.ReadAllText(configuration));

            _Guilds = new ConcurrentDictionary<ulong, SocketGuild>();
            _PinMessageChannels = new ConcurrentDictionary<ulong, IMessageChannel>();
            _AuditLogChannels = new ConcurrentDictionary<ulong, IMessageChannel>();

            // this consumes ~200-300 MB of RAM
            _DictionaryLimit = 5000000;


            _Messages = new ConcurrentDictionary<ulong, IMessage>();

            BotMain().GetAwaiter().GetResult();
        }

        private async Task BotMain()
        {
            // hook up all events
            _Client.Log += _Log;
            _Client.ReactionAdded += _OnReactionAdded;
            _Client.MessageDeleted += _OnMessageDeleted;
            _Client.MessagesBulkDeleted += _OnBulkMessagesDeleted;
            _Client.MessageReceived += _OnMessageReceived;
            _Client.MessageUpdated += _OnMessageUpdated;
            _Client.UserBanned += _OnUserBanned;
            _Client.UserUnbanned += _OnUserUnbanned;
            _Client.UserLeft += _OnUserLeft;
            _Client.UserJoined += _OnUserJoined;
            _Client.Ready += _OnReady;

            await _Client.LoginAsync(TokenType.Bot, _Configuration.Token);
            await _Client.StartAsync();

            await Task.Delay(-1);
        }

        private async Task _OnReady()
        {
            // init the guild(s) & channel(s)
            foreach (DiscordServer s in _Configuration.Servers)
            {
                _Guilds[s.Guild] = _Client.GetGuild(s.Guild);

                _PinMessageChannels[s.Guild] =
                    (IMessageChannel)_Client.GetChannel(s.PinChannel);

                _AuditLogChannels[s.Guild] =
                    (IMessageChannel)_Client.GetChannel(s.AuditLogChannel);
            }

            _PinMessageChannels.ToList().ForEach(async c =>
                (await c.Value.GetMessagesAsync().FlattenAsync()).ToList().ForEach(m =>
                    m.Embeds.ToList().ForEach(e =>
                        {
                            if (ulong.TryParse(e.Footer.Value.Text, out ulong mId))
                                _PinnedMessages.Add(mId);
                        }
                    )
                )
            );

            await _QueryMessages(false);
        }

        private Task _Log(LogMessage message)
        {
            if (!String.IsNullOrEmpty(message.Message))
                Console.WriteLine($"[Discord.Net] {message.Message}");

            return Task.CompletedTask;
        }

        // fills _Messages dictionary
        private async Task _QueryMessages(bool clear)
        {
            if (clear)
                _Messages.Clear();

            foreach (SocketGuild g in _Guilds.Values)
            {
                foreach (IMessageChannel channel in g.TextChannels)
                {
                    try
                    {
                        IEnumerable<IMessage> messages =
                            await channel.GetMessagesAsync(1000).FlattenAsync();

                        messages.ToList().ForEach(m => _Messages[m.Id] = m);
                    }
                    catch (Exception)
                    {
                        // ignore, go to next channel
                    }
                }
            }
        }

        private async Task _OnUserJoined(SocketGuildUser user)
        {
            await _SendEmbed(new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder() { Name = _GetUserName(user), IconUrl = user.GetAvatarUrl() },
                Description = "User joined",
                Timestamp = DateTime.Now,
                Footer = new EmbedFooterBuilder() { Text = $"Event • {user.Id}" }
            }, _AuditLogChannels[user.Guild.Id]);
        }

        private async Task _OnUserLeft(SocketGuildUser user)
        {
            await _SendEmbed(new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder() { Name = _GetUserName(user), IconUrl = user.GetAvatarUrl() },
                Description = "User left",
                Timestamp = DateTime.Now,
                Footer = new EmbedFooterBuilder() { Text = $"Event • {user.Id}" }
            }, _AuditLogChannels[user.Guild.Id]);
        }

        private async Task _OnUserBanned(SocketUser user, SocketGuild guild)
        {
            await _SendEmbed(new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder() { Name = _GetUserName(user), IconUrl = user.GetAvatarUrl() },
                Description = "User banned",
                Timestamp = DateTime.Now,
                Footer = new EmbedFooterBuilder() { Text = $"Event • {user.Id}" }
            }, _AuditLogChannels[guild.Id]);
        }

        private async Task _OnUserUnbanned(SocketUser user, SocketGuild guild)
        {
            await _SendEmbed(new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder() { Name = _GetUserName(user), IconUrl = user.GetAvatarUrl() },
                Description = "User unbanned",
                Timestamp = DateTime.Now,
                Footer = new EmbedFooterBuilder() { Text = $"Event • {user.Id}" }
            }, _AuditLogChannels[guild.Id]);
        }

        private async Task _OnBulkMessagesDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> arg1, ISocketMessageChannel arg2)
        {
            // just to make the warning go away
            await Task.Run(() => null);

            // TODO? from log:
            // [Discord.Net] A bulk delete event has been received, 
            // but the event handling behavior has not been set. 
            // To supress this message, set the ExclusiveBulkDelete configuration property. 
            // This message will appear only once.
        }

        private Task _OnMessageUpdated(Cacheable<IMessage, ulong> message, SocketMessage sMessage, ISocketMessageChannel channel)
        {
            Task.Run(() =>
            {
                _Messages[sMessage.Id] = sMessage;
            });

            return Task.CompletedTask;
        }

        private async Task _OnMessageReceived(SocketMessage message)
        {
            // let's strip the Dictionary once we reach the limit
            if (_Messages.Count >= _DictionaryLimit)
                await _QueryMessages(true);
            else
                _Messages[message.Id] = message;
        }

        private async Task _OnMessageDeleted(Cacheable<IMessage, ulong> message1, ISocketMessageChannel channel)
        {
            if (!_Messages.ContainsKey(message1.Id))
                return;

            IMessage message = _Messages[message1.Id];

            ulong currentGuild = ((SocketGuildChannel)channel).Guild.Id;

            // return when invalid Guild
            if (!_Guilds.ContainsKey(currentGuild))
                return;

            // return when invalid Author
            if (message.Author.Id == 0)
                return;

            // return when message is empty
            if (String.IsNullOrEmpty(message.Content)
                && message.Attachments.Count == 0)
                return;

            // HACK!!
            string content = message.Content;
            foreach (Attachment a in message.Attachments)
            {
                string extension = a.Filename.Split('.').Last().ToLower();
                switch (extension)
                {
                    case "gif":
                    case "jpg":
                    case "jpeg":
                    case "png":
                    case "webp":
                    case "tiff":
                    case "bmp":
                        continue;
                }

                content += Environment.NewLine + a.ProxyUrl;
            }

            await _SendEmbed(new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder() { Name = _GetUserName(message.Author), IconUrl = message.Author.GetAvatarUrl() },
                Description = content,
                ImageUrl = message.Attachments.Count != 0 ? message.Attachments.First().ProxyUrl : null,
                Timestamp = message.Timestamp,
                Footer = new EmbedFooterBuilder() { Text = $"deleted • #{channel.Name}" }
            }, _AuditLogChannels[currentGuild]);

        }

        private async Task _OnReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            // return when it's not the emote we need
            if (reaction.Emote.Name != _PinEmote.Name)
                return;

            ulong currentGuild = ((SocketGuildChannel)channel).Guild.Id;

            // return when it's in a Pin channel
            if (_PinMessageChannels.ContainsKey(currentGuild))
                return;

            // make sure the message has been cached so that we have it
            IUserMessage msg = message.GetOrDownloadAsync().Result;

            // discard everything that's >=24 hours old
            if (msg.Timestamp <= DateTime.Now.AddHours(-24))
                return;

            int emoteCount = (await msg.GetReactionUsersAsync(_PinEmote, _PinEmoteCount).FlattenAsync()).Count();

            lock (_PinnedMessagesLock)
            {
                if (emoteCount < _PinEmoteCount
                    && !_PinnedMessages.Contains(message.Id))
                {
                    return;
                }
            }

            await _SendEmbed(new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder() { Name = _GetUserName(msg.Author), IconUrl = msg.Author.GetAvatarUrl() },
                Description = msg.Content,
                ImageUrl = msg.Attachments.Count != 0 ? msg.Attachments.First().ProxyUrl : null,
                Timestamp = msg.Timestamp,
                Footer = new EmbedFooterBuilder() { Text = $"#{msg.Channel.Name} • {message.Id}" }
            }, _PinMessageChannels[currentGuild]);

            lock (_PinnedMessagesLock)
            {
                _PinnedMessages.Add(message.Id);
            }

        }

        // sends Embed to channel
        private async Task _SendEmbed(EmbedBuilder eBuilder, IMessageChannel channel)
        {
            await channel.SendMessageAsync(embed: eBuilder.Build());
        }

        private string _GetUserName(IUser user) => $"{user.Username}#{user.Discriminator}";
    }
}
