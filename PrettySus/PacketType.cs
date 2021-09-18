using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrettySus
{
    public enum PacketType : byte
    {
        Input,
        GameState,
        SetColorIndex,
        PlayerReady
    }

    public static class Packets
    {
        public static void SendPacket<T>(this NetPeer peer, NetDataWriter writer, NetSerializer serializer, DeliveryMethod deliveryMethod, PacketType type, T packet)
            where T : class, new()
        {
            writer.Reset();
            writer.Put((byte)type);
            serializer.Serialize(writer, packet);
            peer.Send(writer, deliveryMethod);
        }
    }
}
