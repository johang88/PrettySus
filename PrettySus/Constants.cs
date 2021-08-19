using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrettySus
{
    public static class Constants
    {
        public const int TickRate = 30;
        public const int TickLengthInMs = 1000 / TickRate;

        public const int MaxNameLength = 30;

        public const int PlayerWidth = 66;
        public const int PlayerHeight = 92;

        public const float KillDistance = PlayerWidth * 3;
        public const int RespawnTime = 5000;
        public const int AttackCooldown = 2000;
    }
}
