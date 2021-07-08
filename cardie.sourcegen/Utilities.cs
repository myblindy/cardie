using CsvHelper;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Cardie.SourceGen
{
    class Resource
    {
        public string Name { get; set; }
        public ConsoleColor Color { get; set; }
        public string BlockedBy { get; set; }
        public bool HasMaximum { get; set; }
        public string SingleTargetDescription { get; set; }
        public string GainDescription { get; set; }
    }

    class Buff
    {
        public string Name { get; set; }
        public string BlockAdd { get; set; }
        public string HealthDamageAdd { get; set; }
        public string DamageAtEndOfTurn { get; set; }
        public string StackLossAtEndOfTurn { get; set; }
    }

    class CardDefinition
    {
        public string Name { get; set; }
        public string BaseCost { get; set; }
        public string ResourceGain { get; set; }
        public string SingleTargetDamage { get; set; }
        public string CastBuffs { get; set; }
        public string GainBuffs { get; set; }
    }

    class UnitAbility
    {
        public double Chance { get; set; }
        public string GainBuffs { get; set; }
        public string SingleTargetDamage { get; set; }
    }

    class UnitAbilityDefinition : UnitAbility
    {
        public string Name { get; set; }
    }

    class UnitDefinition
    {
        public string Name { get; set; }
        public double BaseHealth { get; set; }
        public double BaseArmor { get; set; }
        public List<UnitAbility> UnitAbilities { get; } = new();
    }

    static class Utilities
    {
        public static string GetSafeName(string name) => Regex.Replace(name, @"\s+", "");

        public static string GeneratedCodeAttribute => $"[GeneratedCode(\"cardie.sourcegen\", \"{Assembly.GetExecutingAssembly().GetName().Version}\")]";

        static readonly Resource[] healthResource = new Resource[]
        {
            new()
            {
                Name = "Health",
                Color = ConsoleColor.Red,
                BlockedBy = "Block",
                HasMaximum = true,
                SingleTargetDescription = "Deal {} damage.",
                GainDescription = "Heal for {}."
            }
        };
        public static Resource[] GetResources(GeneratorExecutionContext context)
        {
            var file = context.AdditionalFiles.Where(f => Regex.IsMatch(f.Path, @"gamedata[/\\]resources\.csv$", RegexOptions.IgnoreCase)).FirstOrDefault();
            if (file is null) return healthResource;

            using var reader = new StreamReader(file.Path);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            return csv.GetRecords<Resource>().Concat(healthResource).ToArray();
        }

        public static Buff[] GetBuffs(GeneratorExecutionContext context)
        {
            var file = context.AdditionalFiles.Where(f => Regex.IsMatch(f.Path, @"gamedata[/\\]buffs\.csv$", RegexOptions.IgnoreCase)).FirstOrDefault();
            if (file is null) return Array.Empty<Buff>();

            using var reader = new StreamReader(file.Path);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            return csv.GetRecords<Buff>().ToArray();
        }

        public static UnitDefinition[] GetUnits(GeneratorExecutionContext context)
        {
            var file = context.AdditionalFiles.Where(f => Regex.IsMatch(f.Path, @"gamedata[/\\]units\.csv$", RegexOptions.IgnoreCase)).FirstOrDefault();
            if (file is not null)
            {
                UnitDefinition[] unitDefinitions;
                using (var reader = new StreamReader(file.Path))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    unitDefinitions = csv.GetRecords<UnitDefinition>().ToArray();

                file = context.AdditionalFiles.Where(f => Regex.IsMatch(f.Path, @"gamedata[/\\]unitabilities\.csv$", RegexOptions.IgnoreCase)).FirstOrDefault();
                if (file is not null)
                {
                    using var reader = new StreamReader(file.Path);
                    using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                    foreach (var unitAbility in csv.GetRecords<UnitAbilityDefinition>())
                        unitDefinitions.Single(w => w.Name == unitAbility.Name).UnitAbilities.Add(unitAbility);

                    return unitDefinitions;
                }
            }

            return Array.Empty<UnitDefinition>();
        }

        public static CardDefinition[] GetCards(GeneratorExecutionContext context)
        {
            var file = context.AdditionalFiles.Where(f => Regex.IsMatch(f.Path, @"gamedata[/\\]cards\.csv$", RegexOptions.IgnoreCase)).FirstOrDefault();
            if (file is null) return Array.Empty<CardDefinition>();

            using var reader = new StreamReader(file.Path);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            return csv.GetRecords<CardDefinition>().ToArray();
        }

        public static string CapitalizeString(this string s) => char.IsUpper(s[0]) ? s : $"{char.ToUpper(s[0])}{s.Substring(1)}";

        public static IEnumerable<(string name, double quantity)> GetResourcesFromString(string res) =>
            Regex.Matches(res, @"(\d+) (\w+)").Cast<Match>().Select(m => (m.Groups[2].Value.CapitalizeString(), double.Parse(m.Groups[1].Value)));

        public static IEnumerable<(string name, double quantity)> GetBuffsFromString(string res) =>
            Regex.Matches(res, @"(\d+)x(\w+)").Cast<Match>().Select(m => (m.Groups[2].Value, double.Parse(m.Groups[1].Value)));
    }
}
