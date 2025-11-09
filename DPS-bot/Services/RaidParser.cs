using DPS_bot.Config;
using DPS_bot.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System;
using System.Linq;
using System.Net.WebSockets;

namespace DPS_bot.Services
{
    public class RaidParser
    {

            private readonly string _baseUrl;
            private readonly string _reportPath;

            public RaidParser(AppConfig config)
            {
                _baseUrl = config.Domen.BaseUrl;
                _reportPath = config.Bot.ReportPath;
            }


        /// <summary>
        /// Парсит страницу гильдии и возвращает список боёв.
        /// </summary>
        public List<Raid> ParseFromGuildPage(string fightUrl)
        {
            var kills = new List<Raid>();

            if (string.IsNullOrEmpty(fightUrl) || !Uri.IsWellFormedUriString(fightUrl, UriKind.Absolute))
            {
                throw new ArgumentException("Некорректный URL", nameof(fightUrl));
            }

            LoggerService.LogInfo($"[Parse] Start parsing URL: {fightUrl}");

            var options = new ChromeOptions();
            //options.AddArgument("--headless");

            using var driver = new ChromeDriver(options);
            try
            {
                LoggerService.LogInfo("[Parse] Navigating to page...");
                driver.Navigate().GoToUrl(fightUrl);
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                wait.Until(ExpectedConditions.ElementExists(By.CssSelector("table tbody tr")));
                var rows = driver.FindElements(By.CssSelector("table tbody tr"));

                LoggerService.LogInfo("[Parse] Page loaded, rows found: " + rows.Count);
                if (rows.Count == 0)
                {
                    LoggerService.LogWarning("[ParseFromGuildPage] В таблице нет строк — возможно страница изменилась.");
                }

                int rowIndex = 0;
                foreach (var row in rows)
                {
                    rowIndex++;
                    try
                    {
                        var cols = row.FindElements(By.TagName("td"));
                        if (cols.Count < 8) continue;

                        var col1Text = cols[1].Text?.Trim() ?? string.Empty;
                        var lines = col1Text
                            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim())
                            .Where(s => !string.IsNullOrEmpty(s))
                            .ToArray();

                        string bossName = string.Empty;
                        var anchors = cols[1].FindElements(By.CssSelector("a.q.truncate"));
                        if (anchors.Count > 0) bossName = anchors[0].Text?.Trim() ?? string.Empty;
                        if (string.IsNullOrEmpty(bossName) && lines.Length > 0) bossName = lines[0];
                        string raidName = lines.Length > 1 ? lines[1] : string.Empty;

                        var countsText = cols[4].Text?.Trim() ?? string.Empty;
                        var countLines = countsText
                            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim())
                            .Where(s => !string.IsNullOrEmpty(s))
                            .ToArray();

                        int ParseIntSafe(string s) => int.TryParse(s, out var v) ? v : 0;
                        int dps = countLines.Length > 0 ? ParseIntSafe(countLines[0]) : 0;
                        int healers = countLines.Length > 1 ? ParseIntSafe(countLines[1]) : 0;
                        int tanks = countLines.Length > 2 ? ParseIntSafe(countLines[2]) : 0;
                        int totalPlayers = ParseIntSafe(cols[2].Text?.Trim() ?? string.Empty);

                        TimeSpan fightDuration = TimeSpan.Zero;
                        var durText = cols[5].Text?.Trim() ?? string.Empty;
                        if (!string.IsNullOrEmpty(durText))
                        {
                            var parts = durText.Split(':');
                            if (parts.Length == 2 && int.TryParse(parts[0], out var minutes) && int.TryParse(parts[1], out var seconds))
                            {
                                fightDuration = TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
                            }
                            else
                            {
                                TimeSpan.TryParse(durText, out fightDuration);
                            }
                        }

                        DateTime fightDate = DateTime.MinValue;
                        DateTime.TryParse(cols[6].Text?.Trim() ?? string.Empty, out fightDate);

                        var detailsUrl = string.Empty;
                        try
                        {
                            var detailAnchor = cols[7].FindElement(By.TagName("a"));
                            var detailHref = detailAnchor.GetAttribute("href") ?? string.Empty;
                            LoggerService.LogDebug($"[Parse] Detail URL found: {detailHref}");
                            if (!string.IsNullOrEmpty(detailHref))
                            {
                                var detailUri = new Uri(detailHref, UriKind.RelativeOrAbsolute);
                                if (!detailUri.IsAbsoluteUri)
                                {
                                    detailUri = new Uri(new Uri(_baseUrl), detailUri);
                                }
                                    detailsUrl = detailUri.ToString();
                                LoggerService.LogDebug($"[Parse] Absolute Detail URL: {detailUri}");
                            }
                        }
                        catch (Exception ex)
                        {

                            LoggerService.LogError($"[Parse] Ссылка на подробности боя не найдена в строке #{rowIndex}: " +
                                $"{ex.GetType().Name}: {ex.Message}");
                            LoggerService.LogDebug($"[Parse] Row HTML (td): {cols[7].GetAttribute("outerHTML")}");
                        }

                        kills.Add(new Raid
                        {
                            BossName = bossName ?? string.Empty,
                            RaidName = raidName ?? string.Empty,
                            TotalPlayers = totalPlayers,
                            Tanks = tanks,
                            Healers = healers,
                            Dps = dps,
                            FightDuration = fightDuration,
                            FightDate = fightDate,
                            DetailsUrl = detailsUrl
                        });
                    }
                    catch (Exception exRow)
                    {
                        try
                        {
                            LoggerService.LogError($"[ParseFromGuildPage] Ошибка при разборе строки #{rowIndex}: {exRow.GetType().Name}: {exRow.Message}");
                            LoggerService.LogDebug($"[ParseFromGuildPage] Row HTML (tr) #{rowIndex}: {row.GetAttribute("outerHTML")}");
                        }
                        catch
                        {
                            LoggerService.LogError($"[ParseFromGuildPage] Ошибка при попытке вывести outerHTML строки #{rowIndex}.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"[Parse] Ошибка при парсинге: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                LoggerService.LogInfo("[Parse] Driver finished.");
            }

            LoggerService.LogInfo($"Всего найдено {kills.Count} записей");

            foreach (var kill in kills)
            {
                LoggerService.LogDebug($"[ParseFromGuildPage] Parsed kill: Boss={kill.BossName}, Raid={kill.RaidName} {kill.TotalPlayers}, " +
                                      $"Date={kill.FightDate:dd.MM.yyyy HH:mm:ss}, " +
                                      $"(Tanks={kill.Tanks}, Healers={kill.Healers}, DPS={kill.Dps}), " +
                                      $"Duration={kill.FightDuration:mm\\:ss}" +
                                      $"\n{kill.DetailsUrl}");
            }

            LoggerService.LogDebug(kills.Count.ToString());
            return kills;
        }
    }
}