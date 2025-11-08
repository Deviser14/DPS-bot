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
    internal class DpsParser
    {
        // Количество колонок в строке игрока и в заголовке таблицы
        int playerColCount = 12; // Количество колонок для игроков с гильдией
        int headerColCount = 7; // Количество колонок для заголовка

        // Конфигурация таймаутов
        private const int WAIT_TIMEOUT_SECONDS = 10;
        private const int RETRY_DELAY_SECONDS = 3;
        internal List<Player> Players { get; private set; } = new List<Player>();

        public DpsParser()
        {
        }

        public void Parse(string fightUrl)
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
                Players = ParseFightResults(driver);
                LoggerService.LogInfo($"[Parse] Parsed players count: {Players?.Count ?? 0}");
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
        }

        public List<Player> ParseFightResults(ChromeDriver driver)
        {
            var players = new List<Player>();

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
                    return new List<Player>();
                }

                LoggerService.LogInfo($"[ParseFightResults] Найдено строк после повторной попытки: {retryRows.Count}");
            }

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
                        string overallUvs = cols[3].Text;
                        string overallIvs = cols[5].Text;
                        LoggerService.LogDebug($"[ParseFightResults] Заголовок/итог на строке #{rowIndex}: УВС={overallUvs}, ИВС={overallIvs}");
                    }
                    else if (cols.Count == playerColCount)
                    {
                        players.Add(ParsePlayerRow(cols));
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
            return players;
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

            var player = new Player
            {
                Name = cols.ElementAt(1).Text,
                Class = className ?? string.Empty,
                Guild = cols.ElementAt(3).Text,
                ILvl = cols.ElementAt(5).Text,
                Uvs = cols.ElementAt(8).Text,
                Ivs = cols.ElementAt(10).Text
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
    }
}