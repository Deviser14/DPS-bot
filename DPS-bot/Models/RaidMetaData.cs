namespace DPS_bot.Models
{
    public class RaidMetaData
    {
        public int Attempts { get; set; }
        public DateTime KilledAt { get; set; } = DateTime.MinValue;
        public List<Item> Loot { get; set; } = new List<Item>();
        public List<Player> Players { get; set; } = new List<Player>();
        
        public int OverallDps { get; set; }
        public int OverallHps { get; set; }
    }
}