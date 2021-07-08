namespace Cardie.GameEnvironment
{
    public class PlayerUnit : Unit
    {
        public override string Name => "Player";

        public override double BaseHealth => 0;

        public override double BaseBlock => 0;

        public override UnitInstance CreateInstance(World world) => throw new NotImplementedException();
    }

    public partial class Player : UnitInstance
    {
        public override string Intent => "";

        public Player(World world) : base(world, new PlayerUnit())
        {
        }
    }
}
