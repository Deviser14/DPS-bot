using DPS_bot.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System;
using System.Text.RegularExpressions;


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
            // Пока пусто — можно передать конфиг при необходимости
        }

        /// <summary>
        /// Точка входа: создаёт WebDriver, открывает страницу и запускает парсинг.
        /// Логирует начало/конец работы и ошибки.
        /// </summary>
        public void Parse(string fightUrl)
        {
            if (string.IsNullOrEmpty(fightUrl) || !Uri.IsWellFormedUriString(fightUrl, UriKind.Absolute))
            {
                throw new ArgumentException("Некорректный URL", nameof(fightUrl));
            }

            Console.WriteLine($"[Parse] Start parsing URL: {fightUrl}");

            ChromeDriver? driver = null;
            try
            {
                var options = new ChromeOptions();
                //options.AddArgument("--headless");
                driver = new ChromeDriver(options);

                Console.WriteLine("[Parse] Navigating to page...");
                driver.Navigate().GoToUrl(fightUrl);

                Console.WriteLine("[Parse] Page loaded, parsing fight results...");
                Players = ParseFightResults(driver);
                Console.WriteLine($"[Parse] Parsed players count: {Players?.Count ?? 0}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Parse] Ошибка при парсинге: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                // Убедимся, что браузер закрыт
                driver?.Quit();
                Console.WriteLine("[Parse] Driver quit, finished.");
            }
        }

        /// <summary>
        /// Парсит страницу с результатами боя и возвращает список игроков.
        /// Логирует прогресс: ожидание таблицы, количество строк, детали заголовков и ошибок.
        /// </summary>
        public List<Player> ParseFightResults(ChromeDriver driver)
        {
            var players = new List<Player>();

            Console.WriteLine("[ParseFightResults] Waiting for table rows to appear...");
            // 2. Ждём появления таблицы игроков
            try
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(WAIT_TIMEOUT_SECONDS));
                wait.Until(ExpectedConditions.ElementExists(By.CssSelector("table tbody tr")));
                Console.WriteLine("[ParseFightResults] Table rows detected by wait.");
            }
            catch (WebDriverTimeoutException)
            {
                Console.WriteLine($"[ParseFightResults] Таблица не появилась за {WAIT_TIMEOUT_SECONDS} секунд. Пробуем ещё раз через {RETRY_DELAY_SECONDS} сек...");
                Thread.Sleep(RETRY_DELAY_SECONDS * 1000);
                // Повторная попытка
                var retryRows = driver.FindElements(By.CssSelector("table tbody tr"));
                if (retryRows.Count == 0)
                {
                    Console.WriteLine("[ParseFightResults] Таблица всё ещё не найдена. Прерываем.");
                    return new List<Player>(); // Исправлено: возвращаем пустой список вместо null
                }

                Console.WriteLine($"[ParseFightResults] Найдено строк после повторной попытки: {retryRows.Count}");
            }

            // 3. Находим строки таблицы
            var rows = driver.FindElements(By.CssSelector("table tbody tr"));
            Console.WriteLine($"[ParseFightResults] Всего строк в таблице: {rows.Count}");


            int rowIndex = 0;
            foreach (var row in rows)
            {
                rowIndex++;
                try
                {
                    var cols = row.FindElements(By.TagName("td"));
                    if (cols.Count == headerColCount)
                    {
                        // Это строка заголовка / итогов
                        string overallUvs = cols[3].Text; // Общий УВС
                        string overallIvs = cols[5].Text; // Общий ИВС
                        Console.WriteLine($"[ParseFightResults] Заголовок/итог на строке #{rowIndex}: УВС={overallUvs}, ИВС={overallIvs}");
                    }
                    else if (cols.Count == playerColCount)
                    {
                        players.Add(ParsePlayerRow(cols));
                    }
                    else
                    {
                        Console.WriteLine($"[ParseFightResults] Пропущена строка #{rowIndex} с {cols.Count} колонками (ожидалось {playerColCount} или {headerColCount}).");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ParseFightResults] Ошибка при обработке строки #{rowIndex}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            Console.WriteLine("[ParseFightResults] Парсинг завершён.");
            return players;
        }

        /// <summary>
        /// Формирует объект Player из набора колонок таблицы.
        /// Логирует найденные значения и ошибки при получении отдельных полей.
        /// </summary>
        private Player ParsePlayerRow(IReadOnlyCollection<IWebElement> cols)
        {
            string? className = null;
            try
            {
                // Находим span с фоновым изображением (иконка класса).
                // Если элемент не найден, будем логировать и продолжим.
                var span = cols.ElementAt(1).FindElement(By.CssSelector(".rounded.bg-background"));
                className = ParseBackgroundImageInfo(span);
            }
            catch (NoSuchElementException)
            {
                // Элемент с иконкой класса отсутствует — логируем outerHTML для диагностики
                try
                {
                    var outer = cols.ElementAt(1).GetAttribute("outerHTML");
                    Console.WriteLine("[ParsePlayerRow] Элемент класса не найден. Колонка (outerHTML): " + outer);
                }
                catch
                {
                    Console.WriteLine("[ParsePlayerRow] Элемент класса не найден и не удалось получить outerHTML.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ParsePlayerRow] Ошибка при получении класса: {ex.GetType().Name}: {ex.Message}");
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

            Console.WriteLine($"[ParsePlayerRow] Parsed player: {player.Name}, Class={player.Class}, Guild={player.Guild}, iLvl={player.ILvl}");
            return player;
        }

        /// <summary>
        /// Парсит URL из атрибута style background-image и возвращает имя файла без расширения.
        /// Комментарии и логи помогают понять, какие строки приходят в style и почему парсинг может не сработать.
        /// </summary>
        public static string? ParseBackgroundImageInfo(IWebElement span)
        {
            // Получаем значение атрибута style (может быть задано inline-style или computed style)
            var style = span?.GetAttribute("style");
            if (string.IsNullOrEmpty(style))
            {
                Console.WriteLine("[ParseBackgroundImageInfo] Элемент не содержит атрибут style или он пустой.");
                return null;
            }

            // Регулярное выражение находит содержимое url(...) — безопасно для большинства случаев.
            // Пример matched groups: input "url('/img/p.png')" -> group1 = "/img/p.png"
            var match = Regex.Match(style, @"url\(['""]?(.*?)['""]?\)");
            if (!match.Success)
            {
                Console.WriteLine($"[ParseBackgroundImageInfo] Не удалось найти URL в style. Style: {style}");
                return null;
            }

            string rawUrl = match.Groups[1].Value;

            // В текущей реализации просто берём имя файла без расширения из строки (может работать и для абсолютных и относительных URL).
            // Если rawUrl содержит параметры или data-uri, Path.GetFileNameWithoutExtension вернёт то, что сможет.
            string fileName = Path.GetFileNameWithoutExtension(rawUrl);

            return string.IsNullOrEmpty(fileName) ? null : fileName;
        }
    }
}