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

    struct PlayerSpriteInfo
    {
        public Texture2D Texture;
        public Color Color;
        public Rectangle Rectangle;
        public Vector2 Position;
    }

    class ClientApp : IDisposable
    {
        private readonly EventBasedNetListener _listener;
        private readonly NetManager _client;

        private PlayerClientState[] _players = new PlayerClientState[16];
        private Dictionary<int, PlayerAnimationState> _playerAnimations = new();
        private int _playerId = 0;
        private NetDataWriter _writer = new();
        private NetSerializer _serializer = new();
        private Stopwatch _timer = new();
        private PlayerInput _input = new();
        private long _lastGameState = 0;
        private long _lastInput = 0;
        private GameState _gameState = new();

        private Texture2D _playerIdle;
        private Texture2D _playerDead;
        private Texture2D[] _playerWalk;
        private ImGuiImplementation _imgui;

        private ClientState _state = ClientState.Disconnected;
        private string _serverAddress = "127.0.0.1";
        private int _serverPort = 9050;
        private string _name = "";

        private long _packetCount = 0;
        private long _connectedAt = 0;
        private bool _isReady = false;

        public ClientApp()
        {
            _listener = new EventBasedNetListener();
            _client = new NetManager(_listener);

            _client.Start();

            _listener.NetworkReceiveEvent += OnNetworkReceiveEvent;

            Raylib.InitWindow(1280, 720, "PrettySus");

            _playerIdle = Raylib.LoadTexture("Assets/Player/p1_stand.png");
            _playerDead = Raylib.LoadTexture("Assets/Player/p1_hurt.png");
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
            Raylib.UnloadTexture(_playerDead);
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
                    _packetCount++;

                    _playerId = reader.GetInt();

                    _serializer.Deserialize(reader, _gameState);
                    for (var i = 0; i < _gameState.PlayerCount; i++)
                    {
                        if (_players[i] == null)
                            _players[i] = new PlayerClientState();

                        _players[i].PrevX = _players[i].X;
                        _players[i].PrevY = _players[i].Y;

                        _serializer.Deserialize(reader, (PlayerState)_players[i]);
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
                SetStateConnected();
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
                SetStateConnected();
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

            if (Raylib.IsKeyDown(KeyboardKey.KEY_SPACE))
                _input.Attack = true;

            // Send input
            var timeSinceLastInput = _timer.ElapsedMilliseconds - _lastInput;
            if (timeSinceLastInput >= Constants.TickLengthInMs)
            {
                _lastInput = _timer.ElapsedMilliseconds;

                _client.FirstPeer.SendPacket(_writer, _serializer, DeliveryMethod.Sequenced, PacketType.Input, _input);

                _input.X = 0;
                _input.Y = 0;
                _input.Attack = false;
            }

            // Render
            var timeSinceLastGameState = _timer.ElapsedMilliseconds - _lastGameState;
            var alpha = timeSinceLastGameState / (float)Constants.TickLengthInMs;

            // Map
            var camera = new Camera2D
            {
                offset = new Vector2(Raylib.GetScreenWidth() / 2.0f, Raylib.GetScreenHeight() / 2.0f),
                zoom = 1.0f
            };

            // Set camera target to player position
            // not very efficent to calculate twice :D
            for (var i = 0; i < _gameState.PlayerCount; i++)
            {
                var player = _players[i];
                if (player.PlayerId == _playerId)
                {
                    var diffX = (player.X - player.PrevX);
                    var diffY = (player.Y - player.PrevY);

                    var x = player.PrevX + diffX * alpha;
                    var y = player.PrevY + diffY * alpha;

                    camera.target = new Vector2((int)x, (int)y);
                }
            }

            Raylib.BeginMode2D(camera);

            for (var y = 0; y < Map.Height; y++)
            {
                for (var x = 0; x < Map.Width; x++)
                {
                    var index = y * Map.Width + x;
                    var tile = Map.Data[index];

                    var color = tile == 0 ? Color.LIGHTGRAY : Color.DARKGRAY;
                    Raylib.DrawRectangle(x * Map.TileSize, y * Map.TileSize, Map.TileSize, Map.TileSize, color);
                }
            }

            // Players
            var maxSprites = _gameState.PlayerCount * 2;
            Span<PlayerSpriteInfo> sprites = stackalloc PlayerSpriteInfo[maxSprites];
            var spriteIndex = 0;

            for (var i = 0; i < _gameState.PlayerCount; i++)
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
                    if (diffX != 0.0f)
                    {
                        animationState.Direction = diffX >= 0.0f ? 1.0f : -1.0f;
                    }

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

                var color = new Color(PlayerColors.Colors[player.ColorIndex].R, PlayerColors.Colors[player.ColorIndex].G, PlayerColors.Colors[player.ColorIndex].B, (byte)255);

                if (!player.IsAlive)
                {
                    sprites[spriteIndex++] = new PlayerSpriteInfo
                    {
                        Color = color,
                        Texture = _playerDead,
                        Rectangle = new Rectangle(0, 0, _playerDead.width, _playerDead.height),
                        Position = new Vector2((int)player.DiedAtX, (int)player.DiedAtY)
                    };

                    color.a = 128;
                }

                sprites[spriteIndex++] = new PlayerSpriteInfo
                {
                    Color = color,
                    Texture = texture,
                    Rectangle = new Rectangle(0, 0, texture.width * animationState.Direction, texture.height),
                    Position = new Vector2((int)x, (int)y)
                };
            }

            sprites.Sort((a, b) => a.Position.Y.CompareTo(b.Position.Y));

            for (var i = 0; i < sprites.Length; i++)
            {
                ref var sprite = ref sprites[i];
                if (sprite.Texture.id != 0)
                    Raylib.DrawTextureRec(sprite.Texture, sprite.Rectangle, sprite.Position, sprite.Color);
            }

            Raylib.EndMode2D();

            if (_gameState.State == States.Lobby || _gameState.State == States.Starting)
            {
                var timeLeft = _gameState.CountDown > 0.0f ? _gameState.CountDown : 0.0f;

                var fontSize = 30;
                var text = _gameState.State == States.Lobby ? "WAITING FOR PLAYERS TO BE READY" : $"STARTING IN {timeLeft:0.00}";

                var width = Raylib.MeasureText(text, fontSize);

                Raylib.DrawText(text, (int)(Raylib.GetScreenWidth() / 2.0f - width / 2.0f), Raylib.GetScreenHeight() - 100, fontSize, Color.WHITE);
            }

            // UI
            if (_gameState.State == States.Lobby)
            {
                ImGui.SetNextWindowSize(new Vector2(400, 200), ImGuiCond.Always);
                ImGui.SetNextWindowPos(new Vector2(20, 20), ImGuiCond.Always);
                ImGui.Begin("Settings", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);

                ImGui.Text("Color");

                for (byte i = 0; i < (byte)PlayerColors.Colors.Length; i++)
                {
                    if (i > 0)
                        ImGui.SameLine();

                    var isColorUsed = PlayerColors.IsColorUsed(_players, _gameState.PlayerCount, i);
                    var color = PlayerColors.Colors[i];
                    var colorF = new Vector4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, 1.0f);
                    if (ImGui.ColorButton($"Color_{i}", colorF) && !isColorUsed)
                    {
                        _writer.Reset();
                        _writer.Put((byte)PacketType.SetColorIndex);
                        _writer.Put(i);
                        _client.FirstPeer.Send(_writer, DeliveryMethod.ReliableOrdered);
                    }
                }

                if (!_isReady)
                {
                    if (ImGui.Button("Ready?"))
                    {
                        _isReady = true;

                        _writer.Reset();
                        _writer.Put((byte)PacketType.PlayerReady);
                        _client.FirstPeer.Send(_writer, DeliveryMethod.ReliableOrdered);
                    }
                }

                _imgui.End();
            }
        }

        private void SetStateConnected()
        {
            _state = ClientState.Connected;
            _connectedAt = _timer.ElapsedMilliseconds;
            _packetCount = 0;
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

                ImGui.SetNextWindowSize(new Vector2(400, 200), ImGuiCond.Always);
                ImGui.SetNextWindowPos(new Vector2(Raylib.GetScreenWidth() - 420, 20), ImGuiCond.Always);
                ImGui.Begin("Statistics", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);

                ImGui.Text($"FPS: {Raylib.GetFPS()}");
                if (_client.ConnectedPeersCount > 0)
                {
                    ImGui.Text($"Ping: {_client.FirstPeer.Ping}");
                    ImGui.Text($"RX Packets: {_packetCount}");

                    var connectedTime = (_timer.ElapsedMilliseconds - _connectedAt) / 1000.0;
                    ImGui.Text($"RX P/S: {_packetCount / connectedTime:0.00}");
                }

                _imgui.End();

                Raylib.EndDrawing();
            }
        }

    }
}
