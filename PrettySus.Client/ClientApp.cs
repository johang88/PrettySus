using LiteNetLib;
using LiteNetLib.Utils;
using Raylib_cs;
using System;
using System.Collections.Generic;
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
                        _players[i].X = reader.GetFloat();
                        _players[i].Y = reader.GetFloat();
                        _players[i].ColorR = reader.GetByte();
                        _players[i].ColorG = reader.GetByte();
                        _players[i].ColorB = reader.GetByte();
                    }

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

            while (!Raylib.WindowShouldClose())
            {
                _client.PollEvents();

                // Send input
                var inputX = 0.0f;
                var inputY = 0.0f;

                if (Raylib.IsKeyDown(KeyboardKey.KEY_LEFT) || Raylib.IsKeyDown(KeyboardKey.KEY_A))
                    inputX = -1.0f;
                if (Raylib.IsKeyDown(KeyboardKey.KEY_RIGHT) || Raylib.IsKeyDown(KeyboardKey.KEY_D))
                    inputX = 1.0f;

                if (Raylib.IsKeyDown(KeyboardKey.KEY_UP) || Raylib.IsKeyDown(KeyboardKey.KEY_W))
                    inputY = -1.0f;
                if (Raylib.IsKeyDown(KeyboardKey.KEY_DOWN) || Raylib.IsKeyDown(KeyboardKey.KEY_S))
                    inputY = 1.0f;

                _writer.Reset();
                _writer.Put((byte)PacketType.Input);
                _writer.Put(inputX);
                _writer.Put(inputY);

                _client.FirstPeer.Send(_writer, DeliveryMethod.Sequenced);

                // Draw
                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.WHITE);

                for (var i = 0; i < _playerCount; i++)
                {
                    var player = _players[i];
                    if (player.PlayerId == _playerId)
                    {
                    }

                    Raylib.DrawRectangle((int)player.X, (int)player.Y, 32, 64, new Color(player.ColorR, player.ColorG, player.ColorB, (byte)255));
                }

                Raylib.EndDrawing();

                Thread.Sleep(10);
            }
        }
    }
}
