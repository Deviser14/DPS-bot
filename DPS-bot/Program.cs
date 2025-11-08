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
        // Загрузка конфигурации
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        // Загружаем AppConfig (а не ConfigService)
        var appConfig = configuration.Get<AppConfig>();

        // Настройка логгера
        LoggerService.Configure(appConfig.Logger);
        LoggerService.CleanOldLogs(30);
        LoggerService.LogInfo("Бот запущен.");
        Console.WriteLine($"Guild ID: {appConfig.Discord.GuildId}");

        // Инициализация репозитория
        var reportPath = appConfig.Bot.ReportPath;
        var filePath = Path.Combine(reportPath, "boss_kills.json");
        var repo = new FightRepository(filePath);

        // Парсинг свежих боёв
        var bossKillParser = new BossKillParser(appConfig);
        var freshFights = bossKillParser.ParseFromGuildPage(appConfig.Domen.BaseUrl); // без подробностей

        var dpsParser = new DpsParser();

        foreach (var fight in freshFights)
        {
            if (repo.Contains(fight)) continue;

            try
            {
                var detailed = bossKillParser.ParseFromGuildPage; // Получаем подробности
                repo.Add(detailed);
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"Ошибка при получении подробностей боя: {ex.Message}");
            }
        }

        repo.Save();
    }
}