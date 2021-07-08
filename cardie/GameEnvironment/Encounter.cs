namespace Cardie.GameEnvironment
{
    public class Encounter
    {
        readonly World world;
        readonly List<CardInstance> deck, hand = new(), discard = new();
        readonly List<UnitInstance> units = new();

        public ReadOnlyCollection<UnitInstance> Units { get; }
        public ReadOnlyCollection<CardInstance> Deck { get; }
        public ReadOnlyCollection<CardInstance> Hand { get; }
        public ReadOnlyCollection<CardInstance> Discard { get; }

        public Encounter(World world, IEnumerable<UnitInstance> units)
        {
            (this.world, deck) = (world, new(world.Deck));
            (Units, Deck, Hand, Discard) = (new(this.units), new(deck), new(hand), new(discard));
            deck.Shuffle();
            this.units.AddRange(units);
        }

        public void DrawCards(int count, bool discardFirst)
        {
            if (discardFirst)
                DiscardHand();

            while (count-- > 0)
                DrawCard();
        }

        public CardInstance? DrawCard()
        {
            if (deck.Count == 0 && discard.Count > 0)
            {
                deck.AddRange(discard);
                discard.Clear();
                deck.Shuffle();
            }
            else if (deck.Count == 0)
                return null;

            var card = deck[0];
            hand.Add(card);
            deck.RemoveAt(0);
            return card;
        }

        public void DiscardCard(CardInstance cardInstance)
        {
            hand.Remove(cardInstance);
            discard.Add(cardInstance);
        }

        public void DiscardHand()
        {
            hand.Reverse();
            discard.AddRange(hand);
            hand.Clear();
        }

        public void EndTurn()
        {
            Units.ForEach(w => w.EndTurn());
            units.Where(w => w.Health <= 0).ForEach(w => Globals.Log?.Invoke($"{w.TemplateUnit.Name} has died."));
            units.RemoveAll(w => w.Health <= 0);
        }
    }
}
