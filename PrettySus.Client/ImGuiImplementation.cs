using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Raylib_cs;
using ImGuiNET;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.IO;

namespace PrettySus.Client
{
    class ImGuiImplementation : IDisposable
    {
        [FixedAddressValueType]
        private static SetClipboardDelegate _setClipboardFn;

        [FixedAddressValueType]
        private static GetClipboardDelegate _getClipboardFn;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void SetClipboardDelegate(IntPtr data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate IntPtr GetClipboardDelegate();

        public unsafe ImGuiImplementation()
        {
            ImGui.SetCurrentContext(ImGui.CreateContext());

            var io = ImGui.GetIO();

            // Key maps
            io.KeyMap[(int)ImGuiKey.Tab] = (int)KeyboardKey.KEY_TAB;
            io.KeyMap[(int)ImGuiKey.LeftArrow] = (int)KeyboardKey.KEY_LEFT;
            io.KeyMap[(int)ImGuiKey.RightArrow] = (int)KeyboardKey.KEY_RIGHT;
            io.KeyMap[(int)ImGuiKey.UpArrow] = (int)KeyboardKey.KEY_UP;
            io.KeyMap[(int)ImGuiKey.DownArrow] = (int)KeyboardKey.KEY_DOWN;
            io.KeyMap[(int)ImGuiKey.PageUp] = (int)KeyboardKey.KEY_PAGE_DOWN;
            io.KeyMap[(int)ImGuiKey.PageDown] = (int)KeyboardKey.KEY_PAGE_UP;
            io.KeyMap[(int)ImGuiKey.Home] = (int)KeyboardKey.KEY_HOME;
            io.KeyMap[(int)ImGuiKey.End] = (int)KeyboardKey.KEY_END;
            io.KeyMap[(int)ImGuiKey.Insert] = (int)KeyboardKey.KEY_INSERT;
            io.KeyMap[(int)ImGuiKey.Delete] = (int)KeyboardKey.KEY_DELETE;
            io.KeyMap[(int)ImGuiKey.Backspace] = (int)KeyboardKey.KEY_BACKSPACE;
            io.KeyMap[(int)ImGuiKey.Space] = (int)KeyboardKey.KEY_SPACE;
            io.KeyMap[(int)ImGuiKey.Enter] = (int)KeyboardKey.KEY_ENTER;
            io.KeyMap[(int)ImGuiKey.Escape] = (int)KeyboardKey.KEY_ESCAPE;
            io.KeyMap[(int)ImGuiKey.KeyPadEnter] = (int)KeyboardKey.KEY_KP_ENTER;
            io.KeyMap[(int)ImGuiKey.A] = (int)KeyboardKey.KEY_A;
            io.KeyMap[(int)ImGuiKey.C] = (int)KeyboardKey.KEY_C;
            io.KeyMap[(int)ImGuiKey.V] = (int)KeyboardKey.KEY_V;
            io.KeyMap[(int)ImGuiKey.X] = (int)KeyboardKey.KEY_X;
            io.KeyMap[(int)ImGuiKey.Y] = (int)KeyboardKey.KEY_Y;
            io.KeyMap[(int)ImGuiKey.Z] = (int)KeyboardKey.KEY_Z;

            io.MousePos = new Vector2(float.MaxValue, -float.MaxValue);

            // Clip board
            _setClipboardFn = SetClipboard;
            _getClipboardFn = GetClipboard;

            io.SetClipboardTextFn = Marshal.GetFunctionPointerForDelegate(_setClipboardFn);
            io.GetClipboardTextFn = Marshal.GetFunctionPointerForDelegate(_getClipboardFn);

            io.ClipboardUserData = IntPtr.Zero;

            // Fonts
            var fontData = File.ReadAllBytes("Assets/Fonts/Roboto-Regular.ttf");
            fixed (byte* ptr = fontData)
            {
                ImGui.GetIO().Fonts.AddFontFromMemoryTTF((IntPtr)ptr, fontData.Length, 16.0f);
            }

            var image = new Image();
            io.Fonts.GetTexDataAsRGBA32(out image.data, out image.width, out image.height);

            image.mipmaps = 1;
            image.format = PixelFormat.PIXELFORMAT_UNCOMPRESSED_R8G8B8A8;

            var texture = Raylib.LoadTextureFromImage(image);
            io.Fonts.SetTexID((IntPtr)texture.id);

            // Cleanup
            io.Fonts.ClearTexData();
        }

        public void Dispose()
        {
            ImGui.DestroyContext();
        }

        private void SetClipboard(IntPtr data)
            => Raylib.SetClipboardText(Marshal.PtrToStringAnsi(data));

        private IntPtr GetClipboard()
            => Marshal.StringToHGlobalAnsi(Raylib.GetClipboardText());

        public void ProcessEvents()
        {
            var io = ImGui.GetIO();

            for (var i = (int)KeyboardKey.KEY_NULL; i < (int)KeyboardKey.KEY_KB_MENU; i++)
            {
                if (i >= io.KeysDown.Count)
                    continue;

                io.KeysDown[i] = Raylib.IsKeyDown((KeyboardKey)i);
            }

            io.AddInputCharacter((uint)Raylib.GetKeyPressed());
        }

        public void Begin()
        {
            var io = ImGui.GetIO();

            io.DisplaySize = new Vector2(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
            io.DeltaTime = Raylib.GetFrameTime();

            io.KeyCtrl = Raylib.IsKeyDown(KeyboardKey.KEY_RIGHT_CONTROL) || Raylib.IsKeyDown(KeyboardKey.KEY_LEFT_CONTROL);
            io.KeyShift = Raylib.IsKeyDown(KeyboardKey.KEY_RIGHT_SHIFT) || Raylib.IsKeyDown(KeyboardKey.KEY_LEFT_SHIFT);
            io.KeyAlt = Raylib.IsKeyDown(KeyboardKey.KEY_RIGHT_ALT) || Raylib.IsKeyDown(KeyboardKey.KEY_LEFT_ALT);
            io.KeySuper = Raylib.IsKeyDown(KeyboardKey.KEY_RIGHT_SUPER) || Raylib.IsKeyDown(KeyboardKey.KEY_LEFT_SUPER);

            UpdateMousePositionAndButtons();
            UpdateMouseCursor();

            if (Raylib.GetMouseWheelMove() > 0)
                io.MouseWheel += 1;
            else if (Raylib.GetMouseWheelMove() < 0)
                io.MouseWheel -= 1;

            ImGui.NewFrame();
        }

        private void UpdateMousePositionAndButtons()
        {
            var io = ImGui.GetIO();

            // Set OS mouse position if requested (rarely used, only when ImGuiConfigFlags_NavEnableSetMousePos is enabled by user)
            if (io.WantSetMousePos)
                Raylib.SetMousePosition((int)io.MousePos.X, (int)io.MousePos.Y);
            else
                io.MousePos = new Vector2(float.MinValue, -float.MaxValue);

            io.MouseDown[0] = Raylib.IsMouseButtonDown(MouseButton.MOUSE_LEFT_BUTTON);
            io.MouseDown[1] = Raylib.IsMouseButtonDown(MouseButton.MOUSE_RIGHT_BUTTON);
            io.MouseDown[2] = Raylib.IsMouseButtonDown(MouseButton.MOUSE_MIDDLE_BUTTON);

            if (!Raylib.IsWindowMinimized())
                io.MousePos = new Vector2(Raylib.GetMouseX(), Raylib.GetMouseY());
        }

        private void UpdateMouseCursor()
        {
            var io = ImGui.GetIO();

            if (io.ConfigFlags.HasFlag(ImGuiConfigFlags.NoMouseCursorChange))
                return;

            var imgui_cursor = ImGui.GetMouseCursor();
            if (io.MouseDrawCursor || imgui_cursor == ImGuiMouseCursor.None)
            {
                // Hide OS mouse cursor if imgui is drawing it or if it wants no cursor
                Raylib.HideCursor();
            }
            else
            {
                // Show OS mouse cursor
                Raylib.ShowCursor();
            }
        }

        public void End()
        {
            ImGui.EndFrame();
            ImGui.Render();

            Rlgl.rlDisableBackfaceCulling();

            var drawData = ImGui.GetDrawData();
            for (int n = 0; n < drawData.CmdListsCount; n++)
            {
                var cmdList = drawData.CmdListsRange[n];

                for (var cmdI = 0; cmdI < cmdList.CmdBuffer.Size; cmdI++)
                {
                    var cmd = cmdList.CmdBuffer[cmdI];
                    if (cmd.UserCallback != IntPtr.Zero)
                    {
                        throw new NotImplementedException();
                    }
                    else
                    {
                        var pos = drawData.DisplayPos;
                        int rectX = (int)(cmd.ClipRect.X - pos.X);
                        int rectY = (int)(cmd.ClipRect.Y - pos.Y);
                        int rectW = (int)(cmd.ClipRect.Z - rectX);
                        int rectH = (int)(cmd.ClipRect.W - rectY);

                        Raylib.BeginScissorMode(rectX, rectY, rectW, rectH);

                        var textureId = (uint)cmd.TextureId;
                        for (var i = 0; i < cmd.ElemCount; i += 3)
                        {
                            Rlgl.rlBegin(Rlgl.RL_TRIANGLES);
                            Rlgl.rlSetTexture(textureId);

                            ref var index = ref cmdList.IdxBuffer[(int)cmd.IdxOffset + i];
                            var vertex = cmdList.VtxBuffer[(int)cmd.VtxOffset + index];
                            DrawTriangleVertex(vertex);

                            index = cmdList.IdxBuffer[(int)cmd.IdxOffset + i + 2];
                            vertex = cmdList.VtxBuffer[(int)cmd.VtxOffset + index];
                            DrawTriangleVertex(vertex);

                            index = cmdList.IdxBuffer[(int)cmd.IdxOffset + i + 1];
                            vertex = cmdList.VtxBuffer[(int)cmd.VtxOffset + index];
                            DrawTriangleVertex(vertex);

                            Rlgl.rlEnd();
                            Rlgl.rlSetTexture(0);
                        }

                        Raylib.EndScissorMode();
                    }
                }
            }

            Rlgl.rlDrawRenderBatchActive();
            Rlgl.rlEnableBackfaceCulling();
        }

        private static unsafe void DrawTriangleVertex(ImDrawVertPtr vertex)
        {
            var c = vertex.col;
            var r = (byte)c;
            var g = (byte)(c >> 8);
            var b = (byte)(c >> 16);
            var a = (byte)(c >> 24);

            Rlgl.rlColor4ub(r, g, b, a);
            Rlgl.rlTexCoord2f(vertex.uv.X, vertex.uv.Y);
            Rlgl.rlVertex2f(vertex.pos.X, vertex.pos.Y);
        }
    }
}
