using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace PrettySus.Server
{
    class ServerApp : IDisposable
    {
        private readonly EventBasedNetListener _listener;
        private readonly NetManager _server;

        private const int Port = 9050;
        private const int MaxConnections = 8;

        private Dictionary<NetPeer, PlayerServerState> _players = new();
        private Dictionary<NetPeer, PlayerInput> _playerInputs = new();

        private Random _rng = new();
        private NetDataWriter _writer = new();

        private int _sleepTime = 0;

        private GameState _gameState = new();

        public ServerApp(string[] args)
        {
            var sleepTime = args.FirstOrDefault(x => x.StartsWith("--sleep-time="));
            if (sleepTime != null)
            {
                int.TryParse(sleepTime.Replace("--sleep-time=", ""), out _sleepTime);
                Log.Information("--sleep-time {SleepTime}", _sleepTime);
            }

            _listener = new EventBasedNetListener();
            _server = new NetManager(_listener);

            _server.Start(Port);

            _listener.ConnectionRequestEvent += OnConnectionRequestEvent;
            _listener.PeerConnectedEvent += OnPeerConnectedEvent;
            _listener.PeerDisconnectedEvent += PeerDisconnectedEvent;
            _listener.NetworkReceiveEvent += NetworkReceiveEvent;

            Log.Information("Server listening at port {@Port}", _server.LocalPort);
        }

        public void Dispose()
        {
            _server.Stop();
        }

        private void OnConnectionRequestEvent(ConnectionRequest request)
        {
            Log.Information("Incoming connection from {@EndPoint}", request.RemoteEndPoint.Address.ToString());

            if (_server.ConnectedPeersCount >= MaxConnections)
            {
                Log.Information("Connection from {@EndPoint} rejected, max players reached", request.RemoteEndPoint.Address.ToString());
                request.Reject();
            }
            else if (_gameState.State != States.Lobby)
            {
                Log.Information("Connection from {@EndPoint} rejected, game not in lobby", request.RemoteEndPoint.Address.ToString());
                request.Reject();
            }
            else
            {
                var playerName = request.Data.GetString(Constants.MaxNameLength);
                var peer = request.Accept();

                _players.Add(peer, new PlayerServerState
                {
                    ConnectionState = PlayerConnectionState.Connecting,
                    PlayerId = peer.Id,
                    IsAlive = true,
                    Name = playerName,
                    ColorIndex = PlayerColors.GetNextColorIndex(_players.Values),
                    X = Map.TileSize * 2,
                    Y = Map.TileSize * 2
                });
            }
        }

        private void OnPeerConnectedEvent(NetPeer peer)
        {
            var player = _players[peer];

            Log.Information("{Player} connected", player.Name);
            player.ConnectionState = PlayerConnectionState.Connected;
        }

        private void PeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            var player = _players[peer];
            Log.Information("{Player} disconnected", player.Name);

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
                    input.IsReady = reader.GetBool();
                    input.Attack = reader.GetBool();
                    input.ColorIndex = reader.GetByte();
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
                _server.PollEvents();

                var timeSinceLastTick = watch.ElapsedMilliseconds - lastTick;
                if (timeSinceLastTick >= Constants.TickLengthInMs)
                {
                    lastTick = watch.ElapsedMilliseconds;

                    var dt = timeSinceLastTick / 1000.0f;

                    foreach (var input in _playerInputs)
                    {
                        if (_players.TryGetValue(input.Key, out var playerState) && playerState.ConnectionState == PlayerConnectionState.Connected)
                        {
                            if (_gameState.State == States.Lobby && input.Value.ColorIndex != playerState.ColorIndex && !PlayerColors.IsColorUsed(_players.Values, input.Value.ColorIndex))
                            {
                                playerState.ColorIndex = input.Value.ColorIndex;
                            }

                            if (_gameState.State == States.Lobby)
                            {
                                playerState.IsReady = input.Value.IsReady;
                            }

                            if (playerState.IsAlive)
                            {
                                // Very good movement logic
                                var x = Math.Min(1.0f, Math.Max(-1.0f, input.Value.X));
                                var y = Math.Min(1.0f, Math.Max(-1.0f, input.Value.Y));

                                var speed = 500;

                                var newPositionX = playerState.X + x * speed * dt;
                                if (Map.PlayerCollides(newPositionX, playerState.Y))
                                {
                                    newPositionX = playerState.X;
                                }

                                var newPositionY = playerState.Y + y * speed * dt;
                                if (Map.PlayerCollides(playerState.X, newPositionY))
                                {
                                    newPositionY = playerState.Y;
                                }

                                playerState.X = newPositionX;
                                playerState.Y = newPositionY;

                                if (input.Value.Attack && (playerState.AttackedAt == null || (watch.ElapsedMilliseconds - playerState.AttackedAt.Value) >= Constants.AttackCooldown))
                                {
                                    // TODO: Trace through tilemap so you cant kill through stuff
                                    playerState.AttackedAt = watch.ElapsedMilliseconds;

                                    foreach (var otherPlayer in _players.Values)
                                    {
                                        if (otherPlayer.PlayerId == playerState.PlayerId)
                                            continue;

                                        var dx = playerState.X - otherPlayer.X;
                                        var dy = playerState.Y - otherPlayer.Y;

                                        var distance = MathF.Sqrt(dx * dx + dy * dy);
                                        if (distance <= Constants.KillDistance)
                                        {
                                            otherPlayer.IsAlive = false;
                                            otherPlayer.DieadAt = watch.ElapsedMilliseconds;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                var timeDead = watch.ElapsedMilliseconds - playerState.DieadAt;
                                if (timeDead >= Constants.RespawnTime)
                                {
                                    playerState.IsAlive = true;
                                }
                            }
                        }
                    }

                    switch (_gameState.State)
                    {
                        case States.Lobby when _players.Count > 0 && _players.Values.All(x => x.IsReady):
                            _gameState.State = States.Starting;
                            _gameState.CountDown = 5;
                            break;
                        case States.Starting:
                            _gameState.CountDown -= dt;
                            if (_gameState.CountDown <= 0.0f)
                            {
                                _gameState.State = States.Started;
                                // TODO: Reset player positions
                            }
                            break;
                        case States.Started:
                            break;
                    }

                    foreach (var player in _players)
                    {
                        SendGameState(player.Key, player.Value);
                    }
                }

                if (_sleepTime >= 0)
                {
                    Thread.Sleep(_sleepTime);
                }
            }
        }

        private void SendGameState(NetPeer peer, PlayerState value)
        {
            _writer.Reset();

            _writer.Put((byte)PacketType.GameState);
            _writer.Put(peer.Id);

            _writer.Put((byte)_gameState.State);
            _writer.Put(_gameState.CountDown);

            _writer.Put(_players.Count);
            foreach (var player in _players)
            {
                _writer.Put(player.Key.Id);

                var state = player.Value;
                _writer.Put(state.Name, Constants.MaxNameLength);
                _writer.Put((byte)state.ConnectionState);
                _writer.Put(state.IsReady);
                _writer.Put(state.IsAlive);
                _writer.Put(state.X);
                _writer.Put(state.Y);
                _writer.Put(state.ColorIndex);
            }

            peer.Send(_writer, DeliveryMethod.Sequenced);
        }
    }
}
