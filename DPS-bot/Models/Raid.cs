using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DPS_bot.Models
{
    public class Raid
    {

        public string BossName { get; set; } = string.Empty;
        public string RaidName { get; set; } = string.Empty;
        public int TotalPlayers { get; set; }
        public int Tanks { get; set; }
        public int Healers { get; set; }
        public int Dps { get; set; }
        public int OverallDps { get; set; }
        public int OverallHps { get; set; }
        public DateTime FightDate { get; set; } = DateTime.MinValue;
        public TimeSpan FightDuration { get; set; } = TimeSpan.Zero;
        public List<Player> Players { get; set; } = new List<Player>();
        public int Attempts { get; set; }
        public List<Item> Loot { get; set; } = new List<Item>();
        public string DetailsUrl { get; set; } = string.Empty;
        public string GetKey() =>
            $"{BossName}_{RaidName}({TotalPlayers})_{DetailsUrl}";
    }
}
