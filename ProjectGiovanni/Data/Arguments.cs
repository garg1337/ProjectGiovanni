using System.Collections.Generic;
using POGOProtos.Enums;

namespace ProjectGiovanni.Data
{
    public class Arguments
    {
        //PTC Login Info
        public string PTCUsername { get; set; }
        public string PTCPassword { get; set; }

        //The center of the scan
        public Location ScanLocation { get; set; }

        //Time delay between requests in scan threads in seconds
        public int ScanDelay { get; set; }

        //Time delay between each scan thread loop in seconds
        public int ThreadDelay { get; set; }

        //Steps
        public int StepLimit { get; set; }

        //Time delay between each login attempt in seconds
        public int LoginDelay { get; set; }

        //Number of search threads
        public int NumThreads { get; set; }

        public List<Credentials> Credentials { get; set; }

        //Minimum Seconds Before Pokemon Despawns
        public int MinimumTimeBeforeDespawn { get; set; }

        //Pokemon To Filter out of notifications
        public List<PokemonId> ExcludedPokemon { get; set; }
    }
}
