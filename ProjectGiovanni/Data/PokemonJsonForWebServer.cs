using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectGiovanni.Data
{
    class PokemonJsonForWebServer
    {
        public int timeleft { get; set; }

        public string name { get; set; }

        public long timestamp { get; set; }

        public double lat { get; set; }

        public double lng { get; set; }

        public int id { get; set; }
    }
}
