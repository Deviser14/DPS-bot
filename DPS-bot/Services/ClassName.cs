using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DPS_bot.Services
{
    public static class ClassName
    {
        public static readonly Dictionary<string, string> ById = new()
        {
            { "warrior", "Воин" },
            { "paladin", "Паладин" },
            { "hunter", "Охотник" },
            { "rogue", "Разбойник" },
            {"priest", "Жрец" },
            { "deathknight", "Рыцарь смерти" },
            { "shaman", "Шаман" },
            { "mage", "Маг" },
            { "warlock", "Чернокнижник" },
            { "druid", "Друид" }
        };
    }
}
