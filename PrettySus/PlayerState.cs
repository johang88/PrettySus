using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrettySus
{
    public enum PlayerConnectionState : byte
    {
        Connecting,
        Connected
    }

    public struct PlayerColor
    {
        public byte R;
        public byte G;
        public byte B;

        public PlayerColor(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }
    }

    public static class PlayerColors
    {
        public static PlayerColor[] Colors = new PlayerColor[]
        {
            new (255, 255, 255), // White
            new (255, 0, 0), // Red
            new (0, 255, 0), // Green
            new (0, 0, 255), // Blue
            new (255, 0, 255), // Pink
            new (102, 51, 0), // Brown
            new (0, 255, 255), // Teal
            new (255, 255, 0), // Yellow
        };

        public static bool IsColorUsed(IEnumerable<PlayerState> players, byte index)
        {
            foreach (var player in players)
            {
                if (player.ColorIndex == index)
                    return true;
            }

            return false;
        }

        public static bool IsColorUsed(PlayerState[] players, int playerCount, byte index)
        {
            for (var i = 0; i < playerCount; i++)
            {
                if (players[i].ColorIndex == index)
                    return true;
            }

            return false;
        }

        public static byte GetNextColorIndex(IEnumerable<PlayerState> players)
        {
            for (byte i = 0; i < (byte)Colors.Length; i++)
            {
                if (!IsColorUsed(players, i))
                    return i;
            }

            throw new InvalidOperationException("No available colors");
        }
    }

    public class PlayerState
    {
        public string Name;
        public PlayerConnectionState ConnectionState;
        public int PlayerId;
        public bool IsAlive;
        public bool IsReady;
        public float X;
        public float Y;
        public float PrevX;
        public float PrevY;
        public byte ColorIndex;
    }
}
