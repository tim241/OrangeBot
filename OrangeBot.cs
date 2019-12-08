// disable useless warnings
#pragma warning disable CS4014,CS1998

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.API;
using Discord.WebSocket;

namespace OrangeBot
{
    public class OrangeBot
    {
        private DiscordSocketClient _Client { get; set; }
        private Emoji _PinEmote { get; set; }
        private List<ulong> _PinnedMessages { get; set; }
        private int _PinEmoteCount { get; set; }

        private ulong _GuildId { get; set; }
        private ulong _PinMessageChannelId { get; set; }
        private ulong _AuditLogChannelId { get; set; }

        private SocketGuild _Guild { get; set; }
        private IMessageChannel _PinMessageChannel { get; set; }
        private IMessageChannel _AuditLogChannel { get; set; }

        private int _DictionaryLimit { get; set; }

        private Dictionary<ulong, IMessage> _Messages { get; set; }
        public OrangeBot()
        {
            _Client = new DiscordSocketClient();
            _PinEmote = new Emoji("📌");
            _PinnedMessages = new List<ulong>();
            _PinEmoteCount = 4;

            _GuildId = 0;
            _PinMessageChannelId = 0;
            _AuditLogChannelId = 0;

            // this consumes ~400 MB of RAM
            _DictionaryLimit = 10000000;

            _Messages = new Dictionary<ulong, IMessage>();
>>>>>>> e9a0a22... update

            BotMain().GetAwaiter().GetResult();
        }

        private async Task BotMain()
        {
            string token = "";

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

            await _Client.LoginAsync(TokenType.Bot, token);
            await _Client.StartAsync();

            await Task.Delay(-1);
        }

        private async Task _OnReady()
        {
            // init the guild(s) & channel(s)
            _Guild = _Client.GetGuild(_GuildId);
            _PinMessageChannel = (IMessageChannel)_Client.GetChannel(_PinMessageChannelId);
            _AuditLogChannel = (IMessageChannel)_Client.GetChannel(_AuditLogChannelId);

            // fill _PunnedMessages List
            IEnumerable<IMessage> messages =
                await _PinMessageChannel.GetMessagesAsync().FlattenAsync();

            messages.ToList().ForEach(m =>
                m.Embeds.ToList().ForEach(e =>
                    {
                        if (ulong.TryParse(e.Footer.Value.Text, out ulong mId))
                            _PinnedMessages.Add(mId);
                    }
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

            foreach (IMessageChannel channel in _Guild.TextChannels)
            {
                try
                {
                    foreach (IMessage message in channel.GetMessagesAsync(1000).FlattenAsync().Result)
                    {
                        _Messages.Add(message.Id, message);
                    }
                }
                catch (Exception)
                {
                    // ignore, go to next channel
                }
            }
        }

        private async Task _OnUserJoined(SocketGuildUser user)
        {
            _SendEmbed(new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder() { Name = user.Username, IconUrl = user.GetAvatarUrl() },
                Description = "User joined",
                Timestamp = DateTime.Now,
                Footer = new EmbedFooterBuilder() { Text = $"Event • {user.Id}" }
            }, _AuditLogChannel);
        }

        private async Task _OnUserLeft(SocketGuildUser user)
        {
            _SendEmbed(new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder() { Name = user.Username, IconUrl = user.GetAvatarUrl() },
                Description = "User left",
                Timestamp = DateTime.Now,
                Footer = new EmbedFooterBuilder() { Text = $"Event • {user.Id}" }
            }, _AuditLogChannel);
        }

        private async Task _OnUserBanned(SocketUser user, SocketGuild guild)
        {
            _SendEmbed(new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder() { Name = user.Username, IconUrl = user.GetAvatarUrl() },
                Description = "User banned",
                Timestamp = DateTime.Now,
                Footer = new EmbedFooterBuilder() { Text = $"Event • {user.Id}" }
            }, _AuditLogChannel);
        }

        private async Task _OnUserUnbanned(SocketUser user, SocketGuild guild)
        {
            _SendEmbed(new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder() { Name = user.Username, IconUrl = user.GetAvatarUrl() },
                Description = "User unbanned",
                Timestamp = DateTime.Now,
                Footer = new EmbedFooterBuilder() { Text = $"Event • {user.Id}" }
            }, _AuditLogChannel);
        }

        private async Task _OnBulkMessagesDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> arg1, ISocketMessageChannel arg2)
        {
            // TODO? from log:
            // [Discord.Net] A bulk delete event has been received, 
            // but the event handling behavior has not been set. 
            // To supress this message, set the ExclusiveBulkDelete configuration property. 
            // This message will appear only once.
        }

        private async Task _OnMessageUpdated(Cacheable<IMessage, ulong> message, SocketMessage sMessage, ISocketMessageChannel channel)
        {
            if (!_Messages.ContainsKey(sMessage.Id))
                _Messages.Add(sMessage.Id, sMessage);
            else
                _Messages[sMessage.Id] = sMessage;
        }

        private async Task _OnMessageReceived(SocketMessage message)
        {
            //  let's strip the Dictionary once we reach the limit
            if (_Messages.Count >= _DictionaryLimit)
                _QueryMessages(true);
            else
                _Messages.Add(message.Id, message);
        }

        private async Task _OnMessageDeleted(Cacheable<IMessage, ulong> message1, ISocketMessageChannel channel)
        {
            if (!_Messages.ContainsKey(message1.Id))
                return;

            IMessage message = _Messages[message1.Id];

            // return when invalid Author
            if (message.Author.Id == 0)
                return;

            // return when message is empty
            if (String.IsNullOrEmpty(message.Content)
                && message.Attachments.Count == 0)
                return;

            _SendEmbed(new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder() { Name = message.Author.Username, IconUrl = message.Author.GetAvatarUrl() },
                Description = message.Content,
                ImageUrl = message.Attachments.Count != 0 ? message.Attachments.First().ProxyUrl : null,
                Timestamp = message.Timestamp,
                Footer = new EmbedFooterBuilder() { Text = $"deleted • #{channel.Name}" }
            }, _AuditLogChannel);

        }

        private async Task _OnReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            // return when it's not the emote we need
            if (reaction.Emote.Name != _PinEmote.Name)
                return;

            // return when it's not in a sane channel!
            if (channel.Id == _PinMessageChannelId)
                return;

            // make sure the message has been cached so that we have it
            IUserMessage msg = message.GetOrDownloadAsync().Result;

            // discard everything that's >=24 hours old
            if (msg.Timestamp <= DateTime.Now.AddHours(-24))
                return;

            int emoteCount = msg.GetReactionUsersAsync(_PinEmote, _PinEmoteCount, null).FlattenAsync().Result.Count();

            if (emoteCount >= _PinEmoteCount
                && !_PinnedMessages.Contains(message.Id))
            {
                _SendEmbed(new EmbedBuilder()
                {
                    Author = new EmbedAuthorBuilder() { Name = msg.Author.Username, IconUrl = msg.Author.GetAvatarUrl() },
                    Description = msg.Content,
                    ImageUrl = msg.Attachments.Count != 0 ? msg.Attachments.First().ProxyUrl : null,
                    Timestamp = msg.Timestamp,
                    Footer = new EmbedFooterBuilder() { Text = message.Id.ToString() }
                }, _PinMessageChannel);

                _PinnedMessages.Add(message.Id);
            }
        }

        // sends Embed to channel
        private async Task _SendEmbed(EmbedBuilder eBuilder, IMessageChannel channel)
        {
            await channel.SendMessageAsync(embed: eBuilder.Build());
        }
    }
}
