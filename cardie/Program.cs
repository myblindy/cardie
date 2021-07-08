using Cardie;
using Cardie.GameEnvironment;

var world = new World
{
    Player = { HealthMaximum = 40, Health = 40, ManaMaximum = 3 },
    Deck =
    {
        Card.All["FireBlast"].CreateInstance(),
        Card.All["FireBlast"].CreateInstance(),
        Card.All["FireBlast"].CreateInstance(),
        Card.All["PoisonedStrike"].CreateInstance(),
        Card.All["PoisonedStrike"].CreateInstance(),
        Card.All["PoisonedStrike"].CreateInstance(),
        Card.All["Nimble"].CreateInstance(),
        Card.All["LifeBolt"].CreateInstance(),
        Card.All["Block"].CreateInstance(),
        Card.All["Block"].CreateInstance(),
        Card.All["Block"].CreateInstance(),
        Card.All["Block"].CreateInstance(),
        Card.All["HealingTouch"].CreateInstance(),
        Card.All["HealingTouch"].CreateInstance(),
    }
};

var encounter = new Encounter(world, new[] { Unit.All["FungiBeast"].CreateInstance(world), Unit.All["FungiBeast"].CreateInstance(world) });
EndTurn(world, encounter, initial: true);
Globals.Log = s => Console.WriteLine($"  - {s}");

bool quit = false;
do
{
    Render(world, encounter);
    var cardToCast = SelectNumber("Select card to cast (0 to end turn)", 0, encounter.Hand.Count);
    if (cardToCast is not null)
    {
        if (cardToCast == -1) { EndTurn(world, encounter, initial: false); continue; }

        var card = encounter.Hand[cardToCast.Value];
        if (card.CardTemplate.RequiresTarget)
        {
            var enemyToTarget = SelectNumber("Select enemy to target", 1, encounter.Units.Count);
            if (enemyToTarget is not null)
            {
                if (card.Cast(world.Player, encounter.Units[enemyToTarget.Value]))
                    encounter.DiscardCard(card);
            }
        }
        else
        {
            if (card.Cast(world.Player, null))
                encounter.DiscardCard(card);
        }
    }
} while (!quit);

static ConsoleColor ConsoleColorFromPercentage(double d) => d switch
{
    <= .33 => ConsoleColor.Red,
    <= .66 => ConsoleColor.Yellow,
    _ => ConsoleColor.Green,
};

static void Write(ConsoleColor color, string s) { Console.ForegroundColor = color; Console.Write(s); }
static void WriteLine(ConsoleColor color, string s) { Write(color, s); Console.WriteLine(); }

static void EndTurn(World world, Encounter encounter, bool initial)
{
    if (!initial)
    {
        Console.WriteLine();
        Console.WriteLine("End of Turn Actions:");
    }

    world.Player.EndTurn();
    if (!initial)
        encounter.EndTurn();

    if (!initial)
    {
        Console.WriteLine();
        Console.WriteLine("Press Enter to continue to the next turn.");
        while (Console.ReadKey().Key != ConsoleKey.Enter) { }
    }

    world.Player.Block = 0;
    world.Player.Mana = world.Player.ManaMaximum;
    encounter.DrawCards(6, discardFirst: true);
}

static int? SelectNumber(string msg, int min, int max)
{
    Write(ConsoleColor.White, $"{msg}: ");
    return int.TryParse(Console.ReadLine(), out var val) && val >= min && val <= max ? val - 1 : null;
}

static void Render(World world, Encounter encounter)
{
    Console.Clear();

    Write(ConsoleColor.White, "Player [");
    Write(ConsoleColorFromPercentage((double)world.Player.Health / world.Player.HealthMaximum), $"{world.Player.Health:000}");
    Write(ConsoleColor.White, "/");
    Write(ConsoleColor.Green, $"{world.Player.HealthMaximum:000} ");
    Write(Card.ManaCostColor, $"{world.Player.Mana} ");
    Write(Card.BlockCostColor, $"{world.Player.Block}");
    WriteLine(ConsoleColor.White, "]");
    RenderBuffs(world.Player);
    Console.WriteLine();

    int idx = 0;
    WriteLine(ConsoleColor.White, "Units:");
    foreach (var unit in encounter.Units)
    {
        Write(ConsoleColor.Cyan, $"{++idx:00}");
        Write(ConsoleColor.White, ". [");
        Write(ConsoleColorFromPercentage((double)unit.Health / unit.HealthMaximum), $"{unit.Health:000}");
        Write(ConsoleColor.White, "/");
        Write(ConsoleColor.Green, $"{unit.HealthMaximum:000}");
        Write(ConsoleColor.White, "] ");
        Write(ConsoleColor.Yellow, unit.TemplateUnit.Name);
        WriteLine(ConsoleColor.White, $". {unit.Intent}");
        RenderBuffs(unit);
    }

    idx = 0;
    Console.WriteLine();
    Write(ConsoleColor.White, "Cards [Deck: ");
    Write(ConsoleColor.Cyan, $"{encounter.Deck.Count:00}");
    Write(ConsoleColor.White, " Discard: ");
    Write(ConsoleColor.Cyan, $"{encounter.Discard.Count:00}");
    WriteLine(ConsoleColor.White, "]:");
    foreach (var card in encounter.Hand)
    {
        Write(ConsoleColor.Cyan, $"{++idx:00}");
        Write(ConsoleColor.White, " [");
        Write(Card.ManaCostColor, $"{card.ManaCost} ");
        Write(Card.HealthCostColor, $"{card.HealthCost} ");
        Write(Card.BlockCostColor, $"{card.BlockCost}");
        Write(ConsoleColor.White, "] ");
        Write(ConsoleColor.Yellow, card.CardTemplate.Name);
        WriteLine(ConsoleColor.White, $": {card.GetDescription(world.Player)}");
    }

    Console.WriteLine();
}

static void RenderBuffs(UnitInstance unitInstance)
{
    foreach (var buff in unitInstance.Buffs.Where(b => b.Stacks > 0))
    {
        Write(ConsoleColor.White, $"  - {buff.Stacks}x");
        WriteLine(ConsoleColor.Yellow, $"{buff.Name}");
    }
}