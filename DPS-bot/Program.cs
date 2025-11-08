using DPS_bot.Models;
using DPS_bot.Services;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
// Заменяем SeleniumExtras на актуальный пакет
using SeleniumExtras.WaitHelpers;
using System;
using System.Text.RegularExpressions;

class Program
{
    static void Main()
    {
        // Настройка логгера (если есть конфиг)
        //LoggerService.MinimumLevel = LogLevel.Debug;
        //LoggerService.WriteToConsole = true;

        // 🧹 Очистка старых логов
        LoggerService.CleanOldLogs(30);

        // Основная логика
        LoggerService.LogInfo("Бот запущен.");
        // ...

        BossKillParser bossKillParser = new BossKillParser();
        bossKillParser.ParseFromGuildPage("https://sirus.su/base/guilds/x3/3029/latest-boss-kills");
        DpsParser parser = new DpsParser();
        parser.Parse("https://sirus.su/base/pve-progression/boss-kill/x3/64814");
    }
}