using LiteNetLib;
using LiteNetLib.Utils;
using Raylib_cs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PrettySus.Client
{
    class ClientApp : IDisposable
    {
        private readonly EventBasedNetListener _listener;
        private readonly NetManager _client;

        private int _playerCount = 0;
        private PlayerState[] _players = new PlayerState[16];
        private int _playerId = 0;
        private NetDataWriter _writer = new();
        private Stopwatch _timer = new();
        private PlayerInput _input = new();
        private long _lastGameState = 0;
        private long _lastInput = 0;

        public ClientApp()
        {
            _listener = new EventBasedNetListener();
            _client = new NetManager(_listener);

            _client.Start();

            _listener.NetworkReceiveEvent += OnNetworkReceiveEvent;

            Raylib.InitWindow(1280, 720, "PrettySus");
        }

        public void Dispose()
        {
            Raylib.CloseWindow();

            _client.Stop();
        }

        private void OnNetworkReceiveEvent(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            var packetType = (PacketType)reader.GetByte();
            switch (packetType)
            {
                case PacketType.GameState:
                    _playerId = reader.GetInt();
                    _playerCount = reader.GetInt();

                    for (var i = 0; i < _playerCount; i++)
                    {
                        if (_players[i] == null)
                            _players[i] = new PlayerState();

                        _players[i].PlayerId = reader.GetInt();
                        _players[i].PrevX = _players[i].X;
                        _players[i].PrevY = _players[i].Y;
                        _players[i].X = reader.GetFloat();
                        _players[i].Y = reader.GetFloat();
                        _players[i].ColorR = reader.GetByte();
                        _players[i].ColorG = reader.GetByte();
                        _players[i].ColorB = reader.GetByte();
                    }

                    _lastGameState = _timer.ElapsedMilliseconds;

                    break;
                default:
                    Console.WriteLine($"Invalid packet type {packetType} from {peer}");
                    break;
            }

            reader.Recycle();
        }

        public void Run()
        {
            _client.Connect("127.0.0.1", 9050, "TEST");

            _timer.Start();

            while (!Raylib.WindowShouldClose())
            {
                _client.PollEvents();

                // Gather input
                if (Raylib.IsKeyDown(KeyboardKey.KEY_LEFT) || Raylib.IsKeyDown(KeyboardKey.KEY_A))
                    _input.X = -1.0f;
                if (Raylib.IsKeyDown(KeyboardKey.KEY_RIGHT) || Raylib.IsKeyDown(KeyboardKey.KEY_D))
                    _input.X = 1.0f;

                if (Raylib.IsKeyDown(KeyboardKey.KEY_UP) || Raylib.IsKeyDown(KeyboardKey.KEY_W))
                    _input.Y = -1.0f;
                if (Raylib.IsKeyDown(KeyboardKey.KEY_DOWN) || Raylib.IsKeyDown(KeyboardKey.KEY_S))
                    _input.Y = 1.0f;

                // Send input
                var timeSinceLastInput = _timer.ElapsedMilliseconds - _lastInput;
                if (timeSinceLastInput >= Constants.TickLengthInMs)
                {
                    _lastInput = _timer.ElapsedMilliseconds;

                    _writer.Reset();
                    _writer.Put((byte)PacketType.Input);
                    _writer.Put(_input.X);
                    _writer.Put(_input.Y);

                    _client.FirstPeer.Send(_writer, DeliveryMethod.Sequenced);

                    _input.X = 0;
                    _input.Y = 0;
                }

                // Draw
                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.WHITE);

                var timeSinceLastGameState = _timer.ElapsedMilliseconds - _lastGameState;
                var alpha = timeSinceLastGameState / (float)Constants.TickLengthInMs;

                for (var i = 0; i < _playerCount; i++)
                {
                    var player = _players[i];
                    if (player.PlayerId == _playerId)
                    {
                    }

                    var x = player.PrevX + (player.X - player.PrevX) * alpha;
                    var y = player.PrevY + (player.Y - player.PrevY) * alpha;

                    Raylib.DrawRectangle((int)x, (int)y, 32, 64, new Color(player.ColorR, player.ColorG, player.ColorB, (byte)255));
                }

                Raylib.EndDrawing();

                Thread.Sleep(0);
            }
        }
    }
}
