using System.Text.Json;
using DPS_bot.Models;
using DPS_bot.Services;

namespace DPS_bot.Repositories
{
    public class FightRepository
    {
        private readonly string _filePath;
        private readonly List<Fight> _fights = new();
        private readonly HashSet<string> _knownKeys = new();

        public FightRepository(string filePath)
        {
            _filePath = filePath;
            Load();
        }

        public IReadOnlyList<Fight> All => _fights;

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
                var loaded = JsonSerializer.Deserialize<List<Fight>>(json);
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

        public bool Contains(Fight fight) => _knownKeys.Contains(fight.GetKey());

        public void Add(Fight fight)
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
                var json = JsonSerializer.Serialize(_fights, new JsonSerializerOptions { WriteIndented = true });
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