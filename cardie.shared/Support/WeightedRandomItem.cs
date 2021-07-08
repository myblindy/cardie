using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cardie.Support
{
    public class WeightedRandomItem<TItem>
    {
        readonly double totalWeight;
        readonly (TItem item, double cummulativeWeight)[] items;

        public WeightedRandomItem(params (TItem item, double weight)[] items)
        {
            double cummulativeWeight = 0;
            this.items = items.Select(w => (w.item, cummulativeWeight += w.weight)).ToArray();
            totalWeight = cummulativeWeight;
        }

        public TItem GetRandomItem(Random random) =>
            items.First(w => random.NextDouble() * totalWeight <= w.cummulativeWeight).item;
    }
}
