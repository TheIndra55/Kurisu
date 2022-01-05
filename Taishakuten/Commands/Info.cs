﻿using System.Linq;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Humanizer;
using System.Threading.Tasks;
using DSharpPlus;
using Taishakuten.Entities;
using System;
using System.Diagnostics;

namespace Taishakuten.Commands
{
    class Info : ApplicationCommandModule
    {
        private Configuration _config;

        public Info(Configuration config)
        {
            _config = config;
        }

        [SlashCommand("about", "Get information about the bot")]
        public async Task AboutCommand(InteractionContext ctx)
        {
            var embed = new DiscordEmbedBuilder()
                .WithTitle(ctx.Client.CurrentUser.Username)
                .AddField("Contributors", "TheIndra, Xwilarg", true)
                .AddField("Library", "DSharpPlus", true)
                .AddField("Guilds", ctx.Client.Guilds.Count.ToString(), true)
                .AddField("Uptime", (DateTime.Now - Process.GetCurrentProcess().StartTime).Humanize(), true)
                .AddField("Source code", "https://github.com/TheIndra55/Kurisu")
                .WithThumbnail(ctx.Client.CurrentUser.GetAvatarUrl(ImageFormat.Png));

            var shortVersion = _config.Version?.Substring(0, 7);
            embed.AddField("Version", $"[{shortVersion}](https://github.com/TheIndra55/Kurisu/commit/{_config.Version})");

            await ctx.CreateResponseAsync(embed);
        }

        [SlashCommand("ping", "See the bot's response time")]
        public async Task AvatarCommand(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync("Pong!");
            var message = await ctx.GetOriginalResponseAsync();

            // get the time difference between the ping command and bot response
            var ping = message.CreationTimestamp - ctx.Interaction.CreationTimestamp;
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Ping: {ping.Humanize()}"));
        }

        [SlashCommand("avatar", "Get the avatar from a user")]
        public async Task AvatarCommand(InteractionContext ctx, [Option("user", "The user to get the avatar from")] DiscordUser user = null)
        {
            if (user == null)
            {
                user = ctx.Guild == null ? ctx.User : ctx.Member;
            }

            // get guild avatar if guild member else just get avatar url from user
            // using GetMemberAsync since DiscordMember from command seems some half object
            var avatar = user is DiscordMember ? (await ctx.Guild.GetMemberAsync(user.Id)).GuildAvatarUrl : user.AvatarUrl;

            var embed = new DiscordEmbedBuilder()
                .WithTitle($"Avatar of {user.Username}")
                .WithImageUrl(avatar);

            await ctx.CreateResponseAsync(embed);
        }

        [SlashCommand("user", "Get information about a user")]
        public async Task UserCommand(InteractionContext ctx, [Option("user", "The user to get information about")] DiscordUser user)
        {
            var mutual =
                ctx.Client.Guilds.Where(guild => guild.Value.Members.Any(member => member.Key == user.Id)).ToList();

            var embed = new DiscordEmbedBuilder()
                .WithTitle($"{user.Username}#{user.Discriminator}")
                .WithDescription(user.Id.ToString())
                .AddField("Account creation", $"{user.CreationTimestamp.Humanize()} ({user.CreationTimestamp:yyyy-MM-dd hh:mm})")
                .WithThumbnail(user.AvatarUrl);

            // if member then add guild related fields
            if (user is DiscordMember)
            {
                var member = user as DiscordMember;

                embed.AddField("Guild join", $"{member.JoinedAt.Humanize()} ({member.JoinedAt:yyyy-MM-dd hh:mm})");
                embed.AddField("Roles", string.Join(", ", member.Roles.Select(x => $"`{x.Name}`")));
            }

            if (mutual.Count > 0)
            {
                // show guilds the bot and user share
                embed.AddField($"Mutual guilds ({mutual.Count})", string.Join(", ", mutual.Select(x => $"`{x.Value.Name}`").Take(5)));
            }

            await ctx.CreateResponseAsync(embed);
        }


        [SlashCommand("guild", "Get information about the current guild")]
        public async Task GuildCommand(InteractionContext ctx)
        {
            // check if run in a guild
            if (ctx.Guild == null)
            {
                await ctx.CreateResponseAsync("This command is only available in a guild", true);
                return;
            }

            var guild = ctx.Guild;

            var embed = new DiscordEmbedBuilder()
                .WithTitle(guild.Name)
                .WithDescription(guild.Id.ToString())
                .AddField("Owner", guild.Owner.Username)
                .AddField("Members", guild.MemberCount.ToString())
                .AddField("Age", $"{guild.CreationTimestamp.Humanize()} ({guild.CreationTimestamp:yyyy-MM-dd hh:mm})")
                .WithThumbnail(guild.IconUrl);

            await ctx.CreateResponseAsync(embed);
        }

        [SlashCommand("channel", "Get information about a channel")]
        public async Task ChannelCommand(InteractionContext ctx, [Option("channel", "The channel to get information about")] DiscordChannel chan = null)
        {
            // get mentioned channel, else use current channel
            var channel = ctx.Guild.GetChannel((chan ?? ctx.Channel).Id);

            var embed = new DiscordEmbedBuilder()
                .WithTitle($"#{channel.Name}")
                .WithDescription(channel.Id.ToString())
                .AddField("Age", $"{channel.CreationTimestamp.Humanize()} ({channel.CreationTimestamp:yyyy-MM-dd hh:mm})");

            if (!string.IsNullOrWhiteSpace(channel.Topic))
            {
                // remove messy newlines
                var topic = channel.Topic.Replace("\n", "");

                embed.AddField("Topic", topic.Substring(0,
                    topic.Length < 300 ? topic.Length : 300));
            }

            // add bitrate if voice/stage channel
            if (channel.Type == ChannelType.Voice || channel.Type == ChannelType.Stage)
                embed.AddField("Bitrate", $"{(channel.Bitrate ?? 64000) / 1000}kbps");

            await ctx.CreateResponseAsync(embed);
        }
    }
}
