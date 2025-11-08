using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DPS_bot.Config
{
    public class AppConfig
    {
        public BotConfig Bot { get; set; }
        public DiscordConfig Discord { get; set; }
        public DomenConfig Domen { get; set; }
        public LoggerConfig Logger { get; set; }

    }
}
