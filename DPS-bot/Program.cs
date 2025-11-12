using DPS_bot.Models;
using DPS_bot.Repositories;
using DPS_bot.Services;
using DPS_bot.Config;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

class Program
{
    static void Main()
    {
        // Load configuration from the application's base directory
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var appConfig = configuration.Get<AppConfig>();

        // Настройка логгера
        LoggerService.Configure(appConfig.Logger);
        LoggerService.CleanOldLogs(30);
        LoggerService.LogInfo("Бот запущен.");
        Console.WriteLine($"Guild ID: {appConfig.Discord.GuildId}");

        var reportPath = appConfig.Bot.ReportPath;
        var filePath = Path.Combine(reportPath, "boss_kills.json");
        var repo = new RaidsRepository(filePath);

        var bossKillParser = new RaidParser(appConfig);
        var freshFights = bossKillParser.ParseFromGuildPage(appConfig.Domen.BaseUrl);

        var dpsParser = new RaidInfoParser();

        foreach (var fight in freshFights)
        {
            if (repo.Contains(fight))
            {
                Console.WriteLine($"бой {fight.GetKey} уже есть");
                continue;
            }

            try
            {
                var detailed = dpsParser.Parse(fight.DetailsUrl);
                fight.Players = detailed.Players;
                fight.FightDate = detailed.KilledAt;
                fight.Attempts = detailed.Attempts;
                fight.OverallDps = detailed.OverallDps;
                fight.OverallHps = detailed.OverallHps;
                repo.Add(fight);
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"Ошибка при получении подробностей боя: {ex.Message}");
            }
        }

        repo.Save();
    }
}
