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

    public class PlayerState
    {
        public string Name;
        public PlayerConnectionState ConnectionState;
        public int PlayerId;
        public bool IsAlive;
        public float X;
        public float Y;
        public float PrevX;
        public float PrevY;
        public byte ColorR;
        public byte ColorG;
        public byte ColorB;
    }
}
