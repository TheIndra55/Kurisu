﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using Kurisu.Modules;
using Kurisu.Configuration;
using Kurisu.Models;
using RethinkDb.Driver;
using RethinkDb.Driver.Net;
using System.Reflection;
using Kurisu.External.GoogleAssistant;
using Microsoft.Extensions.Logging;
using DSharpPlus.Interactivity.Extensions;

namespace Kurisu
{
    class Program
    {
        // static variables
        public static RethinkDB R = RethinkDB.R;

        public static DiscordClient Client { get; private set; }
        public static InteractivityExtension Interactivity { get; private set; }

        public static Connection Connection { get; private set; }

        private static CommandsNextExtension Commands { get; set; }

        public static Dictionary<ulong, Guild> Guilds = new();

        // convars
        [ConVar("token", HelpText = "The bot token")]
        public static string Token { get; set; }

        [ConVar("database_name")]
        public static string Database { get; set; }

        [ConVar("database_host")]
        public static string DatabaseHostname { get; set; }

        [ConVar("presence", HelpText = "The presence the bot is 'playing'")]
        public static string Game { get; set; }

        [ConVar("prefix")]
        public static string Prefix { get; set; } = "d!";

        static void Main(string[] args)
        {
            MainAsync(args).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args)
        {
            var types = Assembly.GetExecutingAssembly().GetTypes();

            foreach (var type in types)
            {
                // get all properties with the ConVar attribute
                var convars = type.GetProperties().Where(property => property.GetCustomAttributes(typeof(ConVarAttribute), false).Any());

                foreach (var property in convars)
                {
                    // get the convar attribute from the property
                    var convar = property.GetCustomAttribute<ConVarAttribute>(true);

                    // add the property to list of convars
                    // this will be used later to trace back which property belongs to which convar
                    ConVar.Convars.Add(convar.Name, new KeyValuePair<ConVarAttribute, PropertyInfo>(convar, property));
                }
            }

            if (args.Length == 0)
            {
                Console.WriteLine("No config file executed. Launch with 'dotnet Kurisu.dll bot.cfg'");
                return;
            }

            if (File.Exists("build.cfg"))
            {
                ReadConfig("build.cfg");
            }

            // execute launch file
            ReadConfig(args[0]);

            // connect to database
            Connection = R.Connection()
                .Hostname(DatabaseHostname)
                .Port(RethinkDBConstants.DefaultPort)
                .Timeout(RethinkDBConstants.DefaultTimeout)
                .Connect();

            Connection.Use(Database);

            // initialize Discord bot
            Client = new DiscordClient(new DiscordConfiguration
            {
                Token = Token,
                TokenType = TokenType.Bot,
                MinimumLogLevel = LogLevel.Debug,
                Intents = DiscordIntents.AllUnprivileged | DiscordIntents.GuildMembers | DiscordIntents.MessageContents
            });

            Interactivity = Client.UseInteractivity(new InteractivityConfiguration());

            // setup modules
            Client.AddExtension(new Scheduler());
            Client.AddExtension(new Modules.Scan());

            // initialize CommandsNext
            Commands = Client.UseCommandsNext(new CommandsNextConfiguration
            {
                StringPrefixes = new[] { Prefix },
                EnableDms = false
            });

            Client.GuildMemberAdded += GuildMemberAdded;
            Client.GuildAvailable += GuildAvailable;

            // register commands
            Commands.RegisterCommands<Administration>();
            Commands.RegisterCommands<Information>();
            Commands.RegisterCommands<Moderation>();
            Commands.RegisterCommands<Remind>();

            Commands.RegisterCommands<Commands.Welcome>();
            Commands.RegisterCommands<Commands.VirusScan>();
            Commands.RegisterCommands<Assistant>();

            Commands.CommandErrored += async (_, e) =>
            {
                var ctx = e.Context;

                if (e.Exception is CommandNotFoundException) return;

                if (e.Exception is ArgumentException && e.Exception.Message == "Could not find a suitable overload for the command.")
                {
                    // create help command context
                    var command = ctx.CommandsNext.FindCommand("help", out var args);
                    var context = ctx.CommandsNext.CreateContext(ctx.Message, ctx.Prefix, command, e.Command.QualifiedName);

                    // send help command
                    await ctx.CommandsNext.ExecuteCommandAsync(context);

                    return;
                }

                if (e.Exception is ArgumentException)
                {
                    // show exception message but less scary than below
                    await ctx.RespondAsync((e.Command.Name == "remindme" ? "⏰ " : "") + e.Exception.Message);

                    return;
                }

                await ctx.RespondAsync($"An error occured while executing the command:\n`{e.Exception.Message}`");
            };

            Client.Ready += async (c, e) =>
            {
                Client.Logger.LogInformation($"Logged in as {c.CurrentUser.Username}");

                await Client.UpdateStatusAsync(new(Game, ActivityType.Playing));
            };

            // start bot
            await Client.ConnectAsync();
            await Task.Delay(-1);
        }

        /// <summary>
        /// Triggered when an user join a guild
        /// </summary>
        private static async Task GuildMemberAdded(DiscordClient _, GuildMemberAddEventArgs e)
        {
            var welcome = Guilds[e.Guild.Id].Welcome;
            if (welcome.Channel == null) return;

            var channel = e.Guild.GetChannel(ulong.Parse(welcome.Channel));
            if (channel == null) return;

            var embed = new DiscordEmbedBuilder()
                .WithTitle(string.Format(welcome.Header, e.Member.Username))
                .WithDescription(welcome.Body)
                .WithColor(new DiscordColor(welcome.Color))
                .WithThumbnail(e.Member.AvatarUrl)
                .Build();

            await channel.SendMessageAsync(embed: embed, content: welcome.Mention ? e.Member.Mention : null);
        }

        /// <summary>
        /// Triggered when a new guild has become available
        /// </summary>
        private static async Task GuildAvailable(DiscordClient _, GuildCreateEventArgs e)
        {
            // check if guild exists in the database
            var guild = await R.Table("guilds").Get(e.Guild.Id.ToString()).RunResultAsync<Guild>(Connection);
            if (guild == null)
            {
                // doesn't exist insert
                guild = new Guild
                {
                    Id = e.Guild.Id.ToString()
                };
                Guilds.Add(e.Guild.Id, guild);

                await R.Table("guilds").Insert(guild).RunAsync(Connection);

                return;
            }

            // add to memory
            Guilds.Add(e.Guild.Id, guild);
        }

        /// <summary>
        /// Reads the config file by path into convars
        /// </summary>
        /// <param name="file">Path to the config file</param>
        private static void ReadConfig(string file)
        {
            // todo protection
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file);
            var lines = File.ReadAllLines(path);

            foreach (var line in lines)
            {
                var split = line.Split(' ');

                var name = split[0];
                var value = string.Join(" ", split.Skip(1));

                ConVar.Set(name, value);
            }
        }
    }
}
