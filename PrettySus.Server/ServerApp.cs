using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PrettySus.Server
{
    class ServerApp : IDisposable
    {
        private readonly EventBasedNetListener _listener;
        private readonly NetManager _server;

        private const int Port = 9050;
        private const int MaxConnections = 10;

        private Dictionary<NetPeer, PlayerState> _players = new();
        private Dictionary<NetPeer, PlayerInput> _playerInputs = new();

        private Random _rng = new();
        private NetDataWriter _writer = new();

        public ServerApp()
        {
            _listener = new EventBasedNetListener();
            _server = new NetManager(_listener);

            _server.Start(Port);

            _listener.ConnectionRequestEvent += OnConnectionRequestEvent;
            _listener.PeerConnectedEvent += OnPeerConnectedEvent;
            _listener.PeerDisconnectedEvent += PeerDisconnectedEvent;
            _listener.NetworkReceiveEvent += NetworkReceiveEvent;
        }

        public void Dispose()
        {
            _server.Stop();
        }

        private void OnConnectionRequestEvent(ConnectionRequest request)
        {
            if (_server.ConnectedPeersCount < MaxConnections)
                request.AcceptIfKey("TEST");
            else
                request.Reject();
        }

        private void OnPeerConnectedEvent(NetPeer peer)
        {
            Console.WriteLine($"{peer.EndPoint} connected");

            _players.Add(peer, new PlayerState
            {
                PlayerId = peer.Id,
                ColorR = (byte)_rng.Next(255),
                ColorG = (byte)_rng.Next(255),
                ColorB = (byte)_rng.Next(255)
            });
        }

        private void PeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            Console.WriteLine($"{peer.EndPoint} disconnected");

            _players.Remove(peer);
        }

        private void NetworkReceiveEvent(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            var packetType = (PacketType)reader.GetByte();

            switch (packetType)
            {
                case PacketType.Input:
                    if (!_playerInputs.TryGetValue(peer, out var input))
                    {
                        input = new PlayerInput();
                        _playerInputs.Add(peer, input);
                    }

                    // Only keep latest input
                    input.X = reader.GetFloat();
                    input.Y = reader.GetFloat();
                    break;
                default:
                    Console.WriteLine($"Invalid packet type {packetType} from {peer}");
                    break;
            }

            reader.Recycle();
        }

        public void Run()
        {
            var watch = new Stopwatch();
            watch.Start();

            var lastTick = 0L;

            var running = true;
            while (running)
            {
                if (Console.KeyAvailable && Console.ReadKey().Key == ConsoleKey.C)
                {
                    running = false;
                }

                _server.PollEvents();

                var timeSinceLastTick = watch.ElapsedMilliseconds - lastTick;
                if (timeSinceLastTick >= Constants.TickLengthInMs)
                {
                    lastTick = watch.ElapsedMilliseconds;

                    var dt = timeSinceLastTick / 1000.0f;

                    foreach (var input in _playerInputs)
                    {
                        if (_players.TryGetValue(input.Key, out var playerState))
                        {
                            // Very good movement logic
                            var x = Math.Min(1.0f, Math.Max(-1.0f, input.Value.X));
                            var y = Math.Min(1.0f, Math.Max(-1.0f, input.Value.Y));

                            playerState.X += x * 500 * dt;
                            playerState.Y += y * 500 * dt;
                        }
                    }

                    foreach (var player in _players)
                    {
                        SendGameState(player.Key, player.Value);
                    }
                }

                Thread.Sleep(0);
            }
        }

        private void SendGameState(NetPeer peer, PlayerState value)
        {
            _writer.Reset();

            _writer.Put((byte)PacketType.GameState);
            _writer.Put(peer.Id);

            _writer.Put(_players.Count);
            foreach (var player in _players)
            {
                _writer.Put(player.Key.Id);

                var state = player.Value;
                _writer.Put(state.X);
                _writer.Put(state.Y);
                _writer.Put(state.ColorR);
                _writer.Put(state.ColorG);
                _writer.Put(state.ColorB);
            }

            peer.Send(_writer, DeliveryMethod.Sequenced);
        }
    }
}
