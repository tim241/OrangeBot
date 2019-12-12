using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using OrangeBot.Configuration;
using OrangeBot.Helpers;
namespace OrangeBot.Behaviours
{
    public class LogBehaviour : IBotBehaviour
    {
        private ConcurrentDictionary<ulong, IMessage> _Messages { get; set; }
        private int _MessagesLimitPerChannel { get; set; }
        private ConcurrentDictionary<ulong, IMessageChannel> _AuditLogChannel { get; set; }
        private DiscordSocketClient _Client { get; set; }
        private BotConfiguration _Configuration { get; set; }
        public LogBehaviour(DiscordSocketClient client, BotConfiguration config)
        {
            this._Client = client;
            this._Configuration = config;

            this._Messages = new ConcurrentDictionary<ulong, IMessage>();
            this._MessagesLimitPerChannel = 1000;
            this._AuditLogChannel = new ConcurrentDictionary<ulong, IMessageChannel>();
        }

        public Task OnReady()
        {
            // init _AuditLogChannel
            foreach (DiscordServer server in _Configuration.Servers)
            {
                _AuditLogChannel[server.Guild] =
                    (IMessageChannel)_Client.GetChannel(server.AuditLogChannel);
            }

            return Task.CompletedTask;
        }

        public Task OnMessageReceived(SocketMessage message)
        {
            ulong currentGuild = ((SocketGuildChannel)message.Channel).Guild.Id;

            // return when not in configured Guild
            if (!_Configuration.Servers.Select(s => s.Guild)
                .Contains(currentGuild))
                return Task.CompletedTask;

            _Messages[message.Id] = message;
            _StripMessages(message.Channel);
            return Task.CompletedTask;
        }

        // use same code as OnMessageReceived
        public Task OnMessageUpdated(IMessage message,
                                     SocketMessage sMessage,
                                     ISocketMessageChannel channel) => OnMessageReceived(sMessage);

        public async Task OnMessageDeleted(Cacheable<IMessage, ulong> msg, ISocketMessageChannel channel)
        {
            if (!_Messages.ContainsKey(msg.Id))
                return;

            IMessage message = _Messages[msg.Id];

            // return when message is empty
            if (String.IsNullOrEmpty(message.Content)
                && message.Attachments.Count == 0)
                return;

            // return when the author is us
            if (message.Author.Id == 0)
                return;

            ulong currentGuild = ((SocketGuildChannel)channel).Guild.Id;

            await DiscordHelper.SendEmbed(new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder()
                {
                    Name = DiscordHelper.GetUserName(message.Author),
                    IconUrl = message.Author.GetAvatarUrl()
                },
                Description = DiscordHelper.GetMessageContent(message),
                ImageUrl = message.Attachments.Count != 0 ? message.Attachments.First().ProxyUrl : null,
                Timestamp = message.Timestamp,
                Footer = new EmbedFooterBuilder() { Text = $"deleted • #{channel.Name}" }
            }, _AuditLogChannel[currentGuild]);

            // remove message from memory
            _Messages.Remove(message.Id, out _);
        }

        public Task OnReactionAdded(IUserMessage message,
                                    ISocketMessageChannel channel,
                                    SocketReaction reaction) => Task.CompletedTask;

        public async Task OnBulkMessagesDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> messages,
                                          ISocketMessageChannel channel)
        {
            await DiscordHelper.SendEmbed(new EmbedBuilder()
            {
                Description = $"{messages.Count} messages deleted in {channel.Name}",
                Timestamp = DateTime.Now,
                Footer = new EmbedFooterBuilder() { Text = $"Event • {channel.Id}" }
            }, _AuditLogChannel[((SocketGuildChannel)channel).Guild.Id]);
        }

        public async Task OnUserJoined(SocketGuildUser user)
        {
            await DiscordHelper.SendEmbed(new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder() { Name = DiscordHelper.GetUserName(user), IconUrl = user.GetAvatarUrl() },
                Description = "User joined",
                Timestamp = DateTime.Now,
                Footer = new EmbedFooterBuilder() { Text = $"Event • {user.Id}" }
            }, _AuditLogChannel[user.Guild.Id]);
        }

        public async Task OnUserLeft(SocketGuildUser user)
        {
            await DiscordHelper.SendEmbed(new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder() { Name = DiscordHelper.GetUserName(user), IconUrl = user.GetAvatarUrl() },
                Description = "User left",
                Timestamp = DateTime.Now,
                Footer = new EmbedFooterBuilder() { Text = $"Event • {user.Id}" }
            }, _AuditLogChannel[user.Guild.Id]);
        }

        public async Task OnUserBanned(SocketUser user, SocketGuild guild)
        {
            await DiscordHelper.SendEmbed(new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder() { Name = DiscordHelper.GetUserName(user), IconUrl = user.GetAvatarUrl() },
                Description = "User banned",
                Timestamp = DateTime.Now,
                Footer = new EmbedFooterBuilder() { Text = $"Event • {user.Id}" }
            }, _AuditLogChannel[guild.Id]);
        }

        public async Task OnUserUnbanned(SocketUser user, SocketGuild guild)
        {
            await DiscordHelper.SendEmbed(new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder() { Name = DiscordHelper.GetUserName(user), IconUrl = user.GetAvatarUrl() },
                Description = "User unbanned",
                Timestamp = DateTime.Now,
                Footer = new EmbedFooterBuilder() { Text = $"Event • {user.Id}" }
            }, _AuditLogChannel[guild.Id]);
        }

        // strips _Messages dictionary when required
        private void _StripMessages(ISocketMessageChannel channel)
        {
            // loop over each channel,
            // count the messages,
            // once it's over the limit,
            // remove the oldest one(s) until we're below the limit
            foreach (IMessageChannel c in _Messages.Select(m => m.Value.Channel))
            {
                while (_Messages.Values.Select(m => m.Channel.Id == c.Id).Count()
                            > _MessagesLimitPerChannel)
                {
                    ulong oldestMsgId = _Messages.First().Key;
                    foreach (KeyValuePair<ulong, IMessage> msg in _Messages)
                    {
                        // skip when not in the right channel
                        if (msg.Value.Channel.Id != c.Id)
                            continue;

                        if (msg.Value.Timestamp < _Messages[oldestMsgId].Timestamp)
                            oldestMsgId = msg.Value.Id;
                    }

                    // if this happens,
                    // print to stdout and return
                    if (!_Messages.Remove(oldestMsgId, out _))
                    {
                        Console.WriteLine("[LogBehavior] Failed to remove message from Dictionary");
                        return;
                    }
                }
            }
        }
    }
}