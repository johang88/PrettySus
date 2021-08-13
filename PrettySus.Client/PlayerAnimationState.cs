using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrettySus.Client
{
    class PlayerAnimationState
    {
        public bool IsWalking;
        public long LastFrameTime;
        public long CurrentFrame;
        public float Direction = 1.0f;
    }
}
