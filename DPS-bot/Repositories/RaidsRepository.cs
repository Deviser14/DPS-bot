using DPS_bot.Models;
using DPS_bot.Services;
using OpenQA.Selenium.Remote;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DPS_bot.Repositories
{
    public class RaidsRepository
    {
        private readonly string _filePath;
        private readonly List<Raid> _fights = new();
        private readonly HashSet<string> _knownKeys = new();

        public RaidsRepository(string filePath)
        {
            _filePath = filePath;
            Load();
        }

        public IReadOnlyList<Raid> All => _fights;

        private void Load()
        {
            if (!File.Exists(_filePath))
            {
                LoggerService.LogWarning($"Файл боёв не найден: {_filePath}");
                return;
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                var loaded = JsonSerializer.Deserialize<List<Raid>>(json);
                if (loaded != null)
                {
                    _fights.AddRange(loaded);
                    foreach (var fight in loaded)
                        _knownKeys.Add(fight.GetKey());

                    LoggerService.LogInfo($"Загружено {loaded.Count} боёв из {_filePath}");
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"Ошибка загрузки боёв: {ex.Message}");
            }
        }

        public bool Contains(Raid fight) => _knownKeys.Contains(fight.GetKey());

        public void Add(Raid fight)
        {
            var key = fight.GetKey();
            if (_knownKeys.Contains(key)) return;

            _fights.Add(fight);
            _knownKeys.Add(key);
            LoggerService.LogInfo($"Добавлен новый бой: {key}");
        }

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_fights, new JsonSerializerOptions { WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                File.WriteAllText(_filePath, json);
                LoggerService.LogInfo($"Сохранено {_fights.Count} боёв в {_filePath}");
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"Ошибка сохранения боёв: {ex.Message}");
            }
        }
    }
}