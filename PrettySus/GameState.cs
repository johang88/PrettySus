using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrettySus
{
    public enum States : byte
    {
        Lobby,
        Starting,
        Started
    }

    public class GameState
    {
        public States State { get; set; } = States.Lobby;
        public float CountDown { get; set; } = 0;
        public int PlayerCount { get; set; } = 0;
    }
}
