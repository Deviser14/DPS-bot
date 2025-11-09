using DPS_bot.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading;

namespace DPS_bot.Services
{
    internal class RaidInfoParser
    {
        // Количество колонок в строке игрока и в заголовке таблицы
        int playerColCount = 12; // Количество колонок для игроков с гильдией
        int headerColCount = 7; // Количество колонок для заголовка

        // Конфигурация таймаутов
        private const int WAIT_TIMEOUT_SECONDS = 10;
        private const int RETRY_DELAY_SECONDS = 3;
        public RaidMetaData RaidMetaData { get; private set; } = new ();

        public RaidInfoParser()
        {
        }

        public RaidMetaData Parse(string fightUrl)
        {
            if (string.IsNullOrEmpty(fightUrl) || !Uri.IsWellFormedUriString(fightUrl, UriKind.Absolute))
            {
                throw new ArgumentException("Некорректный URL", nameof(fightUrl));
            }

            LoggerService.LogInfo($"[Parse] Start parsing URL: {fightUrl}");

            ChromeDriver? driver = null;
            try
            {
                var options = new ChromeOptions();
                //options.AddArgument("--headless");
                driver = new ChromeDriver(options);

                LoggerService.LogInfo("[Parse] Navigating to page...");
                driver.Navigate().GoToUrl(fightUrl);

                LoggerService.LogInfo("[Parse] Page loaded, parsing fight results...");
                RaidMetaData = ParseFightResults(driver);
                LoggerService.LogInfo($"[Parse] Parsed players count: {RaidMetaData.Players?.Count ?? 0}");
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"[Parse] Ошибка при парсинге: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                driver?.Quit();
                LoggerService.LogInfo("[Parse] Driver quit, finished.");
            }
            return RaidMetaData;
        }

        public RaidMetaData ParseFightResults(ChromeDriver driver)
        {
            var metaData = new RaidMetaData();

            LoggerService.LogInfo("[ParseFightResults] Waiting for table rows to appear...");
            try
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(WAIT_TIMEOUT_SECONDS));
                wait.Until(ExpectedConditions.ElementExists(By.CssSelector("table tbody tr")));
                LoggerService.LogInfo("[ParseFightResults] Table rows detected by wait.");
            }
            catch (WebDriverTimeoutException)
            {
                LoggerService.LogWarning($"[ParseFightResults] Таблица не появилась за {WAIT_TIMEOUT_SECONDS} секунд. Пробуем ещё раз через {RETRY_DELAY_SECONDS} сек...");
                Thread.Sleep(RETRY_DELAY_SECONDS * 1000);
                var retryRows = driver.FindElements(By.CssSelector("table tbody tr"));
                if (retryRows.Count == 0)
                {
                    LoggerService.LogWarning("[ParseFightResults] Таблица всё ещё не найдена. Прерываем.");
                    return new RaidMetaData();
                }

                LoggerService.LogInfo($"[ParseFightResults] Найдено строк после повторной попытки: {retryRows.Count}");
            }

            var metaContainer = driver.FindElement(By.CssSelector(".flex.flex-col.divide-y"));
            var (killedAt, attempts) = ParseFightMeta(metaContainer);

            metaData.KilledAt = killedAt ?? DateTime.MinValue;
            metaData.Attempts = attempts;
            LoggerService.LogInfo($"[ParseFightResults] Fight meta: KilledAt={metaData.KilledAt}, Attempts={metaData.Attempts}");

            var rows = driver.FindElements(By.CssSelector("table tbody tr"));
            LoggerService.LogInfo($"[ParseFightResults] Всего строк в таблице: {rows.Count}");

