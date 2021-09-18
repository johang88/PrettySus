using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrettySus.Server
{
    public class PlayerServerState : PlayerState
    {
        public long DiedAt;
        public long? AttackedAt;
    }
}
