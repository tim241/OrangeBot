using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace OrangeBot.Behaviours
{
    public interface IBotBehaviour
    {
        Task OnReady();
        Task OnMessageReceived(SocketMessage message);
        Task OnMessageUpdated(IMessage message, SocketMessage sMessage, ISocketMessageChannel channel);
        Task OnMessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel);
        Task OnReactionAdded(IUserMessage message, ISocketMessageChannel channel, SocketReaction reaction);
        Task OnBulkMessagesDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> messages, ISocketMessageChannel channel);
        Task OnUserJoined(SocketGuildUser user);
        Task OnUserLeft(SocketGuildUser user);
        Task OnUserBanned(SocketUser user, SocketGuild guild);
        Task OnUserUnbanned(SocketUser user, SocketGuild guild);

    }
}