using System;

namespace Cardie.Support
{
    public static class ThreadSafeRandom
    {
        [ThreadStatic] private static Random? Local;

        public static Random ThisThreadsRandom =>
            Local ??= new Random(unchecked(Environment.TickCount * 31 + Environment.CurrentManagedThreadId));
    }
}
