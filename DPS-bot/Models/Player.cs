using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace DPS_bot.Models
{
    internal class Player
    {
        public string? Name { get; set; }
        public string? Class { get; set; }
        public string? Spec { get; set; }
        public string? Guild { get; set; }
        public string? ILvl { get; set; }
        public string? Category { get; set; }
        public string? Ivs { get; set; }
        public string? Uvs { get; set; }
    }
}
