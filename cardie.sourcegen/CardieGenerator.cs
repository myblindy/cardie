using Cardie.Support;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Cardie.SourceGen
{
    [Generator]
    class CardieGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            var allResources = Utilities.GetResources(context).ToDictionary(w => w.Name);
            var allCardDefinitions = Utilities.GetCards(context);
            var allUnitDefinitions = Utilities.GetUnits(context);
            var allBuffs = Utilities.GetBuffs(context);

            var allBuffsAddStatFields = allBuffs[0].GetType().GetProperties().Select(pi => pi.Name)
                .Select(w => Regex.Match(w, @$"^({string.Join("|", allResources.Select(r => r.Key))})Add$")).Where(m => m.Success).Select(m => m.Groups[1].Value).ToHashSet();
            var allBuffsDamageAddStatFields = allBuffs[0].GetType().GetProperties().Select(pi => pi.Name)
                .Select(w => Regex.Match(w, @$"^({string.Join("|", allResources.Select(r => r.Key))})DamageAdd$")).Where(m => m.Success).Select(m => m.Groups[1].Value).ToHashSet();

            var resourceBlockers = allResources.Values.Where(r => !string.IsNullOrWhiteSpace(r.BlockedBy)).ToDictionary(r => r.Name, r => r.BlockedBy);

            var sb = new StringBuilder("using Cardie.GameEnvironment; namespace Cardie.Cards { using Cardie.Buffs;");

            string AddBuffStats(string prefix, string resName) => !allBuffsAddStatFields.Contains(resName) ? null :
                $" + {prefix}Buffs.Sum(w => w.{resName}Add)";
            string AddDamageBuffStats(string prefix, string resName) => !allBuffsDamageAddStatFields.Contains(resName) ? null :
                $" + {prefix}Buffs.Sum(w => w.{resName}DamageAdd)";
            string GetDamageCode(string srcPrefix, string dstPrefix, (string name, double qty) res, out string damageDone, out string damageBlocked)
            {
                var damageBuffStats = AddDamageBuffStats(srcPrefix, res.name);
                if (resourceBlockers.TryGetValue(res.name, out var blockedBy))
                {
                    (damageDone, damageBlocked) = ($"Math.Max(0, {res.qty}{damageBuffStats} - {dstPrefix}{blockedBy})", $"Math.Min({res.qty}{damageBuffStats}, {dstPrefix}{blockedBy})");
                    return $"{dstPrefix}{res.name} -= {damageDone};";
                }
                else
                {
                    (damageDone, damageBlocked) = ($"{res.qty}{damageBuffStats}", "0");
                    return $"{dstPrefix}{res.name} -= {damageDone};";
                }
            }

            foreach (var cardDefinition in allCardDefinitions)
            {
                // individual card classes
                var safeName = Utilities.GetSafeName(cardDefinition.Name);
                var costs = Utilities.GetResourcesFromString(cardDefinition.BaseCost).ToList();
                var gains = Utilities.GetResourcesFromString(cardDefinition.ResourceGain).ToList();
                var singleTargetDamage = Utilities.GetResourcesFromString(cardDefinition.SingleTargetDamage).ToList();
                var castBuffs = Utilities.GetBuffsFromString(cardDefinition.CastBuffs).ToList();
                var gainBuffs = Utilities.GetBuffsFromString(cardDefinition.GainBuffs).ToList();

                // actions
                var castDamage = string.IsNullOrWhiteSpace(cardDefinition.SingleTargetDamage) ? ""
                    : string.Concat(singleTargetDamage.Select(dmg => GetDamageCode("player.", "unitInstance!.", dmg, out _, out _)));

                sb.AppendLine(@$"
{Utilities.GeneratedCodeAttribute}
public class {safeName} : Card
{{
    public override string Name => ""{cardDefinition.Name}"";
    public override bool RequiresTarget => {(!string.IsNullOrWhiteSpace(cardDefinition.SingleTargetDamage) ? "true" : "false")};
    public override CardInstance CreateInstance() => new {safeName}Instance(this);
    public {safeName}()
    {{
        {string.Concat(costs.Select(m => @$"Base{m.name}Cost = {m.quantity};"))}
    }}
}}

{Utilities.GeneratedCodeAttribute}
public class {safeName}Instance : CardInstance 
{{
    public {safeName}Instance(Card card) : base(card) 
    {{ 
        {string.Concat(costs.Select(m => @$"{m.name}Cost = card.Base{m.name}Cost;"))}
    }}

    public override string GetDescription(Player player) => 
        $""{string.Join(" ",
            gains.Where(w => w.quantity != 0).Select(w => allResources[w.name].GainDescription.Replace("{}", $"{{{w.quantity}{AddBuffStats("player.", w.name)}}}"))
                .Concat(singleTargetDamage.Where(w => w.quantity != 0).Select(w => allResources[w.name].SingleTargetDescription.Replace("{}", $"{{{w.quantity}}}")))
                .Concat(gainBuffs.Where(w => w.quantity != 0).Select(w => $"Gain {w.quantity} stack{(w.quantity != 1 ? "s" : "")} of {w.name}."))
                .Concat(castBuffs.Where(w => w.quantity != 0).Select(w => $"Cast {w.quantity} stack{(w.quantity != 1 ? "s" : "")} of {w.name}.")))}"";

    public override bool Cast(Player player, UnitInstance? unitInstance)
    {{
        // costs
        if({string.Join("&&", costs.Select(w => $@"player.{w.name} < {w.name}Cost"))})
            return false;
        {string.Concat(costs.Select(w => $@"player.{w.name} -= {w.name}Cost;"))}

        // gains
        {(string.Concat(gains.Select(w => allResources[w.name].HasMaximum
            ? $"player.{w.name} = Math.Min(player.{w.name}Maximum, player.{w.name} + {w.quantity} {AddBuffStats("player.", w.name)});"
            : $"player.{w.name} += {w.quantity} {AddBuffStats("player.", w.name)};")))}
        {string.Concat(gainBuffs.Select(w => $"player.GetOrAddBuff<{Utilities.GetSafeName(w.name)}Buff>({w.quantity});"))}

        // damage
        {castDamage}
        {string.Concat(castBuffs.Select(w => $"unitInstance!.GetOrAddBuff<{Utilities.GetSafeName(w.name)}Buff>({w.quantity});"))}

        return true;
    }}
}}");
            }

            // base card classes
            sb.AppendLine(@$"
{Utilities.GeneratedCodeAttribute}
public abstract class Card
{{
    public abstract string Name {{ get; }}
    public abstract bool RequiresTarget {{ get; }}
    {string.Concat(allResources.Values.Select(w => $"public double Base{w.Name}Cost {{ get; protected set; }}"))}
    public abstract CardInstance CreateInstance();
    public static Dictionary<string, Card> All {{ get; }} = new()
    {{
        {string.Concat(allCardDefinitions.Select(w => $@"[""{Utilities.GetSafeName(w.Name)}""] = new {Utilities.GetSafeName(w.Name)}(),"))}
    }};
    {string.Concat(allResources.Values.Select(w => $"public static ConsoleColor {w.Name}CostColor => ConsoleColor.{w.Color};"))}
}}

{Utilities.GeneratedCodeAttribute}
public abstract class CardInstance
{{
    public Card CardTemplate {{ get; }}
    {string.Concat(allResources.Values.Select(w => $@"public double {w.Name}Cost {{ get; protected set; }}"))}
    public abstract string GetDescription(Player player);

    public abstract bool Cast(Player player, UnitInstance? unitInstance);

    protected CardInstance(Card card) 
    {{
        CardTemplate = card;
    }}
}}");
            sb.AppendLine("}");

            // player class
            sb.AppendLine($@"
namespace Cardie.GameEnvironment {{
    {Utilities.GeneratedCodeAttribute}
    public partial class Player
    {{
        {string.Concat(allResources.Values.Where(w => w.Name is not "Health" and not "Block").Select(w => $@"public double {w.Name} {{ get; set; }}"))}
        {string.Concat(allResources.Values.Where(w => w.Name is not "Health" and not "Block" && w.HasMaximum).Select(w => $@"public double {w.Name}Maximum {{ get; set; }}"))}
    }}
}}");

            // units
            sb.AppendLine("namespace Cardie.Units { using Cardie.Buffs;");
            foreach (var unitDefinition in allUnitDefinitions)
            {
                var safeName = Utilities.GetSafeName(unitDefinition.Name);

                sb.AppendLine(@$"
{Utilities.GeneratedCodeAttribute}
public class {safeName} : Unit
{{
    public override string Name => ""{unitDefinition.Name}"";
    public override double BaseHealth => {unitDefinition.BaseHealth};
    public override double BaseBlock => {unitDefinition.BaseArmor};
    public override UnitInstance CreateInstance(World world) => new {safeName}Instance(world, this);
}}

{Utilities.GeneratedCodeAttribute}
public class {safeName}Instance : UnitInstance 
{{
    public {safeName}Instance(World world, Unit unit) : base(world, unit) 
    {{ 
        SelectNextAbility();
    }}

    static readonly WeightedRandomItem<(Action<{safeName}Instance, Player>, Func<{safeName}Instance, string>)> weightedRandomAction = new(
        {string.Join(",", unitDefinition.UnitAbilities
            .Select(a =>
                $@"(((i, p) => {{ {string.Concat(Utilities.GetResourcesFromString(a.SingleTargetDamage).Select(r =>
                    {
                        return @$"{
GetDamageCode("i.", "p.", r, out var damageDone, out var damageBlocked)}
Globals.Log?.Invoke($""{{i.TemplateUnit.Name}} dealt {{{damageDone}}} damage to the player (and {{{damageBlocked}}} blocked)."");";
                    }))}{string.Concat(Utilities.GetBuffsFromString(a.GainBuffs).Select(b => @$"i.GetOrAddBuff<{b.name}Buff>({b.quantity}); Globals.Log?.Invoke($""{{i.TemplateUnit.Name}} gained {b.quantity} stack{(b.quantity == 1 ? null : "s")} of {b.name}."");"))} }}, 
                i => $""{(Utilities.GetResourcesFromString(a.SingleTargetDamage).Any() ? $"Intends to attack for {string.Join(" ", Utilities.GetResourcesFromString(a.SingleTargetDamage).Select(w=>@$"{{{w.quantity}{AddDamageBuffStats("i.", w.name)}}} {w.name}"))}." : "")}{(Utilities.GetBuffsFromString(a.GainBuffs).Any() ? $"Intends to buff." : "")}""), {a.Chance})"))}
    );

    Action<{safeName}Instance, Player> selectedAbilityAction;
    Func<{safeName}Instance, string> selectedAbilityDescription;

    private void SelectNextAbility() => (selectedAbilityAction, selectedAbilityDescription) = weightedRandomAction.GetRandomItem(ThreadSafeRandom.ThisThreadsRandom);

    public override string Intent => selectedAbilityDescription(this);

    public override void EndTurn()
    {{
        base.EndTurn();
        selectedAbilityAction(this, world.Player);
        SelectNextAbility();
    }}
}}");
            }

            sb.AppendLine(@$"
{Utilities.GeneratedCodeAttribute}
public abstract class Unit
{{
    public abstract string Name {{ get; }}
    public abstract double BaseHealth {{ get; }}
    public abstract double BaseBlock {{ get; }}
    public abstract UnitInstance CreateInstance(World world);
    public static Dictionary<string, Unit> All {{ get; }} = new()
    {{
        {string.Concat(allUnitDefinitions.Select(w => $@"[""{Utilities.GetSafeName(w.Name)}""] = new {Utilities.GetSafeName(w.Name)}(),"))}
    }};
}}

{Utilities.GeneratedCodeAttribute}
public abstract class UnitInstance
{{
    protected readonly World world;

    public Unit TemplateUnit {{ get; }}

    public double Health {{ get; set; }}
    public double HealthMaximum {{ get; set; }}

    public double Block {{ get; set; }}

    public abstract string Intent {{ get; }}

    readonly List<Buff> buffs = new();
    public ReadOnlyCollection<Buff> Buffs {{ get; }}

    static readonly Dictionary<Type, Func<UnitInstance, Buff>> buffConstructorCache = new()
    {{
        {string.Concat(allBuffs.Select(b => $"[typeof({b.Name}Buff)] = unit => new {b.Name}Buff(unit),"))}
    }};

    public Buff GetOrAddBuff<TBuff>(int addStacks = 0) where TBuff : Buff
    {{
        var buff = buffs.OfType<TBuff>().FirstOrDefault();
        if(buff is null)
            buffs.Add(buff = (TBuff)(object)buffConstructorCache[typeof(TBuff)](this));
        buff.Stacks += addStacks;
        return buff;
    }}

    protected UnitInstance(World world, Unit unit) =>
        (this.world, TemplateUnit, Health, HealthMaximum, Block, Buffs) =
        (world, unit, unit.BaseHealth, unit.BaseHealth, unit.BaseBlock, new(buffs));

    public virtual void EndTurn()
    {{
        Buffs.ForEach(b => b.EndTurn());
    }}
}}");
            sb.AppendLine("}");

            sb.AppendLine("namespace Cardie.Buffs {");
            sb.AppendLine(@$"
{Utilities.GeneratedCodeAttribute}
public abstract class Buff 
{{
    protected readonly UnitInstance unitInstance;
    public Buff(UnitInstance unitInstance) => this.unitInstance = unitInstance;

    public abstract string Name {{ get; }}
    public int Stacks {{ get; set; }}
    public abstract double BlockAdd {{ get; }}
    public abstract double HealthDamageAdd {{ get; }}
    public abstract void EndTurn();
}}");

            foreach (var buff in allBuffs)
            {
                var safeName = Utilities.GetSafeName(buff.Name);
                static string EntryToCode(string entry) => string.IsNullOrWhiteSpace(entry) ? "0" :
                    Regex.Replace(entry, @"\{[^}]+\}", m => m.Groups[0].Value switch
                      {
                          "{stackCount}" => "Stacks",
                          _ => throw new InvalidOperationException()
                      });

                sb.AppendLine($@"
{Utilities.GeneratedCodeAttribute}
public class {safeName}Buff : Buff
{{
    public {safeName}Buff(UnitInstance unitInstance) : base(unitInstance) {{ }}

    public override string Name => ""{buff.Name}"";
    public override double BlockAdd => {EntryToCode(buff.BlockAdd)};
    public override double HealthDamageAdd => {EntryToCode(buff.HealthDamageAdd)};
    public override void EndTurn()
    {{
        var damageAtEndOfTurn = {EntryToCode(buff.DamageAtEndOfTurn)};
        if(damageAtEndOfTurn != 0)
        {{
            unitInstance.Health -= damageAtEndOfTurn;
            Globals.Log?.Invoke($""{{unitInstance.TemplateUnit.Name}} lost {{damageAtEndOfTurn}} health due to {{Name}}."");
        }}

        var newStacks = Math.Max(0, Stacks - (int)({EntryToCode(buff.StackLossAtEndOfTurn)}));
        if(newStacks != Stacks)
        {{
            Globals.Log?.Invoke($""{{unitInstance.TemplateUnit.Name}}'s {{Name}} lost {{Stacks - newStacks}} stacks."");
            Stacks = newStacks;
        }}
    }}
}}");
            }

            sb.AppendLine("}");

            context.AddSource("GeneratedCards.cs", sb.ToString());
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            //Debugger.Launch();
        }
    }
}
