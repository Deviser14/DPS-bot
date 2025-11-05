using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DPS_bot.Models
{
    internal class Fight
    {
        public string RaidName { get; set; }
        public int PlayersCount { get; set; }
        public DateTime FightDate { get; set; }
        public string BossName { get; set; }
        public List<Player> Players { get; set; }
        public int BossPulls { get; set; }
        public List<Item> Loot { get; set; }
    }
}
