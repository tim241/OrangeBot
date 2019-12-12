/*
 OrangeBot - https://github.com/tim241/OrangeBot   
 Copyright (C) 2019 Tim Wanders <tim241@mailbox.org>
 This program is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.
 This program is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 GNU General Public License for more details.
 You should have received a copy of the GNU General Public License
 along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using OrangeBot.Behaviours;
using OrangeBot.Configuration;

namespace OrangeBot
{
    public class OrangeBot
    {
        private DiscordSocketClient _Client { get; set; }
        private BotConfiguration _Configuration { get; set; }
        private List<IBotBehaviour> _BotBehaviours { get; set; }
        public OrangeBot(string configuration)
        {
            _Client = new DiscordSocketClient();

            _Configuration =
                JsonConvert.DeserializeObject<BotConfiguration>
                    (File.ReadAllText(configuration));

            _BotBehaviours = new List<IBotBehaviour>();

            _BotBehaviours.Add(new Behaviours.LogBehaviour(_Client, _Configuration));
            _BotBehaviours.Add(new Behaviours.StarboardBehaviour(_Client, _Configuration));

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

        private Task _Log(LogMessage message)
        {
            if (!String.IsNullOrEmpty(message.Message))
                Console.WriteLine($"[Discord.Net:{message.Severity}] {message.Message}");

            return Task.CompletedTask;
        }
        
        private Task _OnReady()
        {
            _BotBehaviours.ForEach(b => b.OnReady());
            return Task.CompletedTask;
        }

        private Task _OnUserJoined(SocketGuildUser user)
        {
            _BotBehaviours.ForEach(b => b.OnUserJoined(user));
            return Task.CompletedTask;
        }

        private Task _OnUserLeft(SocketGuildUser user)
        {
            _BotBehaviours.ForEach(b => b.OnUserLeft(user));
            return Task.CompletedTask;
        }

        private Task _OnUserBanned(SocketUser user, SocketGuild guild)
        {
            _BotBehaviours.ForEach(b => b.OnUserBanned(user, guild));
            return Task.CompletedTask;
        }

        private Task _OnUserUnbanned(SocketUser user, SocketGuild guild)
        {
            _BotBehaviours.ForEach(b => b.OnUserUnbanned(user, guild));
            return Task.CompletedTask;
        }

        private Task _OnBulkMessagesDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> messages, ISocketMessageChannel channel)
        {
            _BotBehaviours.ForEach(b => b.OnBulkMessagesDeleted(messages, channel));
            return Task.CompletedTask;
        }

        private Task _OnMessageUpdated(Cacheable<IMessage, ulong> message, SocketMessage sMessage, ISocketMessageChannel channel)
        {
            _BotBehaviours.ForEach(b => b.OnMessageUpdated(message.GetOrDownloadAsync().Result, sMessage, channel));
            return Task.CompletedTask;
        }

        private Task _OnMessageReceived(SocketMessage message)
        {
            _BotBehaviours.ForEach(b => b.OnMessageReceived(message));
            return Task.CompletedTask;
        }

        private Task _OnMessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            _BotBehaviours.ForEach(b => b.OnMessageDeleted(message, channel));
            return Task.CompletedTask;
        }

        private Task _OnReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            _BotBehaviours.ForEach(b => b.OnReactionAdded(message.GetOrDownloadAsync().Result, channel, reaction));
            return Task.CompletedTask;
        }
    }
}
