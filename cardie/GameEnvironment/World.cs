namespace Cardie.GameEnvironment
{
    public class World
    {
        public List<CardInstance> Deck { get; } = new();
        public Player Player { get; }

        public World() => Player = new(this);
    }
}
