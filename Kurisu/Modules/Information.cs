﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Humanizer;
using Kurisu.Configuration;
using Newtonsoft.Json;

namespace Kurisu.Modules
{
    class Information : BaseCommandModule
    {
        [ConVar("version")]
        public static string Version { get; set; }

        [Command("about"), Aliases("info"), Description("Show information about the bot")]
        public async Task About(CommandContext ctx)
        {
            var shortVersion = Version?.Substring(0, 7);
            var embed = new DiscordEmbedBuilder()
                .WithTitle(ctx.Client.CurrentUser.Username)
                .AddField("Contributors", "TheIndra, Xwilarg", true)
                .AddField("Library", "DSharpPlus", true)
                .AddField("Guilds", ctx.Client.Guilds.Count.ToString(), true)
                .AddField("Uptime", (DateTime.Now - Process.GetCurrentProcess().StartTime).Humanize(), true)
                .AddField("Source code", "https://github.com/TheIndra55/Kurisu")
                .WithThumbnail(ctx.Client.CurrentUser.GetAvatarUrl(ImageFormat.Png));
            if (shortVersion != null)
            {
                embed.AddField("Version", $"[{shortVersion}](https://github.com/TheIndra55/Kurisu/commit/{Version})");
            }

            await ctx.RespondAsync(embed: embed);
        }

        [Command("guild"), Aliases("server", "serverinfo"), Description("Shows information about the current guild")]
        public async Task Guild(CommandContext ctx)
        {
            var guild = ctx.Guild;

            var embed = new DiscordEmbedBuilder()
                .WithTitle(guild.Name)
                .WithDescription(guild.Id.ToString())
                .AddField("Owner", guild.Owner.Username)
                .AddField("Members", guild.MemberCount.ToString())
                .AddField("Age", $"{guild.CreationTimestamp.Humanize()} ({guild.CreationTimestamp:g})")
                //.AddField("Region", guild.VoiceRegion.Id)
                .WithThumbnail(guild.IconUrl)
                .Build();

            await ctx.RespondAsync(embed: embed);
        }

        [Command("user"), Aliases("whois", "userinfo", "member"), Description("See info about a member")]
        public async Task User(CommandContext ctx, DiscordMember user)
        {
            var mutual =
                ctx.Client.Guilds.Where(guild => guild.Value.Members.Any(member => member.Key == user.Id)).ToList();

            var embed = new DiscordEmbedBuilder()
                .WithTitle($"{user.Username}#{user.Discriminator}")
                .WithDescription(user.Id.ToString())
                .AddField("Account creation", $"{user.CreationTimestamp.Humanize()} ({user.CreationTimestamp:g})")
                .AddField("Guild join", $"{user.JoinedAt.Humanize()} ({user.JoinedAt:g})")
                .AddField("Roles", string.Join(", ", user.Roles.Select(x => $"`{x.Name}`")))
                .WithThumbnail(user.AvatarUrl);

            if (mutual.Any())
            {
                // show all guilds the bot and user share
                embed.AddField($"Mutual guilds ({mutual.Count})", string.Join(", ", mutual.Select(x => $"`{x.Value.Name}`").Take(5)));
            }

            await ctx.RespondAsync(embed: embed.Build());
        }

        [Command("avatar"), Aliases("pf", "pic"), Description("Shows the user's avatar")]
        public async Task Avatar(CommandContext ctx, DiscordMember user = null, string kind = "guild")
        {
            if(user == null)
            {
                user = ctx.Member;
            }

            var avatar = (user.GuildAvatarHash != user.AvatarHash && kind != "user") ? user.GuildAvatarUrl : user.AvatarUrl;

            var embed = new DiscordEmbedBuilder()
                .WithTitle($"Avatar of {user.Username}")
                .WithImageUrl(avatar)
                .Build();

            await ctx.RespondAsync(embed: embed);
        }

        [Command("channel"), Description("See info about a channel")]
        public async Task Channel(CommandContext ctx, DiscordChannel chan = null)
        {
            // get mentioned channel, else use current channel
            var channel = chan ?? ctx.Channel;

            var embed = new DiscordEmbedBuilder()
                .WithTitle($"#{channel.Name}")
                .WithDescription(channel.Id.ToString())
                .AddField("Age", $"{channel.CreationTimestamp.Humanize()} ({channel.CreationTimestamp:g})");

            if (!string.IsNullOrWhiteSpace(channel.Topic))
            {
                // remove messy newlines
                var topic = channel.Topic.Replace("\n", "");

                embed.AddField("Topic", topic.Substring(0,
                    topic.Length < 300 ? topic.Length : 300));
            }

            if (channel.Type == ChannelType.Voice)
                embed.AddField("Bitrate", $"{channel.Bitrate / 1000}kbps");

            await ctx.RespondAsync(embed: embed.Build());
        }

        [Command("ping"), Description("See the bot's ping")]
        public async Task Ping(CommandContext ctx)
        {
            var message = await ctx.RespondAsync("Pong!");

            // get the time difference between the ping command and bot response
            var ping = message.CreationTimestamp - ctx.Message.CreationTimestamp;
            await message.ModifyAsync($"Ping: {ping.Humanize()}");
        }

        [Command("settings")]
        public async Task Settings(CommandContext ctx)
        {
            await ctx.RespondAsync(JsonConvert.SerializeObject(Program.Guilds[ctx.Guild.Id]));
        }
    }
}
