using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;

namespace OrangeBot.Helpers
{
    public static class DiscordHelper
    {
        public static async Task SendEmbed(EmbedBuilder eBuilder, IMessageChannel channel)
        {
            await channel.SendMessageAsync(embed: eBuilder.Build());
        }

        public static string GetUserName(IUser user) => $"{user.Username}#{user.Discriminator}";

        public static string GetMessageContent(IMessage message)
        {
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

            return content;
        }
    }
}