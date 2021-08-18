using ImGuiNET;
using LiteNetLib;
using LiteNetLib.Utils;
using Raylib_cs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PrettySus.Client
{
    enum ClientState
    {
        Disconnected,
        Connecting,
        Connected
    }

    class ClientApp : IDisposable
    {
        private readonly EventBasedNetListener _listener;
        private readonly NetManager _client;

        private int _playerCount = 0;
        private PlayerState[] _players = new PlayerState[16];
        private Dictionary<int, PlayerAnimationState> _playerAnimations = new();
        private int _playerId = 0;
        private NetDataWriter _writer = new();
        private Stopwatch _timer = new();
        private PlayerInput _input = new();
        private long _lastGameState = 0;
        private long _lastInput = 0;

        private Texture2D _playerIdle;
        private Texture2D[] _playerWalk;
        private ImGuiImplementation _imgui;

        private ClientState _state = ClientState.Disconnected;
        private string _serverAddress = "127.0.0.1";
        private int _serverPort = 9050;
        private string _name = "";

        public ClientApp()
        {
            _listener = new EventBasedNetListener();
            _client = new NetManager(_listener);

            _client.Start();

            _listener.NetworkReceiveEvent += OnNetworkReceiveEvent;

            Raylib.InitWindow(1280, 720, "PrettySus");

            _playerIdle = Raylib.LoadTexture("Assets/Player/p1_stand.png");
            _playerWalk = new Texture2D[11];
            for (var i = 0; i < _playerWalk.Length; i++)
            {
                _playerWalk[i] = Raylib.LoadTexture($"Assets/Player/p1_walk{(i + 1):00}.png");
            }

            _imgui = new ImGuiImplementation();
        }

        public void Dispose()
        {
            Raylib.UnloadTexture(_playerIdle);
            for (var i = 0; i < _playerWalk.Length; i++)
            {
                Raylib.UnloadTexture(_playerWalk[i]);
            }

            _imgui.Dispose();

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
                        
                        _players[i].Name = reader.GetString();
                        _players[i].ConnectionState = (PlayerConnectionState)reader.GetByte();
                        
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

        private void Connect()
        {
            _writer.Reset();
            _writer.Put(_name, Constants.MaxNameLength);

            _client.Connect(_serverAddress, _serverPort, _writer);
            _state = ClientState.Connecting;
        }

        private void UpdateDisconnected()
        {
            ImGui.SetNextWindowSize(new Vector2(400, 200), ImGuiCond.Always);
            ImGui.Begin("Menu", ImGuiWindowFlags.NoSavedSettings);

            ImGui.InputText("Host", ref _serverAddress, 32);
            ImGui.InputText("Name", ref _name, 32);
            ImGui.InputInt("Port", ref _serverPort);

            if (ImGui.Button("Connect") && _name.Length > 0)
            {
                Connect();
            }
            ImGui.End();

            if (_client.ConnectedPeersCount > 0)
            {
                _state = ClientState.Connected;
            }
        }

        private void UpdateConnecting()
        {
            ImGui.SetNextWindowSize(new Vector2(400, 200), ImGuiCond.Always);
            ImGui.Begin("Connecting", ImGuiWindowFlags.NoSavedSettings);

            if (ImGui.Button("Cancel"))
            {
                _client.DisconnectAll();
                _state = ClientState.Disconnected;
            }
            ImGui.End();

            if (_client.ConnectedPeersCount > 0)
            {
                _state = ClientState.Connected;
            }
        }

        private void UpdateConnected()
        {
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

            // Render
            var timeSinceLastGameState = _timer.ElapsedMilliseconds - _lastGameState;
            var alpha = timeSinceLastGameState / (float)Constants.TickLengthInMs;

            for (var i = 0; i < _playerCount; i++)
            {
                var player = _players[i];

                if (!_playerAnimations.TryGetValue(player.PlayerId, out var animationState))
                {
                    animationState = new PlayerAnimationState();
                    _playerAnimations.Add(player.PlayerId, animationState);
                }

                var diffX = (player.X - player.PrevX);
                var diffY = (player.Y - player.PrevY);

                Texture2D texture = _playerIdle;
                if (diffX != 0.0f || diffY != 0.0f)
                {
                    animationState.Direction = diffX >= 0.0f ? 1.0f : -1.0f;

                    if (!animationState.IsWalking)
                    {
                        animationState.IsWalking = true;
                    }

                    var timeSinceLastFrame = _timer.ElapsedMilliseconds - animationState.LastFrameTime;
                    if (timeSinceLastFrame >= 100)
                    {
                        animationState.CurrentFrame++;
                        animationState.LastFrameTime = _timer.ElapsedMilliseconds;
                    }

                    texture = _playerWalk[animationState.CurrentFrame % _playerWalk.Length];
                }
                else
                {
                    animationState.IsWalking = false;
                }

                var x = player.PrevX + diffX * alpha;
                var y = player.PrevY + diffY * alpha;

                var fontSize = 30;
                var width = Raylib.MeasureText(player.Name, fontSize);
                Raylib.DrawText(player.Name, (int)(x + texture.width / 2 - width / 2), (int)(y - fontSize), fontSize, Color.WHITE);
                
                var color = new Color(player.ColorR, player.ColorG, player.ColorB, (byte)255);
                Raylib.DrawTextureRec(texture, new Rectangle(0, 0, texture.width * animationState.Direction, texture.height), new Vector2((int)x, (int)y), color);
            }
        }

        public void Run()
        {
            _timer.Start();

            while (!Raylib.WindowShouldClose())
            {
                _client.PollEvents();
                _imgui.ProcessEvents();
                _imgui.Begin();

                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.BLACK);

                switch (_state)
                {
                    case ClientState.Disconnected:
                        UpdateDisconnected();
                        break;
                    case ClientState.Connecting:
                        UpdateConnecting();
                        break;
                    case ClientState.Connected:
                        UpdateConnected();
                        break;
                }

                _imgui.End();

                Raylib.EndDrawing();
            }
        }
    }
}
