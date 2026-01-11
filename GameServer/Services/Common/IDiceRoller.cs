using System;

namespace GameServer.Services.Common
{
    public interface IDiceRoller
    {
        int Next(int minValue, int maxValue);
    }

    public class RandomDiceRoller : IDiceRoller
    {
        private static readonly Random _random = new Random();
        private static readonly object _lock = new object();

        public int Next(int minValue, int maxValue)
        {
            lock (_lock)
            {
                return _random.Next(minValue, maxValue);
            }
        }
    }
}