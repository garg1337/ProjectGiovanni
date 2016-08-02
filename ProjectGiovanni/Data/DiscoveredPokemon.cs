using System;
using POGOProtos.Enums;

namespace ProjectGiovanni.Data
{
    public class DiscoveredPokemon : IEquatable<DiscoveredPokemon>
    {
        public PokemonId Kind { get; set; }
        public Location Location { get; set; }
        public int TimeToDespawn { get; set; }

        public bool Equals(DiscoveredPokemon other)
        {
            // time to despawn on one scan may not equal that of another scan
            return Kind == other.Kind && Location.Equals(other.Location);
        }
    }
}
