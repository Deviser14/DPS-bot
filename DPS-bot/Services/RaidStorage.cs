using System.Text.Json;
using DPS_bot.Models;

namespace DPS_bot.Services
{
    public class RaidStorage
    {
        private readonly string _filePath;
        private Dictionary<string, HashSet<DateTime>> _kills;

        public RaidStorage(string filePath)
        {
            _filePath = filePath;
            Load();
        }

        private void Load()
        {
            if (!File.Exists(_filePath))
            {
                _kills = new();
                return;
            }

            var json = File.ReadAllText(_filePath);
            _kills = JsonSerializer.Deserialize<Dictionary<string, HashSet<DateTime>>>(json)
                     ?? new();
        }

        public bool IsKnown(string bossName, DateTime killDate)
        {
            return _kills.TryGetValue(bossName, out var dates) && dates.Contains(killDate);

        }

        public void AddKill(string bossName, DateTime killDate)
        {
            if (!_kills.ContainsKey(bossName))
                _kills[bossName] = new();

            if (_kills[bossName].Add(killDate))
                Save();
        }

        private void Save()
        {
            var json = JsonSerializer.Serialize(_kills, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
    }
}