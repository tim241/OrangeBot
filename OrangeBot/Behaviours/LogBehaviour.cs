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
        private ConcurrentDictionary<ulong, IMessageChannel> _AuditLogChannel { get; set; }
        private DiscordSocketClient _Client { get; set; }
        private BotConfiguration _Configuration { get; set; }
        public LogBehaviour(DiscordSocketClient client, BotConfiguration config)
        {
            this._Client = client;
            this._Configuration = config;

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

        public Task OnMessageReceived(SocketMessage message) => Task.CompletedTask;

        public async Task OnMessageUpdated(Cacheable<IMessage, ulong> oldMessage,
                                     SocketMessage newMessage,
                                     ISocketMessageChannel channel)
        {
            // return when oldMessage has no value
            if (!oldMessage.HasValue)
                return;

            // Amazing API design Part 1:
            // when there's an embed,
            // the Discord API will send the metadata
            // and apply it with a MessageUpdated event,
            // without modifying the Timestamp
            //
            // https://github.com/Rapptz/discord.py/issues/273#issuecomment-239647478
            //
            // so to combat this 'intended behaviour'
            // we just return once there are *any* embeds in the message
            if (oldMessage.Value.Embeds.Count > 0 ||
                newMessage.Embeds.Count > 0)
                return;

            ulong currentGuild = ((SocketGuildChannel)channel).Guild.Id;

            // return when the message isn't in the configured guilds
            if (!_AuditLogChannel.ContainsKey(currentGuild))
                return;

            await DiscordHelper.SendEmbed(new EmbedBuilder()
            {
                Url = newMessage.GetJumpUrl(),
                Author = new EmbedAuthorBuilder()
                {
                    Name = DiscordHelper.GetUserName(newMessage.Author),
                    IconUrl = newMessage.Author.GetAvatarUrl()
                },
                Fields = {
                    new EmbedFieldBuilder()
                    {
                        Name = "Old Message",
                        Value = oldMessage.Value.Content
                    },
                    new EmbedFieldBuilder()
                    {
                        Name = "New Message",
                        Value = newMessage.Content
                    }
                },
                Timestamp = newMessage.EditedTimestamp,
                Footer = new EmbedFooterBuilder() { Text = $"edited • #{channel.Name}" }
            }, _AuditLogChannel[currentGuild]);
        }

        public async Task OnMessageDeleted(Cacheable<IMessage, ulong> msg, ISocketMessageChannel channel)
        {
            // return when msg has no value
            if (!msg.HasValue)
                return;

            IMessage message = msg.GetOrDownloadAsync().Result;

            // return when message is empty
            if (String.IsNullOrEmpty(message.Content)
                && message.Attachments.Count == 0)
                return;

            ulong currentGuild = ((SocketGuildChannel)channel).Guild.Id;

            // return when the message isn't in the configured guilds
            if (!_AuditLogChannel.ContainsKey(currentGuild))
                return;

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
        }

        public Task OnReactionAdded(IUserMessage message,
                                    ISocketMessageChannel channel,
                                    SocketReaction reaction) => Task.CompletedTask;

        public async Task OnBulkMessagesDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> messages,
                                          ISocketMessageChannel channel)
        {
            await DiscordHelper.SendEmbed(new EmbedBuilder()
            {
                Description = $"{messages.Count} messages deleted in #{channel.Name}",
                Timestamp = DateTime.Now,
                Footer = new EmbedFooterBuilder() { Text = $"Event • {channel.Id}" }
            }, _AuditLogChannel[((SocketGuildChannel)channel).Guild.Id]);
        }

        public async Task OnUserJoined(SocketGuildUser user)
        {
            await DiscordHelper.SendEmbed(new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder()
                {
                    Name = DiscordHelper.GetUserName(user),
                    IconUrl = user.GetAvatarUrl()
                },
                Description = "User joined",
                Timestamp = DateTime.Now,
                Footer = new EmbedFooterBuilder() { Text = $"Event • {user.Id}" }
            }, _AuditLogChannel[user.Guild.Id]);
        }

        public async Task OnUserLeft(SocketGuildUser user)
        {
            await DiscordHelper.SendEmbed(new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder()
                {
                    Name = DiscordHelper.GetUserName(user),
                    IconUrl = user.GetAvatarUrl()
                },
                Description = "User left",
                Timestamp = DateTime.Now,
                Footer = new EmbedFooterBuilder() { Text = $"Event • {user.Id}" }
            }, _AuditLogChannel[user.Guild.Id]);
        }

        public async Task OnUserBanned(SocketUser user, SocketGuild guild)
        {
            await DiscordHelper.SendEmbed(new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder()
                {
                    Name = DiscordHelper.GetUserName(user),
                    IconUrl = user.GetAvatarUrl()
                },
                Description = "User banned",
                Timestamp = DateTime.Now,
                Footer = new EmbedFooterBuilder() { Text = $"Event • {user.Id}" }
            }, _AuditLogChannel[guild.Id]);
        }

        public async Task OnUserUnbanned(SocketUser user, SocketGuild guild)
        {
            await DiscordHelper.SendEmbed(new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder()
                {
                    Name = DiscordHelper.GetUserName(user),
                    IconUrl = user.GetAvatarUrl()
                },
                Description = "User unbanned",
                Timestamp = DateTime.Now,
                Footer = new EmbedFooterBuilder() { Text = $"Event • {user.Id}" }
            }, _AuditLogChannel[guild.Id]);
        }
    }
}