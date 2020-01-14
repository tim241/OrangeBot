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
    public class StarboardBehaviour : IBotBehaviour
    {
        private DiscordSocketClient _Client { get; set; }
        private BotConfiguration _Configuration { get; set; }
        private ConcurrentDictionary<ulong, IEmote> _StarEmote { get; set; }
        private ConcurrentDictionary<ulong, int> _StarEmoteAmount { get; set; }
        private ConcurrentDictionary<ulong, IMessageChannel> _StarBoardMessageChannel { get; set; }
        private ConcurrentDictionary<ulong, List<ulong>> _StarredMessages { get; set; }
        private object _StarredMessagesLock { get; set; }
        private int _StarredMessagesLimitPerGuild { get; set; }
        public StarboardBehaviour(DiscordSocketClient client, BotConfiguration config)
        {
            this._Client = client;
            this._Configuration = config;

            this._StarEmote = new ConcurrentDictionary<ulong, IEmote>();
            this._StarEmoteAmount = new ConcurrentDictionary<ulong, int>();
            this._StarBoardMessageChannel = new ConcurrentDictionary<ulong, IMessageChannel>();
            this._StarredMessages = new ConcurrentDictionary<ulong, List<ulong>>();
            this._StarredMessagesLock = new object();
            this._StarredMessagesLimitPerGuild = 1000;
        }

        public async Task OnReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            ulong currentGuild = ((SocketGuildChannel)channel).Guild.Id;

            // return when currentGuild isn't in _StarEmote
            if (!_StarEmote.ContainsKey(currentGuild))
                return;

            // we support both 'Emote' and 'Emoji',
            // so check the type
            if (_StarEmote[currentGuild].GetType() == typeof(Emote))
            {
                // return when it's not the 'Emote' we need
                if (!((Emote)_StarEmote[currentGuild]).Equals(reaction.Emote))
                    return;
            }
            else
            {
                // return when it's not the 'Emoji' we need
                if (!((Emoji)_StarEmote[currentGuild]).Equals(reaction.Emote))
                    return;
            }

            // return when it's in a StarBoard channel
            if (_StarBoardMessageChannel[currentGuild].Id == channel.Id)
                return;

            IUserMessage msg = message.GetOrDownloadAsync().Result;

            // return when we failed to retrieve the message
            if (msg == null)
                return;

            // discard everything that's >=24 hours old
            if (msg.Timestamp <= DateTime.Now.AddHours(-24))
                return;

            lock (_StarredMessagesLock)
            {
                int emoteCount = (msg.GetReactionUsersAsync((IEmote)_StarEmote[currentGuild],
                                    _StarEmoteAmount[currentGuild]).FlattenAsync()).Result.Count();

                // once we have less emotes than required
                // or if it's already starred,
                // return
                if (emoteCount < _StarEmoteAmount[currentGuild]
                        || _StarredMessages[currentGuild].Contains(msg.Id))
                {
                    return;
                }

                // store the message id in a List
                _StarredMessages[currentGuild].Add(msg.Id);

                // strip _PinnedMessages once the limit has been reached
                while (_StarredMessages[currentGuild].Count > _StarredMessagesLimitPerGuild)
                {
                    _StarredMessages[currentGuild].RemoveAt(0);
                }
            }

            await DiscordHelper.SendEmbed(new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder()
                {
                    Name = DiscordHelper.GetUserName(msg.Author),
                    IconUrl = msg.Author.GetAvatarUrl(),
                    Url = msg.GetJumpUrl()
                },
                Description = DiscordHelper.GetMessageContent(msg),
                ImageUrl = msg.Attachments.Count != 0 ? msg.Attachments.First().ProxyUrl : null,
                Timestamp = msg.Timestamp,
                Footer = new EmbedFooterBuilder() { Text = $"#{msg.Channel.Name} • {msg.Id}" }
            }, _StarBoardMessageChannel[currentGuild]);
        }

        public Task OnReady()
        {
            // init _PinEmote
            foreach (DiscordServer server in _Configuration.Servers)
            {
                var starChannel = _Client.GetChannel(server.StarChannel) as IMessageChannel;

                // skip 'server' when we can't find the StarChannel
                if(starChannel == null)
                    continue;

                // I want to support custom emotes,
                // so try to parse 'server.StarEmote' as 'Emote',
                // if we can't, use 'Emoji'
                if (Emote.TryParse(server.StarEmote, out Emote emote))
                    _StarEmote[server.Guild] = emote;
                else
                    _StarEmote[server.Guild] = new Emoji(server.StarEmote);

                _StarEmoteAmount[server.Guild] = server.StarEmoteCount;
                _StarBoardMessageChannel[server.Guild] = starChannel;
                _StarredMessages[server.Guild] = new List<ulong>();
            }

            // import existing starred messages
            _StarBoardMessageChannel.ToList().ForEach(c =>
            {
                // make sure c.Value has a value
                if (c.Value != null)
                {
                    (c.Value.GetMessagesAsync(_StarredMessagesLimitPerGuild).FlattenAsync()).Result.ToList().ForEach(m =>
                     m.Embeds.ToList().ForEach(e =>
                        {
                            ulong guildId = ((SocketGuildChannel)c.Value).Guild.Id;
                            string text = e.Footer.Value.Text;
                            string seperator = " • ";
                            if (text.Contains(seperator) &&
                                text.Split(seperator).Length > 0)
                            {
                                if (ulong.TryParse(text.Split(seperator)[1], out ulong mId))
                                    _StarredMessages[guildId].Add(mId);
                            }
                        }
                    ));
                }
            });

            // we added the starred messages from new to old,
            // so reverse all the Lists
            _StarredMessages.ToList().ForEach(mList => mList.Value.Reverse());

            return Task.CompletedTask;
        }
    }
}