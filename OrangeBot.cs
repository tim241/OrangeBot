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
        private ulong _PinMessageChannel { get; set; }

        private ulong _AuditLogChannel { get; set; }

        public OrangeBot()
        {
            _Client = new DiscordSocketClient();
            _PinEmote = new Emoji("📌");
            _PinnedMessages = new List<ulong>();
            _PinEmoteCount = 4;

            _PinMessageChannel = 0;
            _AuditLogChannel = 0;

            BotMain().GetAwaiter().GetResult();
        }

        private async Task BotMain()
        {
            string token = "";

            _Client.Log += _Log;
            _Client.ReactionAdded += _OnReactionAdded;
            _Client.MessageDeleted += _OnMessageDeleted;
            _Client.Ready += _OnReady;
            
            await _Client.LoginAsync(TokenType.Bot, token);
            await _Client.StartAsync();

            await Task.Delay(-1);
        }

        private async Task _OnReady()
        {
            // fill _PunnedMessages List
            IMessageChannel pinMessageChannel = (IMessageChannel)_Client.GetChannel(_PinMessageChannel);

            IEnumerable<IMessage> messages = 
                await pinMessageChannel.GetMessagesAsync().FlattenAsync();
            
            messages.ToList().ForEach(m =>
                m.Embeds.ToList().ForEach(e =>
                    {
                        if(ulong.TryParse(e.Footer.Value.Text, out ulong mId))
                            _PinnedMessages.Add(mId);
                    }
                )
            );
        }

        private Task _Log(LogMessage message)
        {
            if (!String.IsNullOrEmpty(message.Message))
                Console.WriteLine($"[Discord.Net] {message.Message}");

            return Task.CompletedTask;
        }

        
        private async Task _OnMessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            // TODO
            // doesn't work
            // so maybe manually track the messages?

            if(!message.HasValue)
                return;

            IMessageChannel channel1 = (IMessageChannel)_Client.GetChannel(_AuditLogChannel);

            EmbedBuilder eBuilder = new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder() { Name = message.Value.Author.Username, IconUrl = message.Value.Author.GetAvatarUrl() },
                Description = message.Value.Content,
                ImageUrl = message.Value.Attachments.Count != 0 ? message.Value.Attachments.First().Url : null,
                Timestamp = message.Value.Timestamp,
                Footer = new EmbedFooterBuilder() { Text = $"deleted * {channel.Name} * {message.Id.ToString()}" }
            };

            channel1.SendMessageAsync(embed: eBuilder.Build());
        }

        private async Task _OnReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            // return when it's not the emote we need
            if (reaction.Emote.Name != _PinEmote.Name)
                return;

            // return when it's not in a sane channel!
            if (channel.Id == _PinMessageChannel)
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
                _PinMessage(msg, (IMessageChannel)_Client.GetChannel(_PinMessageChannel));
                _PinnedMessages.Add(message.Id);
            }
        }

        private async Task _PinMessage(IUserMessage message, IMessageChannel channel)
        {
            EmbedBuilder eBuilder = new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder() { Name = message.Author.Username, IconUrl = message.Author.GetAvatarUrl() },
                Description = message.Content,
                ImageUrl = message.Attachments.Count != 0 ? message.Attachments.First().Url : null,
                Timestamp = message.Timestamp,
                Footer = new EmbedFooterBuilder() { Text = message.Id.ToString() }
            };

            await channel.SendMessageAsync(embed: eBuilder.Build());
        }
    }
}