            int rowIndex = 0;
            foreach (var row in rows)
            {
                rowIndex++;
                try
                {
                    var cols = row.FindElements(By.TagName("td"));
                    if (cols.Count == headerColCount)
                    {
                        string overallDps = cols[3].Text;
                        string overallHps = cols[5].Text;
                        metaData.OverallDps = int.TryParse(overallDps.Replace(",", "").Replace(" ", ""), out var dps) ? dps : 0;
                        metaData.OverallHps = int.TryParse(overallHps.Replace(",", "").Replace(" ", ""), out var hps) ? hps : 0;
                        LoggerService.LogDebug($"[ParseFightResults] Заголовок/итог на строке #{rowIndex}: УВС={overallDps}, ИВС={overallHps}");
                    }
                    else if (cols.Count == playerColCount)
                    {
                        metaData.Players.Add(ParsePlayerRow(cols));
                    }
                    else
                    {
                        LoggerService.LogDebug($"[ParseFightResults] Пропущена строка #{rowIndex} с {cols.Count} колонками (ожидалось {playerColCount} или {headerColCount}).");
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.LogError($"[ParseFightResults] Ошибка при обработке строки #{rowIndex}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            LoggerService.LogInfo("[ParseFightResults] Парсинг завершён.");
            return metaData;
        }

        private Player ParsePlayerRow(IReadOnlyCollection<IWebElement> cols)
        {
            string? className = null;
            try
            {
                var span = cols.ElementAt(1).FindElement(By.CssSelector(".rounded.bg-background"));
                className = ParseBackgroundImageInfo(span);
            }
            catch (NoSuchElementException)
            {
                try
                {
                    var outer = cols.ElementAt(1).GetAttribute("outerHTML");
                    LoggerService.LogWarning("[ParsePlayerRow] Элемент класса не найден. Колонка (outerHTML): " + outer);
                }
                catch
                {
                    LoggerService.LogWarning("[ParsePlayerRow] Элемент класса не найден и не удалось получить outerHTML.");
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"[ParsePlayerRow] Ошибка при получении класса: {ex.GetType().Name}: {ex.Message}");
            }

            var spec = ResolveSpecFromElement(cols.ElementAt(4));

            var player = new Player
            {
                Name = cols.ElementAt(1).Text,
                Class = className ?? string.Empty,
                Spec = spec ?? string.Empty,
                Guild = cols.ElementAt(3).Text,
                ILvl = cols.ElementAt(5).Text,
                Dps = cols.ElementAt(8).Text,
                Hps = cols.ElementAt(10).Text
            };

            LoggerService.LogDebug($"[ParsePlayerRow] Parsed player: {player.Name}, Class={player.Class}, Guild={player.Guild}, iLvl={player.ILvl}");
            return player;
        }

        public static string? ParseBackgroundImageInfo(IWebElement span)
        {
            var style = span?.GetAttribute("style");
            if (string.IsNullOrEmpty(style))
            {
                LoggerService.LogDebug("[ParseBackgroundImageInfo] Элемент не содержит атрибут style или он пустой.");
                return null;
            }

            var match = Regex.Match(style, @"url\(['""]?(.*?)['""]?\)");
            if (!match.Success)
            {
                LoggerService.LogDebug($"[ParseBackgroundImageInfo] Не удалось найти URL в style. Style: {style}");
                return null;
            }

            string rawUrl = match.Groups[1].Value;
            string fileName = Path.GetFileNameWithoutExtension(rawUrl);

            return string.IsNullOrEmpty(fileName) ? null : fileName;
        }
        public static (DateTime? killedAt, int attempts) ParseFightMeta(IWebElement container)
        {
            DateTime? killedAt = null;
            int attempts = 0;

            try
            {
                var attemptsText = container.FindElement(By.XPath(".//span[contains(text(),'Попытки:')]/following-sibling::span")).Text;
                if (int.TryParse(attemptsText, out var parsedAttempts))
                    attempts = parsedAttempts;
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"[ParseFightMeta] Не удалось извлечь количество попыток: {ex.GetType().Name}: {ex.Message}");
            }

            try
            {
                var killedText = container.FindElement(By.XPath(".//span[contains(text(),'Убит:')]/following-sibling::span")).Text;
                if (DateTime.TryParse(killedText, out var parsedKilled))
                    killedAt = parsedKilled;
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"[ParseFightMeta] Не удалось извлечь дату убийства: {ex.GetType().Name}: {ex.Message}");
            }

            return (killedAt, attempts);
        }
        private string? ResolveSpecFromElement(IWebElement playerCell)
        {
            try
            {
                var specSpan = playerCell.FindElement(By.CssSelector(".rounded.bg-background"));
                var iconName = ParseBackgroundImageInfo(specSpan);
                if (string.IsNullOrEmpty(iconName))
                    return null;

                return SpecIconMap.ByIconName.TryGetValue(iconName.ToLowerInvariant(), out var spec)
                    ? spec
                    : null;
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"[ResolveSpecFromElement] Не удалось извлечь специализацию: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }
    }
}