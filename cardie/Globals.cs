global using Cardie.Cards;
global using Cardie.Support;
global using Cardie.Units;
global using System;
global using System.Collections;
global using System.Collections.Generic;
global using System.Linq;
global using System.Text;
global using System.Text.RegularExpressions;
global using System.Threading.Tasks;
global using System.Collections.ObjectModel;
global using System.CodeDom.Compiler;
using Cardie.GameEnvironment;

namespace Cardie
{
    public static class Globals
    {
        public static Action<string>? Log { get; set; }
    }
}

public class FungiBeastInstance1 : UnitInstance
{
    public FungiBeastInstance1(World world, Unit unit) : base(world, unit)
    {
        SelectNextAbility();
    }

    static readonly WeightedRandomItem<(Action<FungiBeastInstance1, Player>, Func<string>)> weightedRandomAction = new(
        (((i, p) =>
        {
            var b = i.Buffs.Sum(w => w.HealthDamageAdd);
            //p.Health -= Math.Max(0, 4 + i.Buffs.Sum(w => w.HealthDamageAdd) - p.Block);
            //Globals.Log?.Invoke($"{i.TemplateUnit.Name} dealt {Math.Max(0, 4 + i.Buffs.Sum(w => w.HealthDamageAdd) - p.Block)} damage to the player (and {Math.Min(4 + i.Buffs.Sum(w => w.HealthDamageAdd), p.Block)} blocked).");
        },
                () => /*$"Intends to attack for {4 + i.Buffs.Sum(w => w.HealthDamageAdd)} Health."*/""), 3)
    );

    Action<FungiBeastInstance1, Player> selectedAbilityAction;
    Func<string> selectedAbilityDescription;

    private void SelectNextAbility() => (selectedAbilityAction, selectedAbilityDescription) = weightedRandomAction.GetRandomItem(ThreadSafeRandom.ThisThreadsRandom);

    public override string Intent => selectedAbilityDescription();

    public override void EndTurn()
    {
        base.EndTurn();
        selectedAbilityAction(this, world.Player);
        SelectNextAbility();
    }
}
