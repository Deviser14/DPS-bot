using DPS_bot.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Diagnostics;

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
        private const int LAZY_LOAD_TIMEOUT_SECONDS = 30;
        private const int LAZY_LOAD_POLL_MS = 500;

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
                //driver.Manage().Window.Size = new System.Drawing.Size(1920, 3000);

                LoggerService.LogInfo("[Parse] Navigating to page...");
                driver.Navigate().GoToUrl(fightUrl);

                // Оптимальная быстрая подгрузка строк (инкрементальный скролл без smooth)
                LoggerService.LogInfo("[Parse] Ensuring all rows are loaded (fast incremental scroll)...");
                EnsureAllRowsLoaded(driver);

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

        /// <summary>
        /// Быстрый инкрементальный скролл по ближайшему scrollable-родителю таблицы или по окну.
        /// Шаг достаточен, чтобы триггерить lazy-load, но не делает резких прыжков в конец.
        /// После каждого шага делается короткий poll по количеству строк; останавливаемся при стабильности.
        /// Для тултипов — dispatch hover без лишних scrollIntoView.
        /// </summary>
        private void EnsureAllRowsLoaded(ChromeDriver driver)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                int lastCount = -1;
                int stableRounds = 0;
                int requiredStableRounds = 3;

                // Небольшой initial wait на появление таблицы
                try
                {
                    var shortWait = new WebDriverWait(driver, TimeSpan.FromSeconds(5));
                    shortWait.Until(ExpectedConditions.ElementExists(By.CssSelector("table tbody tr")));
                }
                catch (WebDriverTimeoutException) { }

                while (sw.Elapsed.TotalSeconds < LAZY_LOAD_TIMEOUT_SECONDS)
                {
                    var rows = driver.FindElements(By.CssSelector("table tbody tr"));
                    int count = rows.Count;
                    LoggerService.LogDebug($"[EnsureAllRowsLoaded] Rows count={count}, lastCount={lastCount}, elapsed={sw.Elapsed.TotalSeconds:F1}s");

                    if (count == lastCount)
                    {
                        stableRounds++;
                        if (stableRounds >= requiredStableRounds)
                        {
                            LoggerService.LogInfo($"[EnsureAllRowsLoaded] Rows stabilized after {sw.Elapsed.TotalSeconds:F1}s, count={count}");
                            break;
                        }
                    }
                    else
                    {
                        lastCount = count;
                        stableRounds = 0;
                    }

                    try
                    {
                        var js = (IJavaScriptExecutor)driver;

                        // Инкрементальный немедленный скролл; возвращаем true, если был изменён scroll
                        var didScrollObj = js.ExecuteScript(@"
                            (function(){
                                var tbl = document.querySelector('table');
                                var step = Math.max(Math.floor(window.innerHeight * 0.6), 300);
                                if(!tbl){
                                    window.scrollBy(0, step);
                                    return true;
                                }
                                var el = tbl;
                                while(el && el !== document.body){
                                    var style = window.getComputedStyle(el);
                                    var overflowY = style.overflowY || style['-webkit-overflow-scrolling'] || '';
                                    if(overflowY === 'auto' || overflowY === 'scroll' || overflowY === 'overlay'){
                                        var bottomDelta = el.scrollHeight - (el.scrollTop + el.clientHeight);
                                        if(bottomDelta > 5){
                                            var inc = Math.max(100, Math.floor(el.clientHeight * 0.9));
                                            el.scrollTop = Math.min(el.scrollTop + inc, el.scrollHeight - el.clientHeight);
                                            return true;
                                        }
                                        return false;
                                    }
                                    el = el.parentElement;
                                }
                                window.scrollBy(0, step);
                                return true;
                            })();
                        ");

                        bool jsDidScroll = didScrollObj is bool b && b;
                        LoggerService.LogDebug($"[EnsureAllRowsLoaded] jsDidScroll={jsDidScroll}");

                        // Короткий опрос на появление новых строк — быстрый и надежный
                        Thread.Sleep(250);

                        // Минимальный hover-триггер для s-tooltip (без лишних scrollIntoView)
                        try
                        {
                            var lazyTooltips = driver.FindElements(By.CssSelector("span.s-tooltip[data-s-lazy], span.s-tooltip[data-lazy]"));
                            foreach (var tip in lazyTooltips)
                            {
                                try
                                {
                                    // Если tooltip полностью вне viewport — делаем минимальную прокрутку к нему
                                    var inViewportObj = js.ExecuteScript(@"
                                        try{
                                            var r = arguments[0].getBoundingClientRect();
                                            var h = window.innerHeight || document.documentElement.clientHeight;
                                            return (r.top >= 0 && r.bottom <= h);
                                        }catch(e){ return false; }
                                    ", tip);
                                    var inViewport = inViewportObj is bool vb && vb;
                                    if (!inViewport)
                                    {
                                        // прокручиваем небольшим шагом к элементу (не jump-to-end)
                                        js.ExecuteScript("arguments[0].scrollIntoView({block:'nearest'});", tip);
                                        Thread.Sleep(60);
                                    }

                                    // dispatch hover events — обычно достаточно для инициализации тултипа
                                    js.ExecuteScript(@"
                                        try{
                                            arguments[0].dispatchEvent(new MouseEvent('mouseenter', {bubbles:true, cancelable:true}));
                                            arguments[0].dispatchEvent(new MouseEvent('mouseover', {bubbles:true, cancelable:true}));
                                        }catch(e){}
                                    ", tip);

                                    Thread.Sleep(30);
                                }
                                catch { }
                            }
                        }
                        catch (Exception ex)
                        {
                            LoggerService.LogWarning($"[EnsureAllRowsLoaded] Tooltip hover error: {ex.GetType().Name}: {ex.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerService.LogWarning($"[EnsureAllRowsLoaded] JS scroll error: {ex.GetType().Name}: {ex.Message}");
                    }

                    Thread.Sleep(LAZY_LOAD_POLL_MS);
                }

                if (sw.Elapsed.TotalSeconds >= LAZY_LOAD_TIMEOUT_SECONDS)
                {
                    LoggerService.LogWarning($"[EnsureAllRowsLoaded] Достигнут таймаут {LAZY_LOAD_TIMEOUT_SECONDS}s. Последнее количество строк = {lastCount}");
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"[EnsureAllRowsLoaded] Ошибка: {ex.GetType().Name}: {ex.Message}");
            }
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
                className = ClassName.ById.TryGetValue(ParseBackgroundImageInfo(span),out string? value)?value:String.Empty;
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

            var spec = ResolveSpecFromElement(cols.ElementAt(4),className);

            var player = new Player
            {
                Name = cols.ElementAt(1).Text,
                Class = className ?? string.Empty,
                Spec = spec ?? string.Empty,
                Guild = cols.ElementAt(3).Text,
                ILvl = cols.ElementAt(5).Text,
                Category = cols.ElementAt(6).Text,
                Dps = cols.ElementAt(8).Text,
                Hps = cols.ElementAt(10).Text
            };

            LoggerService.LogDebug($"[ParsePlayerRow] Parsed player: {player.Name}, Class={player.Class}, Guild={player.Guild}, " +
                $"iLvl={player.ILvl}, Category={player.Category}");
            return player;
        }

        public static string? ParseBackgroundImageInfo(IWebElement span)
        {
            var style = span?.GetAttribute("style");
            if (string.IsNullOrEmpty(style))
            {
                LoggerService.LogDebug("[ParseBackgroundImageInfo] Элемент не содержит атрибута style или он пустой.");
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
        private string? ResolveSpecFromElement(IWebElement playerCell,string? className)
        {
            try
            {
                var specSpan = playerCell.FindElement(By.CssSelector(".rounded.bg-background"));
                var iconName = ParseBackgroundImageInfo(specSpan);
                Console.WriteLine($"\"{iconName}\"");
                if (string.IsNullOrEmpty(iconName))
                    return null;
                if (className == "warrior" && iconName.ToLowerInvariant() == "ability_rogue_eviscerate")
                {
                    return "Оружие";
                }
                else
                {
                    return SpecIconMap.ByIconName.TryGetValue(iconName.ToLowerInvariant(), out var spec)
                        ? spec
                        : null;
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"[ResolveSpecFromElement] Не удалось извлечь специализацию: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }
    }
}   