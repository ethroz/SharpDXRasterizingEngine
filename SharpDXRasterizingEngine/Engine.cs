using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Windows;
using SharpDX.DirectInput;
using SharpDX.DXGI;
using SharpDX.Direct3D;
using D3D11 = SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using Point = System.Drawing.Point;
using Color = SharpDX.Color;
using System.Windows.Forms;
using System.IO;
using System.Drawing;

namespace SharpDXRasterizingEngine
{
    sealed class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            using (Engine engine = new Engine())
            {
                engine.Run();
            }
        }
    }

    public class Engine : IDisposable
    {
        private RenderForm renderForm;
        private D3D11.Device d3dDevice;
        private D3D11.DeviceContext d3dDeviceContext;
        private SwapChain swapChain;
        private D3D11.RenderTargetView renderTargetView;
        private VertexPositionColor[] vertices = new VertexPositionColor[]
        {
            new VertexPositionColor(new Vector3(-1.0f, 1.0f, 0.0f), Color.Red),
            new VertexPositionColor(new Vector3(1.0f, 1.0f, 0.0f), Color.Blue),
            new VertexPositionColor(new Vector3(1.0f, -1.0f, 0.0f), Color.Green),
            new VertexPositionColor(new Vector3(-1.0f, 1.0f, 0.0f), Color.Red),
            new VertexPositionColor(new Vector3(1.0f, -1.0f, 0.0f), Color.Green),
            new VertexPositionColor(new Vector3(-1.0f, -1.0f, 0.0f), Color.Yellow)
        };
        private List<VertexPositionColor> vertexp;
        private D3D11.Buffer triangleVertexbuffer;
        private D3D11.Buffer triangleVertexPointbuffer;
        private D3D11.VertexShader vertexShader;
        private D3D11.PixelShader pixelShader;
        private D3D11.InputElement[] inputElements = new D3D11.InputElement[]
        {
            new D3D11.InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, D3D11.InputClassification.PerVertexData, 0),
            new D3D11.InputElement("COLOR", 0, Format.R32G32B32A32_Float, 12, 0, D3D11.InputClassification.PerVertexData, 0)
        };
        private ShaderSignature inputSignature;
        private D3D11.InputLayout inputLayout;
        private Viewport viewport;

        private Mouse mouse;
        //private int ScrollDelta = 0;
        private Button[] buttons;
        private Point MousePos;
        private Point PrevMousePos;
        private Keyboard keyboard;
        private Chey[] cheyArray;

        private PrimitiveTopology topology = PrimitiveTopology.TriangleList;
        private int RefreshRate = 60;
        private int SampleCount = 1; // 1 - 16
        private int Width = 1920, Height = 1080; // not reccomended to exceed display size
        private bool Running = true;
        public enum WindowState { Normal, Minimized, Maximized, FullScreen };
        private WindowState State = WindowState.FullScreen;

        private List<float> fpsList = new List<float>();
        public float elapsedTime;
        private long t1, t2;
        private System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

        private readonly Vec3D vCameraReset = new Vec3D(0.0f, 0.0f, -3.0f);
        private Vec3D vCamera;
        private Vec3D vForward = new Vec3D();
        private Vec3D vUp = new Vec3D();
        private Vec3D vRight = new Vec3D();
        private Vec3D vLight = new Vec3D(0.0f, 0.0f, -3.0f);
        private float Pitch = 0.0f;
        private float Yaw = 0.0f;
        private float MoveSpeed = 8.0f;
        private float RotateSpeed = 4.0f;
        private Mesh mesh = new Mesh();
        private Mesh light = new Mesh();
        private Mesh axis = new Mesh();
        private Mesh tesseract = new Mesh();
        private Frame fTesseract = new Frame();
        private Mat4x4 matProj = new Mat4x4();
        private List<Tria> trisToRaster = new List<Tria>();
        private static bool Clip = false;
        private float ForwardClip = 0.1f;
        private float Lumens = 200.0f;
        private float MinBrightness = 0.05f;
        private Vec3D translate = new Vec3D(0.0f, 0.0f, 3.0f);
        private Vec3D scale = new Vec3D(0.5f, 0.5f, 0.5f);
        private float theta = 0.0f;
        private Vec4D tAngles = new Vec4D();

        private bool Test()
        {
            return false;
        }

        public Engine()
        {
            if (Test())
            {
                Console.ReadKey();
                Environment.Exit(0);
            }

            renderForm = new RenderForm("SharpDXRasterizingEngine")
            {
                ClientSize = new Size(Width, Height),
                AllowUserResizing = true
            };
            if (State == WindowState.FullScreen)
            {
                renderForm.TopMost = true;
                renderForm.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                renderForm.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            }
            else if (State == WindowState.Maximized)
            {
                renderForm.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            }
            else if (State == WindowState.Minimized)
            {
                renderForm.TopMost = false;
                renderForm.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
                renderForm.WindowState = System.Windows.Forms.FormWindowState.Minimized;
            }

            InitializeMouse();
            InitializeKeyboard();
            InitializeDeviceResources();
            InitializeShaders();
            InitializeTriangle();
            d3dDeviceContext.OutputMerger.SetRenderTargets(renderTargetView);
            OnStart();
            sw.Start();
            t1 = sw.ElapsedTicks;
        }

        public Engine(int width, int height, int fps = 0, int antiAliasMult = 1, WindowState state = WindowState.Normal)
        {
            Width = width;
            Height = height;
            RefreshRate = fps;
            SampleCount = antiAliasMult;
            State = state;

            renderForm = new RenderForm("SharpDXRasterizingEngine")
            {
                ClientSize = new Size(Width, Height),
                AllowUserResizing = true
            };
            if (State == WindowState.FullScreen)
            {
                renderForm.TopMost = true;
                renderForm.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                renderForm.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            }
            else if (State == WindowState.Maximized)
            {
                renderForm.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            }
            else if (State == WindowState.Minimized)
            {
                renderForm.TopMost = false;
                renderForm.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
                renderForm.WindowState = System.Windows.Forms.FormWindowState.Minimized;
            }

            InitializeMouse();
            InitializeKeyboard();
            InitializeDeviceResources();
            InitializeShaders();
            InitializeTriangle();
            d3dDeviceContext.OutputMerger.SetRenderTargets(renderTargetView);
            OnStart();
            sw.Start();
            t1 = sw.ElapsedTicks;
        }

        public void Run()
        {
            RenderLoop.Run(renderForm, RenderCallBack);
        }

        private void RenderCallBack()
        {
            Draw();
        }

        private void InitializeMouse()
        {
            mouse = new Mouse(new DirectInput());
            mouse.Acquire();
            var allButtons = mouse.GetCurrentState().Buttons;
            buttons = new Button[allButtons.Length];
            for (int i = 0; i < allButtons.Length; i++)
                buttons[i] = new Button();
            MousePos = Cursor.Position;
            Console.WriteLine(allButtons.Length);
        }

        private void InitializeKeyboard()
        {
            keyboard = new Keyboard(new DirectInput());
            keyboard.Properties.BufferSize = 128;
            keyboard.Acquire();
            var state = keyboard.GetCurrentState();
            var allKeys = state.AllKeys;
            cheyArray = new Chey[allKeys.Count];
            for (int i = 0; i < allKeys.Count; i++)
                cheyArray[i] = new Chey(allKeys[i]);
        }

        private void InitializeDeviceResources()
        {
            ModeDescription backBufferDesc = new ModeDescription(Width, Height, new Rational(10000, 1), Format.R8G8B8A8_UNorm);
            SwapChainDescription swapChainDesc = new SwapChainDescription()
            {
                ModeDescription = backBufferDesc,
                SampleDescription = new SampleDescription(SampleCount, 0),
                Usage = Usage.RenderTargetOutput,
                BufferCount = 1,
                OutputHandle = renderForm.Handle,
                IsWindowed = true
            };
            D3D11.Device.CreateWithSwapChain(DriverType.Hardware, D3D11.DeviceCreationFlags.None, swapChainDesc, out d3dDevice, out swapChain);
            d3dDeviceContext = d3dDevice.ImmediateContext;
            using (D3D11.Texture2D backBuffer = swapChain.GetBackBuffer<D3D11.Texture2D>(0))
            {
                renderTargetView = new D3D11.RenderTargetView(d3dDevice, backBuffer);
            }
            viewport = new Viewport(0, 0, Width, Height);
            d3dDeviceContext.Rasterizer.SetViewport(viewport);
        }

        private void InitializeShaders()
        {
            using (var vertexShaderByteCode = ShaderBytecode.CompileFromFile("Shaders.hlsl", "vertexShader", "vs_4_0", ShaderFlags.Debug))
            {
                inputSignature = ShaderSignature.GetInputSignature(vertexShaderByteCode);
                vertexShader = new D3D11.VertexShader(d3dDevice, vertexShaderByteCode);
            }
            using (var pixelShaderByteCode = ShaderBytecode.CompileFromFile("Shaders.hlsl", "pixelShader", "ps_4_0", ShaderFlags.Debug))
            {
                pixelShader = new D3D11.PixelShader(d3dDevice, pixelShaderByteCode);
            }

            d3dDeviceContext.VertexShader.Set(vertexShader);
            d3dDeviceContext.PixelShader.Set(pixelShader);

            d3dDeviceContext.InputAssembler.PrimitiveTopology = topology;

            inputLayout = new D3D11.InputLayout(d3dDevice, inputSignature, inputElements);
            d3dDeviceContext.InputAssembler.InputLayout = inputLayout;
        }

        private void InitializeTriangle()
        {
            triangleVertexbuffer = D3D11.Buffer.Create(d3dDevice, D3D11.BindFlags.VertexBuffer, vertices);
        }

        private void InitializeTriangleStrip()
        {
            triangleVertexPointbuffer = D3D11.Buffer.Create(d3dDevice, D3D11.BindFlags.VertexBuffer, vertexp.ToArray());
        }

        private void GetTime()
        {
            t2 = sw.ElapsedTicks;
            elapsedTime = (t2 - t1) / 10000000.0f;
            if (RefreshRate != 0)
            {
                while (1.0f / elapsedTime > RefreshRate * 3.35f)
                {
                    t2 = sw.ElapsedTicks;
                    elapsedTime = (t2 - t1) / 10000000.0f;
                }
            }
            //fpsList.Add(1.0f / elapsedTime);
            //float sum = 0;
            //for (int i = 0; i < fpsList.Count; i++)
            //    sum += fpsList[i];
            //sum /= fpsList.Count;
            //Console.WriteLine("Average FPS: " + sum);
            t1 = t2;
            renderForm.Text = "SharpDXRasterizingEngine   FPS: " + 1.0f / (elapsedTime * 3.35f);
        }

        private void GetMouseData()
        {
            mouse.Poll();
            var state = mouse.GetCurrentState().Buttons;
            for (int i = 0; i < state.Length; i++)
            {
                bool pressed = state[i];
                buttons[i].Down = buttons[i].Raised && pressed;
                buttons[i].Up = buttons[i].Held && !pressed;
                buttons[i].Held = pressed;
                buttons[i].Raised = !pressed;
            }
            PrevMousePos = MousePos;
            MousePos = Cursor.Position;
        }

        private void GetKeys()
        {
            keyboard.Poll();
            var state = keyboard.GetCurrentState();
            for (int i = 0; i < cheyArray.Length; i++)
            {
                bool pressed = state.IsPressed(cheyArray[i].key);
                cheyArray[i].Down = cheyArray[i].Raised && pressed;
                cheyArray[i].Up = cheyArray[i].Held && !pressed;
                cheyArray[i].Held = pressed;
                cheyArray[i].Raised = !pressed;
            }
        }

        public bool KeyDown(Key key)
        {
            return FindChey(key).Down;
        }

        public bool KeyUp(Key key)
        {
            return FindChey(key).Up;
        }

        public bool KeyHeld(Key key)
        {
            return FindChey(key).Held;
        }

        public bool KeyRaised(Key key)
        {
            return FindChey(key).Raised;
        }

        private Chey FindChey(Key key)
        {
            for (int i = 0; i < cheyArray.Length; i++)
            {
                if (cheyArray[i].key == key)
                    return cheyArray[i];
            }
            return null;
        }

        public bool ButtonDown(int button)
        {
            return buttons[button].Down;
        }

        public bool ButtonUp(int button)
        {
            return buttons[button].Up;
        }

        public bool ButtonHeld(int button)
        {
            return buttons[button].Held;
        }

        public bool ButtonRaised(int button)
        {
            return buttons[button].Raised;
        }

        public Point GetPosition()
        {
            return MousePos;
        }

        public Point DeltaMousePos()
        {
            return new Point(MousePos.X - PrevMousePos.X, MousePos.Y - PrevMousePos.Y);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool boolean)
        {
            mouse.Dispose();
            keyboard.Dispose();
            inputLayout.Dispose();
            inputSignature.Dispose();
            if (triangleVertexPointbuffer != null)
                triangleVertexPointbuffer.Dispose();
            triangleVertexbuffer.Dispose();
            vertexShader.Dispose();
            pixelShader.Dispose();
            renderTargetView.Dispose();
            swapChain.Dispose();
            d3dDevice.Dispose();
            d3dDeviceContext.Dispose();
            renderForm.Dispose();
        }

        /////////////////////////////////////

        private void Draw()
        {
            GetMouseData();
            GetKeys();
            UserInput();
            if (!Running)
                return;
            OnUpdate();

            d3dDeviceContext.ClearRenderTargetView(renderTargetView, Color.Black);

            if (vertexp.Count > 0)
            {
                InitializeTriangleStrip();
                d3dDeviceContext.InputAssembler.SetVertexBuffers(0, new D3D11.VertexBufferBinding(triangleVertexPointbuffer, Utilities.SizeOf<VertexPositionColor>(), 0));
                d3dDeviceContext.Draw(vertexp.Count, 0);
            }

            //InitializeTriangle();
            //d3dDeviceContext.InputAssembler.SetVertexBuffers(0, new D3D11.VertexBufferBinding(triangleVertexbuffer, Utilities.SizeOf<VertexPositionColor>(), 0));
            //d3dDeviceContext.Draw(vertices.Length, 0);

            swapChain.Present(0, PresentFlags.None);
            GetTime();
            //System.Threading.Thread.Sleep(10000);
        }

        public void OnStart()
        {
            if (!mesh.LoadFromObjectFile(@"C:\Users\ethro\source\repos\SharpDXRenderEngine\SharpDXRenderEngine\Objects\Test.obj"))
            {
                Console.WriteLine("Cube not found");
                throw new FileNotFoundException();
            }
            if (!light.LoadFromObjectFile(@"C:\Users\ethro\source\repos\SharpDXRenderEngine\SharpDXRenderEngine\Objects\Test.obj"))
            {
                Console.WriteLine("Cube not found");
                throw new FileNotFoundException();
            }

            axis = MeshMakeAxis(1.0f);
            tesseract = MeshMakeTesseract();
            fTesseract = FrameMakeTesseract();

            matProj = MatrixMakeProjection(60.0f, (float)Height / Width, ForwardClip, 1000.0f);
            vCamera = vCameraReset;
        }

        public void UserInput()
        {
            if (KeyDown(Key.P))
            {
                Running = !Running;
            }
            if (!Running)
                return;

            if (KeyDown(Key.LeftShift))
                MoveSpeed += 1.0f;
            if (KeyDown(Key.LeftControl))
                MoveSpeed -= 1.0f;
            if (MoveSpeed < 1.0f)
                MoveSpeed = 1.0f;
            if (KeyDown(Key.RightShift))
                RotateSpeed += 1.0f;
            if (KeyDown(Key.RightControl))
                RotateSpeed -= 1.0f;
            if (RotateSpeed < 1.0f)
                RotateSpeed = 1.0f;

            if (KeyHeld(Key.E))
                vCamera += vUp * MoveSpeed * elapsedTime;
            if (KeyHeld(Key.Q))
                vCamera -= vUp * MoveSpeed * elapsedTime;

            if (KeyHeld(Key.D))
                vCamera += vRight * MoveSpeed * elapsedTime;
            if (KeyHeld(Key.A))
                vCamera -= vRight * MoveSpeed * elapsedTime;

            if (KeyHeld(Key.W))
                vCamera += vForward * MoveSpeed * elapsedTime;
            if (KeyHeld(Key.S))
                vCamera -= vForward * MoveSpeed * elapsedTime;

            //Point mos = DeltaMousePos();
            //Yaw -= mos.X * elapsedTime;
            //Pitch += mos.Y * elapsedTime;

            if (KeyHeld(Key.Left))
                Yaw += RotateSpeed * elapsedTime;
            if (KeyHeld(Key.Right))
                Yaw -= RotateSpeed * elapsedTime;
            if (KeyHeld(Key.Down))
                Pitch += RotateSpeed * elapsedTime;
            if (KeyHeld(Key.Up))
                Pitch -= RotateSpeed * elapsedTime;
            if (Pitch > Math.PI / 2.0)
                Pitch = (float)(Math.PI / 2.0);
            if (Pitch < Math.PI / -2.0)
                Pitch = (float)(Math.PI / -2.0);

            if (KeyHeld(Key.O))
                vLight.y += MoveSpeed * elapsedTime;
            if (KeyHeld(Key.U))
                vLight.y -= MoveSpeed * elapsedTime;
            if (KeyHeld(Key.L))
                vLight.x += MoveSpeed * elapsedTime;
            if (KeyHeld(Key.J))
                vLight.x -= MoveSpeed * elapsedTime;
            if (KeyHeld(Key.I))
                vLight.z += MoveSpeed * elapsedTime;
            if (KeyHeld(Key.K))
                vLight.z -= MoveSpeed * elapsedTime;

            if (KeyHeld(Key.Comma))
                Lumens -= 10;
            if (KeyHeld(Key.Period))
                Lumens += 10;

            if (KeyHeld(Key.NumberPad8))
                tAngles.w += 3.0f * elapsedTime;
            if (KeyHeld(Key.NumberPad5))
                tAngles.w -= 3.0f * elapsedTime;
            if (KeyHeld(Key.NumberPad4))
                tAngles.z -= 3.0f * elapsedTime;
            if (KeyHeld(Key.NumberPad6))
                tAngles.z += 3.0f * elapsedTime;

            if (KeyHeld(Key.T))
                tAngles.x += 3.0f * elapsedTime;
            if (KeyHeld(Key.G))
                tAngles.x -= 3.0f * elapsedTime;
            if (KeyHeld(Key.F))
                tAngles.y += 3.0f * elapsedTime;
            if (KeyHeld(Key.H))
                tAngles.y -= 3.0f * elapsedTime;

            if (KeyDown(Key.R))
            {
                vCamera = vCameraReset;
                Yaw = 0;
                Pitch = 0;
                tAngles = new Vec4D();
            }

            if (KeyDown(Key.C))
                Clip = !Clip;

            if (KeyDown(Key.Tab))
            {
                CycleWindowState();
            }

            if (KeyDown(Key.Escape))
                Environment.Exit(0);
        }

        public void OnUpdate()
        {
            //theta += 1f * elapsedTime;
            Mat4x4 meshTrans = MatrixMakeTranslation(translate);
            Mat4x4 lightTrans = MatrixMakeTranslation(vLight);

            Mat4x4 RotXY = MatrixMakeRotXY(0);
            Mat4x4 RotXW = MatrixMakeRotXW(tAngles.y);
            Mat4x4 RotYW = MatrixMakeRotYW(-tAngles.x);
            Mat4x4 RotXZ = MatrixMakeRotXZ(tAngles.z);
            Mat4x4 RotYZ = MatrixMakeRotYZ(tAngles.w);
            Mat4x4 RotZW = MatrixMakeRotZW(0);
            Mat4x4 rot4 = RotZW * (RotYW * (RotYZ * (RotXW * (RotXZ * RotXY))));
            Mat4x4 rot3 = RotYZ * (RotXZ * RotXY);

            Mat4x4 meshScale = MatrixMakeScalation(scale);
            Mat4x4 lightScale = MatrixMakeScalation(new Vec3D(0.1f, 0.1f, 0.1f));

            Mat4x4 world = MatrixMakeIdentity();
            Mat4x4 lightWorld = world * lightTrans;

            Vec3D vForwards = new Vec3D(0.0f, 0.0f, 1.0f);
            Vec3D vUpwards = new Vec3D(0.0f, 1.0f, 0.0f);
            Vec3D vRightwards = new Vec3D(1.0f, 0.0f, 0.0f);
            Mat4x4 cameraRot = MatrixMakeRotYZ(Pitch) * MatrixMakeRotXZ(Yaw);
            vForward = cameraRot * vForwards;
            vUp = cameraRot * vUpwards;
            vRight = cameraRot * vRightwards;
            vForwards = vCamera + vForward;

            Mat4x4 matCamera = MatrixPointAt(vCamera, vForwards, vUpwards);
            Mat4x4 matView = MatrixQuickInverse(matCamera);

            trisToRaster = new List<Tria>();
            vertexp = new List<VertexPositionColor>();

            // mesh
            Render3DObject(mesh, world * MatrixMakeTranslation(new Vec3D(0.0f, 0.0f, 0.0f)), matView, new Mat4x4(), rot4, meshScale);

            // light
            RenderLight(light, lightWorld, matView, lightScale);

            // axis
            Render3DObject(axis, world * MatrixMakeTranslation(new Vec3D(0.0f, 0.0f, 0.0f)), matView, new Mat4x4(), rot3, MatrixMakeScalation(new Vec3D(1.0f, 1.0f, 1.0f)));

            // tesseract
            //Render4DWireFrame(fTesseract, world, matView, new Vec3D(0.0f, 0.0f, 3.0f, 0.0f), rot4, MatrixMakeScalation(new Vec3D(1.0f, 1.0f, 1.0f)));
            //Render4DObject(tesseract, world * MatrixMakeTranslation(new Vec3D(0.0f, 0.0f, 0.0f)), matView, new Mat4x4(), rot, MatrixMakeScalation(new Vec3D(1.0f, 1.0f, 1.0f)));

            // sort
            trisToRaster.Sort();

            // add triangles or lines
            if (topology == PrimitiveTopology.TriangleList)
            {
                foreach (Tria t in trisToRaster)
                    for (int k = 0; k < 3; k++)
                        vertexp.Add(new VertexPositionColor(new Vector3(t.p[k].x, t.p[k].y, 0.0f), Color4.Lerp(Color4.Black, t.p[k].color, t.p[k].lumination)));
            }
            else
            {
                foreach (Tria t in trisToRaster)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        vertexp.Add(new VertexPositionColor(new Vector3(t.p[k].x, t.p[k].y, 0.0f), Color4.Lerp(Color4.Black, t.p[k].color, t.p[k].lumination)));
                        vertexp.Add(new VertexPositionColor(new Vector3(t.p[(k + 1) % 3].x, t.p[(k + 1) % 3].y, 0.0f), Color4.Lerp(Color4.Black, t.p[(k + 1) % 3].color, t.p[(k + 1) % 3].lumination)));
                    }
                }
            }
        }
 
        /////////////////////////////////////

        public void CycleWindowState()
        {
            switch (State)
            {
                case WindowState.Minimized:
                    State = WindowState.Normal;
                    renderForm.TopMost = false;
                    renderForm.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
                    renderForm.WindowState = System.Windows.Forms.FormWindowState.Normal;
                    break;
                case WindowState.Normal:
                    State = WindowState.Maximized;
                    renderForm.TopMost = false;
                    renderForm.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
                    renderForm.WindowState = System.Windows.Forms.FormWindowState.Maximized;
                    break;
                case WindowState.Maximized:
                    State = WindowState.FullScreen;
                    renderForm.TopMost = true;
                    renderForm.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                    renderForm.WindowState = System.Windows.Forms.FormWindowState.Normal;
                    renderForm.WindowState = System.Windows.Forms.FormWindowState.Maximized;
                    break;
                case WindowState.FullScreen:
                    State = WindowState.Minimized;
                    renderForm.TopMost = false;
                    renderForm.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
                    renderForm.WindowState = System.Windows.Forms.FormWindowState.Minimized;
                    break;
            }
        }

        private Vec3D AveragePoint(Vec3D[] p)
        {
            return new Vec3D((p[0].x + p[1].x + p[2].x) / 3.0f, 
                (p[0].y + p[1].y + p[2].y) / 3.0f, 
                (p[0].z + p[1].z + p[2].z) / 3.0f);
        }
        
        private List<Tria> PopFront(List<Tria> list)
        {
            List<Tria> temp = new List<Tria>();
            for (int i = 0; i < list.Count - 1; i++)
                temp.Add(list[i + 1]);
            return temp;
        }

        private Vec3D[] TestReverse(Vec3D[] p)
        {
            if (0 >= (p[1].x - p[0].x) * (p[2].y - p[0].y) - (p[1].y - p[0].y) * (p[2].x - p[0].x))
                return p;
            return Reverse(p);
        }

        private Vec3D[] Reverse(Vec3D[] p)
        {
            Vec3D[] temp = new Vec3D[p.Length];
            for (int i = 0; i < temp.Length; i++)
            {
                temp[i] = new Vec3D(p[temp.Length - i - 1]);
            }
            return temp;
        }

        private Mat4x4 MatrixMakeIdentity()
        {
            Mat4x4 mat4 = new Mat4x4();
            mat4.m[0][0] = 1.0f;
            mat4.m[1][1] = 1.0f;
            mat4.m[2][2] = 1.0f;
            mat4.m[3][3] = 1.0f;
            return mat4;
        }

        private Mat4x4 MatrixMakeRotYZ(float fAngleRad)
        {
            Mat4x4 mat4 = new Mat4x4();
            mat4.m[0][0] = 1.0f;
            mat4.m[1][1] = (float)Math.Cos(fAngleRad);
            mat4.m[1][2] = (float)Math.Sin(fAngleRad);
            mat4.m[2][1] = -(float)Math.Sin(fAngleRad);
            mat4.m[2][2] = (float)Math.Cos(fAngleRad);
            mat4.m[3][3] = 1.0f;
            return mat4;
        }

        private Mat4x4 MatrixMakeRotXZ(float fAngleRad)
        {
            Mat4x4 mat4 = new Mat4x4();
            mat4.m[0][0] = (float)Math.Cos(fAngleRad);
            mat4.m[0][2] = (float)Math.Sin(fAngleRad);
            mat4.m[2][0] = -(float)Math.Sin(fAngleRad);
            mat4.m[1][1] = 1.0f;
            mat4.m[2][2] = (float)Math.Cos(fAngleRad);
            mat4.m[3][3] = 1.0f;
            return mat4;
        }

        private Mat4x4 MatrixMakeRotXY(float fAngleRad)
        {
            Mat4x4 mat4 = new Mat4x4();
            mat4.m[0][0] = (float)Math.Cos(fAngleRad);
            mat4.m[0][1] = (float)Math.Sin(fAngleRad);
            mat4.m[1][0] = -(float)Math.Sin(fAngleRad);
            mat4.m[1][1] = (float)Math.Cos(fAngleRad);
            mat4.m[2][2] = 1.0f;
            mat4.m[3][3] = 1.0f;
            return mat4;
        }

        private Mat4x4 MatrixMakeRotXW(float fAngleRad)
        {
            Mat4x4 mat4 = new Mat4x4();
            mat4.m[0][0] = (float)Math.Cos(fAngleRad);
            mat4.m[0][3] = (float)Math.Sin(fAngleRad);
            mat4.m[1][1] = 1.0f;
            mat4.m[2][2] = 1.0f;
            mat4.m[3][0] = -(float)Math.Sin(fAngleRad);
            mat4.m[3][3] = (float)Math.Cos(fAngleRad);
            return mat4;
        }

        private Mat4x4 MatrixMakeRotYW(float fAngleRad)
        {
            Mat4x4 mat4 = new Mat4x4();
            mat4.m[0][0] = 1.0f;
            mat4.m[1][1] = (float)Math.Cos(fAngleRad);
            mat4.m[1][3] = (float)Math.Sin(fAngleRad);
            mat4.m[2][2] = 1.0f;
            mat4.m[3][1] = -(float)Math.Sin(fAngleRad);
            mat4.m[3][3] = (float)Math.Cos(fAngleRad);
            return mat4;
        }

        private Mat4x4 MatrixMakeRotZW(float fAngleRad)
        {
            Mat4x4 mat4 = new Mat4x4();
            mat4.m[0][0] = 1.0f;
            mat4.m[1][1] = 1.0f;
            mat4.m[2][2] = (float)Math.Cos(fAngleRad);
            mat4.m[2][3] = (float)Math.Sin(fAngleRad);
            mat4.m[3][2] = -(float)Math.Sin(fAngleRad);
            mat4.m[3][3] = (float)Math.Cos(fAngleRad);
            return mat4;
        }

        private Mat4x4 MatrixMakeTranslation(Vec3D v)
        {
            Mat4x4 mat4 = MatrixMakeIdentity();
            mat4.m[3][0] = v.x;
            mat4.m[3][1] = v.y;
            mat4.m[3][2] = v.z;
            mat4.m[3][3] = v.w;
            return mat4;
        }

        private Mat4x4 MatrixMakeScalation(Vec3D s)
        {
            Mat4x4 mat4 = new Mat4x4();
            mat4.m[0][0] = s.x;
            mat4.m[1][1] = s.y;
            mat4.m[2][2] = s.z;
            mat4.m[3][3] = s.w;
            return mat4;
        }

        private Mat4x4 MatrixMakeProjection(float fFovDegrees, float fAspectRatio, float fNear, float fFar)
        {
            float fFovRad = 1.0f / (float)Math.Tan(fFovDegrees * 0.5f / 180.0f * Math.PI);
            Mat4x4 mat4 = new Mat4x4();
            mat4.m[0][0] = fAspectRatio * fFovRad;
            mat4.m[1][1] = fFovRad;
            mat4.m[2][2] = fFar / (fFar - fNear);
            mat4.m[3][2] = (-fFar * fNear) / (fFar - fNear);
            mat4.m[2][3] = 1.0f;
            return mat4;
        }

        private Mat4x4 MatrixPointAt(Vec3D pos, Vec3D target, Vec3D up)
        {
            Vec3D newForward = target - pos;
            newForward = newForward.Normalize();

            Vec3D a = newForward * Vec3D.Dot(up, newForward);
            Vec3D newUp = up - a;
            newUp = newUp.Normalize();

            Vec3D newRight = Vec3D.Cross(newUp, newForward);

            Mat4x4 m = new Mat4x4();
            m.m[0][0] = newRight.x;    m.m[0][1] = newRight.y;    m.m[0][2] = newRight.z;
            m.m[1][0] = newUp.x;       m.m[1][1] = newUp.y;       m.m[1][2] = newUp.z;
            m.m[2][0] = newForward.x;  m.m[2][1] = newForward.y;  m.m[2][2] = newForward.z;
            m.m[3][0] = pos.x;         m.m[3][1] = pos.y;         m.m[3][2] = pos.z;

            return m;
        }

        private Mat4x4 MatrixQuickInverse(Mat4x4 m)
        {
            Mat4x4 mat4 = new Mat4x4();
            mat4.m[0][0] = m.m[0][0]; mat4.m[0][1] = m.m[1][0]; mat4.m[0][2] = m.m[2][0];
            mat4.m[1][0] = m.m[0][1]; mat4.m[1][1] = m.m[1][1]; mat4.m[1][2] = m.m[2][1];
            mat4.m[2][0] = m.m[0][2]; mat4.m[2][1] = m.m[1][2]; mat4.m[2][2] = m.m[2][2];
            mat4.m[3][0] = -(m.m[3][0] * mat4.m[0][0] + m.m[3][1] * mat4.m[1][0] + m.m[3][2] * mat4.m[2][0] + m.m[3][3] * mat4.m[3][0]);
            mat4.m[3][1] = -(m.m[3][0] * mat4.m[0][1] + m.m[3][1] * mat4.m[1][1] + m.m[3][2] * mat4.m[2][1] + m.m[3][3] * mat4.m[3][1]);
            mat4.m[3][2] = -(m.m[3][0] * mat4.m[0][2] + m.m[3][1] * mat4.m[1][2] + m.m[3][2] * mat4.m[2][2] + m.m[3][3] * mat4.m[3][2]);
            mat4.m[3][3] = 1.0f;
            return mat4;
        }

        private Frame FrameMakeTesseract()
        {
            return new Frame()
            {
                p = new List<Vec3D>()
                {
                    // front inside
                    new Vec3D(-1, -1, -1,  1),
                    new Vec3D( 1, -1, -1,  1),
                    new Vec3D(-1,  1, -1,  1),
                    new Vec3D( 1,  1, -1,  1),
                                          
                    new Vec3D(-1, -1, -1,  1),
                    new Vec3D(-1,  1, -1,  1),
                    new Vec3D( 1, -1, -1,  1),
                    new Vec3D( 1,  1, -1,  1),
                                          
                    // back inside        
                    new Vec3D(-1, -1,  1,  1),
                    new Vec3D( 1, -1,  1,  1),
                    new Vec3D(-1,  1,  1,  1),
                    new Vec3D( 1,  1,  1,  1),
                                          
                    new Vec3D(-1, -1,  1,  1),
                    new Vec3D(-1,  1,  1,  1),
                    new Vec3D( 1, -1,  1,  1),
                    new Vec3D( 1,  1,  1,  1),
                                          
                    //connect inside      
                    new Vec3D(-1, -1, -1,  1),
                    new Vec3D(-1, -1,  1,  1),
                    new Vec3D(-1,  1, -1,  1),
                    new Vec3D(-1,  1,  1,  1),
                    new Vec3D( 1, -1, -1,  1),
                    new Vec3D( 1, -1,  1,  1),
                    new Vec3D( 1,  1, -1,  1),
                    new Vec3D( 1,  1,  1,  1),

                    // front outside
                    new Vec3D(-1, -1, -1,  2),
                    new Vec3D( 1, -1, -1,  2),
                    new Vec3D(-1,  1, -1,  2),
                    new Vec3D( 1,  1, -1,  2),
                                           
                    new Vec3D(-1, -1, -1,  2),
                    new Vec3D(-1,  1, -1,  2),
                    new Vec3D( 1, -1, -1,  2),
                    new Vec3D( 1,  1, -1,  2),
                                           
                    // back outside        
                    new Vec3D(-1, -1,  1,  2),
                    new Vec3D( 1, -1,  1,  2),
                    new Vec3D(-1,  1,  1,  2),
                    new Vec3D( 1,  1,  1,  2),
                                           
                    new Vec3D(-1, -1,  1,  2),
                    new Vec3D(-1,  1,  1,  2),
                    new Vec3D( 1, -1,  1,  2),
                    new Vec3D( 1,  1,  1,  2),
                                           
                    // connect outside
                    new Vec3D(-1, -1, -1,  2),
                    new Vec3D(-1, -1,  1,  2),
                    new Vec3D(-1,  1, -1,  2),
                    new Vec3D(-1,  1,  1,  2),
                    new Vec3D( 1, -1, -1,  2),
                    new Vec3D( 1, -1,  1,  2),
                    new Vec3D( 1,  1, -1,  2),
                    new Vec3D( 1,  1,  1,  2),

                    // connect inside outside
                    new Vec3D(-1, -1, -1,  2),
                    new Vec3D(-1, -1, -1,  1),
                    new Vec3D(-1, -1,  1,  2),
                    new Vec3D(-1, -1,  1,  1),
                    new Vec3D(-1,  1, -1,  2),
                    new Vec3D(-1,  1, -1,  1),
                    new Vec3D(-1,  1,  1,  2),
                    new Vec3D(-1,  1,  1,  1),
                    new Vec3D( 1, -1, -1,  2),
                    new Vec3D( 1, -1, -1,  1),
                    new Vec3D( 1, -1,  1,  2),
                    new Vec3D( 1, -1,  1,  1),
                    new Vec3D( 1,  1, -1,  2),
                    new Vec3D( 1,  1, -1,  1),
                    new Vec3D( 1,  1,  1,  2),
                    new Vec3D( 1,  1,  1,  1)
                }
            };
        }

        private Mesh MeshMakeTesseract()
        {
            return new Mesh()
            {
                tris = new List<Tria>()
                {
                    // inside front
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1, -1,  1), new Vec3D( 1,  1, -1,  1), new Vec3D( 1, -1, -1,  1) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1, -1,  1), new Vec3D(-1,  1, -1,  1), new Vec3D( 1,  1, -1,  1) } },
                    // inside back
                    new Tria(){ p = new Vec3D[] { new Vec3D( 1, -1,  1,  1), new Vec3D(-1,  1,  1,  1), new Vec3D(-1, -1,  1,  1) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D( 1, -1,  1,  1), new Vec3D( 1,  1,  1,  1), new Vec3D(-1,  1,  1,  1) } },
                    // inside top
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1,  1, -1,  1), new Vec3D( 1,  1,  1,  1), new Vec3D( 1,  1, -1,  1) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1,  1, -1,  1), new Vec3D(-1,  1,  1,  1), new Vec3D( 1,  1,  1,  1) } },
                    // inside bottom
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1,  1,  1), new Vec3D( 1, -1, -1,  1), new Vec3D( 1, -1,  1,  1) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1,  1,  1), new Vec3D(-1, -1, -1,  1), new Vec3D( 1, -1, -1,  1) } },
                    // inside left
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1,  1,  1), new Vec3D(-1,  1, -1,  1), new Vec3D(-1, -1, -1,  1) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1,  1,  1), new Vec3D(-1,  1,  1,  1), new Vec3D(-1,  1, -1,  1) } },
                    // inside right
                    new Tria(){ p = new Vec3D[] { new Vec3D( 1, -1, -1,  1), new Vec3D( 1,  1,  1,  1), new Vec3D( 1, -1,  1,  1) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D( 1, -1, -1,  1), new Vec3D( 1,  1, -1,  1), new Vec3D( 1,  1,  1,  1) } },

                    // outside front
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1, -1,  2), new Vec3D( 1,  1, -1,  2), new Vec3D( 1, -1, -1,  2) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1, -1,  2), new Vec3D(-1,  1, -1,  2), new Vec3D( 1,  1, -1,  2) } },
                    // ouside back
                    new Tria(){ p = new Vec3D[] { new Vec3D( 1, -1,  1,  2), new Vec3D(-1,  1,  1,  2), new Vec3D(-1, -1,  1,  2) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D( 1, -1,  1,  2), new Vec3D( 1,  1,  1,  2), new Vec3D(-1,  1,  1,  2) } },
                    // outside top
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1,  1, -1,  2), new Vec3D( 1,  1,  1,  2), new Vec3D( 1,  1, -1,  2) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1,  1, -1,  2), new Vec3D(-1,  1,  1,  2), new Vec3D( 1,  1,  1,  2) } },
                    // outside bottom
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1,  1,  2), new Vec3D( 1, -1, -1,  2), new Vec3D( 1, -1,  1,  2) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1,  1,  2), new Vec3D(-1, -1, -1,  2), new Vec3D( 1, -1, -1,  2) } },
                    // outside left
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1,  1,  2), new Vec3D(-1,  1, -1,  2), new Vec3D(-1, -1, -1,  2) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1,  1,  2), new Vec3D(-1,  1,  1,  2), new Vec3D(-1,  1, -1,  2) } },
                    // outside right
                    new Tria(){ p = new Vec3D[] { new Vec3D( 1, -1, -1,  2), new Vec3D( 1,  1,  1,  2), new Vec3D( 1, -1,  1,  2) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D( 1, -1, -1,  2), new Vec3D( 1,  1, -1,  2), new Vec3D( 1,  1,  1,  2) } },

                    // front connections
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1, -1,  1), new Vec3D( 1,  1, -1,  1), new Vec3D( 1, -1, -1,  1) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1, -1,  1), new Vec3D( 1,  1, -1,  1), new Vec3D( 1, -1, -1,  1) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1, -1,  1), new Vec3D( 1,  1, -1,  1), new Vec3D( 1, -1, -1,  1) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1, -1,  1), new Vec3D( 1,  1, -1,  1), new Vec3D( 1, -1, -1,  1) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1, -1,  1), new Vec3D( 1,  1, -1,  1), new Vec3D( 1, -1, -1,  1) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1, -1,  1), new Vec3D( 1,  1, -1,  1), new Vec3D( 1, -1, -1,  1) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1, -1,  1), new Vec3D( 1,  1, -1,  1), new Vec3D( 1, -1, -1,  1) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1, -1,  1), new Vec3D( 1,  1, -1,  1), new Vec3D( 1, -1, -1,  1) } },
                    // back connections
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1, -1,  1), new Vec3D( 1,  1, -1,  1), new Vec3D( 1, -1, -1,  1) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1, -1,  1), new Vec3D( 1,  1, -1,  1), new Vec3D( 1, -1, -1,  1) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1, -1,  1), new Vec3D( 1,  1, -1,  1), new Vec3D( 1, -1, -1,  1) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1, -1,  1), new Vec3D( 1,  1, -1,  1), new Vec3D( 1, -1, -1,  1) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1, -1,  1), new Vec3D( 1,  1, -1,  1), new Vec3D( 1, -1, -1,  1) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1, -1,  1), new Vec3D( 1,  1, -1,  1), new Vec3D( 1, -1, -1,  1) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1, -1,  1), new Vec3D( 1,  1, -1,  1), new Vec3D( 1, -1, -1,  1) } },
                    new Tria(){ p = new Vec3D[] { new Vec3D(-1, -1, -1,  1), new Vec3D( 1,  1, -1,  1), new Vec3D( 1, -1, -1,  1) } },
                }
            };
        }

        private Mesh MeshMakeAxis(float scale)
        {
            return new Mesh
            {
                tris = new List<Tria>
                {
                    // x-axis red
                    new Tria() { p = new Vec3D[] { new Vec3D(0.04f, -0.02f, -0.02f, Color.Red), new Vec3D(scale, 0.02f, -0.02f, Color.Red), new Vec3D(scale, -0.02f, -0.02f, Color.Red) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(0.04f, -0.02f, -0.02f, Color.Red), new Vec3D(0.04f, 0.02f, -0.02f, Color.Red), new Vec3D(scale, 0.02f, -0.02f, Color.Red) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(0.04f, 0.02f, -0.02f, Color.Red), new Vec3D(scale, 0.02f, 0.02f, Color.Red), new Vec3D(scale, 0.02f, -0.02f, Color.Red) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(0.04f, 0.02f, -0.02f, Color.Red), new Vec3D(0.04f, 0.02f, 0.02f, Color.Red), new Vec3D(scale, 0.02f, 0.02f, Color.Red) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(scale, -0.02f, 0.02f, Color.Red), new Vec3D(0.04f, 0.02f, 0.02f, Color.Red), new Vec3D(0.04f, -0.02f, 0.02f, Color.Red) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(scale, -0.02f, 0.02f, Color.Red), new Vec3D(scale, 0.02f, 0.02f, Color.Red), new Vec3D(0.04f, 0.02f, 0.02f, Color.Red) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(0.04f, -0.02f, 0.02f, Color.Red), new Vec3D(scale, -0.02f, -0.02f, Color.Red), new Vec3D(scale, -0.02f, 0.02f, Color.Red) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(0.04f, -0.02f, 0.02f, Color.Red), new Vec3D(0.04f, -0.02f, -0.02f, Color.Red), new Vec3D(scale, -0.02f, -0.02f, Color.Red) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(scale, -0.02f, -0.02f, Color.Red), new Vec3D(scale, 0.02f, 0.02f, Color.Red), new Vec3D(scale, -0.02f, 0.02f, Color.Red) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(scale, -0.02f, -0.02f, Color.Red), new Vec3D(scale, 0.02f, -0.02f, Color.Red), new Vec3D(scale, 0.02f, 0.02f, Color.Red) } },
                    
                    // y-axis blue
                    new Tria() { p = new Vec3D[] { new Vec3D(-0.02f, 0.04f, -0.02f, Color.Blue), new Vec3D(0.02f, scale, -0.02f, Color.Blue), new Vec3D(0.02f, 0.04f, -0.02f, Color.Blue) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(-0.02f, 0.04f, -0.02f, Color.Blue), new Vec3D(-0.02f, scale, -0.02f, Color.Blue), new Vec3D(0.02f, scale, -0.02f, Color.Blue) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(-0.02f, 0.04f, 0.02f, Color.Blue), new Vec3D(-0.02f, scale, -0.02f, Color.Blue), new Vec3D(-0.02f, 0.04f, -0.02f, Color.Blue) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(-0.02f, 0.04f, 0.02f, Color.Blue), new Vec3D(-0.02f, scale, 0.02f, Color.Blue), new Vec3D(-0.02f, scale, -0.02f, Color.Blue) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(0.02f, 0.04f, 0.02f, Color.Blue), new Vec3D(-0.02f, scale, 0.02f, Color.Blue), new Vec3D(-0.02f, 0.04f, 0.02f, Color.Blue) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(0.02f, 0.04f, 0.02f, Color.Blue), new Vec3D(0.02f, scale, 0.02f, Color.Blue), new Vec3D(-0.02f, scale, 0.02f, Color.Blue) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(0.02f, 0.04f, -0.02f, Color.Blue), new Vec3D(0.02f, scale, 0.02f, Color.Blue), new Vec3D(0.02f, 0.04f, 0.02f, Color.Blue) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(0.02f, 0.04f, -0.02f, Color.Blue), new Vec3D(0.02f, scale, -0.02f, Color.Blue), new Vec3D(0.02f, scale, 0.02f, Color.Blue) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(-0.02f, scale, -0.02f, Color.Blue), new Vec3D(0.02f, scale, 0.02f, Color.Blue), new Vec3D(0.02f, scale, -0.02f, Color.Blue) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(-0.02f, scale, -0.02f, Color.Blue), new Vec3D(-0.02f, scale, 0.02f, Color.Blue), new Vec3D(0.02f, scale, 0.02f, Color.Blue) } },

                    // z-axis green
                    new Tria() { p = new Vec3D[] { new Vec3D(-0.02f, 0.02f, 0.04f, Color.Green), new Vec3D(0.02f, 0.02f, scale, Color.Green), new Vec3D(0.02f, 0.02f, 0.04f, Color.Green) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(-0.02f, 0.02f, 0.04f, Color.Green), new Vec3D(-0.02f, 0.02f, scale, Color.Green), new Vec3D(0.02f, 0.02f, scale, Color.Green) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(-0.02f, -0.02f, 0.04f, Color.Green), new Vec3D(-0.02f, 0.02f, scale, Color.Green), new Vec3D(-0.02f, 0.02f, 0.04f, Color.Green) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(-0.02f, -0.02f, 0.04f, Color.Green), new Vec3D(-0.02f, -0.02f, scale, Color.Green), new Vec3D(-0.02f, 0.02f, scale, Color.Green) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(0.02f, -0.02f, 0.04f, Color.Green), new Vec3D(-0.02f, -0.02f, scale, Color.Green), new Vec3D(-0.02f, -0.02f, 0.04f, Color.Green) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(0.02f, -0.02f, 0.04f, Color.Green), new Vec3D(0.02f, -0.02f, scale, Color.Green), new Vec3D(-0.02f, -0.02f, scale, Color.Green) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(0.02f, 0.02f, 0.04f, Color.Green), new Vec3D(0.02f, -0.02f, scale, Color.Green), new Vec3D(0.02f, -0.02f, 0.04f, Color.Green) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(0.02f, 0.02f, 0.04f, Color.Green), new Vec3D(0.02f, 0.02f, scale, Color.Green), new Vec3D(0.02f, -0.02f, scale, Color.Green) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(0.02f, -0.02f, scale, Color.Green), new Vec3D(-0.02f, 0.02f, scale, Color.Green), new Vec3D(-0.02f, -0.02f, scale, Color.Green) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(0.02f, -0.02f, scale, Color.Green), new Vec3D(0.02f, 0.02f, scale, Color.Green), new Vec3D(-0.02f, 0.02f, scale, Color.Green) } },

                    // origin white
                    new Tria() { p = new Vec3D[] { new Vec3D(-0.04f, -0.04f, -0.04f, Color4.White), new Vec3D(0.04f, 0.04f, -0.04f, Color4.White), new Vec3D(0.04f, -0.04f, -0.04f, Color4.White) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(-0.04f, -0.04f, -0.04f, Color4.White), new Vec3D(-0.04f, 0.04f, -0.04f, Color4.White), new Vec3D(0.04f, 0.04f, -0.04f, Color4.White) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(-0.04f, 0.04f, -0.04f, Color4.White), new Vec3D(0.04f, 0.04f, 0.04f, Color4.White), new Vec3D(0.04f, 0.04f, -0.04f, Color4.White) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(-0.04f, 0.04f, -0.04f, Color4.White), new Vec3D(-0.04f, 0.04f, 0.04f, Color4.White), new Vec3D(0.04f, 0.04f, 0.04f, Color4.White) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(0.04f, -0.04f, 0.04f, Color4.White), new Vec3D(-0.04f, 0.04f, 0.04f, Color4.White), new Vec3D(-0.04f, -0.04f, 0.04f, Color4.White) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(0.04f, -0.04f, 0.04f, Color4.White), new Vec3D(0.04f, 0.04f, 0.04f, Color4.White), new Vec3D(-0.04f, 0.04f, 0.04f, Color4.White) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(-0.04f, -0.04f, 0.04f, Color4.White), new Vec3D(0.04f, -0.04f, -0.04f, Color4.White), new Vec3D(0.04f, -0.04f, 0.04f, Color4.White) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(-0.04f, -0.04f, 0.04f, Color4.White), new Vec3D(-0.04f, -0.04f, -0.04f, Color4.White), new Vec3D(0.04f, -0.04f, -0.04f, Color4.White) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(-0.04f, -0.04f, 0.04f, Color4.White), new Vec3D(-0.04f, 0.04f, -0.04f, Color4.White), new Vec3D(-0.04f, -0.04f, -0.04f, Color4.White) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(-0.04f, -0.04f, 0.04f, Color4.White), new Vec3D(-0.04f, 0.04f, 0.04f, Color4.White), new Vec3D(-0.04f, 0.04f, -0.04f, Color4.White) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(0.04f, -0.04f, -0.04f, Color4.White), new Vec3D(0.04f, 0.04f, 0.04f, Color4.White), new Vec3D(0.04f, -0.04f, 0.04f, Color4.White) } },
                    new Tria() { p = new Vec3D[] { new Vec3D(0.04f, -0.04f, -0.04f, Color4.White), new Vec3D(0.04f, 0.04f, -0.04f, Color4.White), new Vec3D(0.04f, 0.04f, 0.04f, Color4.White) } },
                }
            };
        }

        private void RenderLight(Mesh mesh, Mat4x4 lightWorld, Mat4x4 matView, Mat4x4 lightScale)
        {
            for (int i = 0; i < mesh.tris.Count; i++)
            {
                Tria triProjected = new Tria(), triTransformed = new Tria(), triViewed = new Tria();

                for (int j = 0; j < 3; j++)
                {
                    triTransformed.p[j] = lightScale * mesh.tris[i].p[j];
                    triTransformed.p[j] = lightWorld * triTransformed.p[j];
                }

                Vec3D line1 = triTransformed.p[1] - triTransformed.p[0],
                      line2 = triTransformed.p[2] - triTransformed.p[0],
                      normal = Vec3D.Cross(line1, line2);
                normal = normal.Normalize();

                Vec3D vCameraRay = triTransformed.p[0] - vCamera;

                if (Vec3D.Dot(normal, vCameraRay) < 0.0f)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        triViewed.p[j] = matView * triTransformed.p[j];
                        triViewed.p[j].color = mesh.tris[i].p[j].color;
                        triViewed.p[j].lumination = 1;
                    }

                    Tria[] clipped = new Tria[2];
                    int nClippedTriangles = TriangleClipAgainstPlane(new Vec3D(0.0f, 0.0f, ForwardClip), new Vec3D(0.0f, 0.0f, 1.0f), ref triViewed, out clipped[0], out clipped[1]);

                    for (int j = 0; j < nClippedTriangles; j++)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            triProjected.p[k] = matProj * clipped[j].p[k];
                            triProjected.p[k] /= triProjected.p[k].w;
                            triProjected.p[k].color = clipped[j].p[k].color;
                            triProjected.p[k].lumination = clipped[j].p[k].lumination;
                        }

                        Tria[] edged = new Tria[2];
                        List<Tria> listTriangles = new List<Tria>
                        {
                            triProjected
                        };
                        int nNewTriangles = 1;

                        for (int p = 0; p < 4; p++)
                        {
                            int nTrisToAdd = 0;
                            while (nNewTriangles > 0)
                            {
                                Tria test = listTriangles[0];
                                listTriangles = PopFront(listTriangles);
                                nNewTriangles--;

                                switch (p)
                                {
                                    case 0:
                                        nTrisToAdd = TriangleClipAgainstPlane(new Vec3D(0.0f, -1.0f, 0.0f), new Vec3D(0.0f, 1.0f, 0.0f), ref test, out edged[0], out edged[1]);
                                        break;
                                    case 1:
                                        nTrisToAdd = TriangleClipAgainstPlane(new Vec3D(0.0f, 1.0f, 0.0f), new Vec3D(0.0f, -1.0f, 0.0f), ref test, out edged[0], out edged[1]);
                                        break;
                                    case 2:
                                        nTrisToAdd = TriangleClipAgainstPlane(new Vec3D(-1.0f, 0.0f, 0.0f), new Vec3D(1.0f, 0.0f, 0.0f), ref test, out edged[0], out edged[1]);
                                        break;
                                    case 3:
                                        nTrisToAdd = TriangleClipAgainstPlane(new Vec3D(1.0f, 0.0f, 0.0f), new Vec3D(-1.0f, 0.0f, 0.0f), ref test, out edged[0], out edged[1]);
                                        break;
                                }

                                for (int w = 0; w < nTrisToAdd; w++)
                                {
                                    edged[w].p = TestReverse(edged[w].p);
                                    listTriangles.Add(edged[w]);
                                    trisToRaster.Add(edged[w]);
                                }
                            }
                            nNewTriangles = listTriangles.Count;
                        }
                    }
                }
            }
        }

        private void Render3DObject(Mesh mesh, Mat4x4 world, Mat4x4 view, Mat4x4 trans, Mat4x4 rot, Mat4x4 scale)
        {
            for (int i = 0; i < mesh.tris.Count; i++)
            {
                Tria triProjected = new Tria(), triTransformed = new Tria(), triViewed = new Tria();

                for (int j = 0; j < 3; j++)
                {
                    triTransformed.p[j] = scale * mesh.tris[i].p[j];
                    triTransformed.p[j] = rot * triTransformed.p[j];
                    triTransformed.p[j] = world * triTransformed.p[j];
                }

                Vec3D line1 = triTransformed.p[1] - triTransformed.p[0],
                      line2 = triTransformed.p[2] - triTransformed.p[0],
                      normal = Vec3D.Cross(line1, line2);
                normal = normal.Normalize();

                Vec3D vCameraRay = triTransformed.p[0] - vCamera;

                if (Vec3D.Dot(normal, vCameraRay) < 0.0f)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        Vec3D ray = vLight - triTransformed.p[j];
                        float dp = Vec3D.Dot(normal, ray.Normalize());
                        float distanceSquared = ray.LengthSquared();
                        float brightness = Lumens / (float)(4.0f * Math.PI * distanceSquared);
                        float shade = Math.Min(Math.Max(MinBrightness, brightness * dp), 1.0f);

                        ///////////////////////

                        triViewed.p[j] = view * triTransformed.p[j];
                        triViewed.p[j].color = mesh.tris[i].p[j].color;
                        triViewed.p[j].lumination = shade;
                    }

                    Tria[] clipped = new Tria[2];
                    int nClippedTriangles = TriangleClipAgainstPlane(new Vec3D(0.0f, 0.0f, ForwardClip), new Vec3D(0.0f, 0.0f, 1.0f), ref triViewed, out clipped[0], out clipped[1]);

                    for (int j = 0; j < nClippedTriangles; j++)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            triProjected.p[k] = matProj * clipped[j].p[k];
                            triProjected.p[k] /= triProjected.p[k].w;
                            triProjected.p[k].color = clipped[j].p[k].color;
                            triProjected.p[k].lumination = clipped[j].p[k].lumination;
                        }

                        Tria[] edged = new Tria[2];
                        List<Tria> listTriangles = new List<Tria>
                        {
                            triProjected
                        };
                        int nNewTriangles = 1;

                        for (int p = 0; p < 4; p++)
                        {
                            int nTrisToAdd = 0;
                            while (nNewTriangles > 0)
                            {
                                Tria test = listTriangles[0];
                                listTriangles = PopFront(listTriangles);
                                nNewTriangles--;

                                switch (p)
                                {
                                    case 0:
                                        nTrisToAdd = TriangleClipAgainstPlane(new Vec3D(0.0f, -1.0f, 0.0f), new Vec3D(0.0f, 1.0f, 0.0f), ref test, out edged[0], out edged[1]);
                                        break;
                                    case 1:
                                        nTrisToAdd = TriangleClipAgainstPlane(new Vec3D(0.0f, 1.0f, 0.0f), new Vec3D(0.0f, -1.0f, 0.0f), ref test, out edged[0], out edged[1]);
                                        break;
                                    case 2:
                                        nTrisToAdd = TriangleClipAgainstPlane(new Vec3D(-1.0f, 0.0f, 0.0f), new Vec3D(1.0f, 0.0f, 0.0f), ref test, out edged[0], out edged[1]);
                                        break;
                                    case 3:
                                        nTrisToAdd = TriangleClipAgainstPlane(new Vec3D(1.0f, 0.0f, 0.0f), new Vec3D(-1.0f, 0.0f, 0.0f), ref test, out edged[0], out edged[1]);
                                        break;
                                }

                                for (int w = 0; w < nTrisToAdd; w++)
                                {
                                    edged[w].p = TestReverse(edged[w].p);
                                    listTriangles.Add(new Tria() { p = new Vec3D[] { new Vec3D(edged[w].p[0]), new Vec3D(edged[w].p[1]), new Vec3D(edged[w].p[2]) } });
                                }
                            }
                            nNewTriangles = listTriangles.Count;
                        }
                        foreach (Tria t in listTriangles)
                            trisToRaster.Add(t);
                    }
                }
            }
        }

        private void Render4DObject(Mesh mesh, Mat4x4 world, Mat4x4 view, Mat4x4 trans, Mat4x4 rot, Mat4x4 scale)
        {
            for (int i = 0; i < mesh.tris.Count; i++)
            {
                Tria triProjected = new Tria(), triTransformed = new Tria(), triViewed = new Tria();

                for (int j = 0; j < 3; j++)
                {
                    triTransformed.p[j] = scale * mesh.tris[i].p[j];
                    triTransformed.p[j] = world * triTransformed.p[j];
                }

                Vec3D line1 = triTransformed.p[1] - triTransformed.p[0],
                      line2 = triTransformed.p[2] - triTransformed.p[0],
                      normal = Vec3D.Cross(line1, line2);
                normal = normal.Normalize();

                Vec3D vCameraRay = triTransformed.p[0] - vCamera;

                if (Vec3D.Dot(normal, vCameraRay) < 0.0f)
                {
                    Vec3D lightPos = vLight;
                    Vec3D[] rays = new Vec3D[3];
                    float[] shades = new float[3];
                    for (int j = 0; j < 3; j++)
                    {
                        rays[j] = lightPos - triTransformed.p[j];
                        float dp = Math.Min(Math.Max(Vec3D.Dot(normal, rays[j]), 0.0f), 1.0f);
                        float distance = rays[j].Length();
                        float brightness = Lumens / (float)(4.0f * Math.PI * distance * distance);
                        shades[j] = Math.Min(Math.Max(MinBrightness, brightness * dp), 1.0f);

                        ///////////////////////

                        triViewed.p[j] = view * triTransformed.p[j];
                        triViewed.p[j].color = mesh.tris[i].p[j].color;
                        triViewed.p[j].lumination = shades[j];
                    }

                    Tria[] clipped = new Tria[2];
                    int nClippedTriangles = TriangleClipAgainstPlane(new Vec3D(0.0f, 0.0f, ForwardClip), new Vec3D(0.0f, 0.0f, 1.0f), ref triViewed, out clipped[0], out clipped[1]);

                    for (int j = 0; j < nClippedTriangles; j++)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            triProjected.p[k] = matProj * clipped[j].p[k];
                            triProjected.p[k] /= triProjected.p[k].w;
                            triProjected.p[k].color = clipped[j].p[k].color;
                            triProjected.p[k].lumination = clipped[j].p[k].lumination;
                        }

                        Tria[] edged = new Tria[2];
                        List<Tria> listTriangles = new List<Tria>
                        {
                            triProjected
                        };
                        int nNewTriangles = 1;

                        for (int p = 0; p < 4; p++)
                        {
                            int nTrisToAdd = 0;
                            while (nNewTriangles > 0)
                            {
                                Tria test = listTriangles[0];
                                listTriangles = PopFront(listTriangles);
                                nNewTriangles--;

                                switch (p)
                                {
                                    case 0:
                                        nTrisToAdd = TriangleClipAgainstPlane(new Vec3D(0.0f, -1.0f, 0.0f), new Vec3D(0.0f, 1.0f, 0.0f), ref test, out edged[0], out edged[1]);
                                        break;
                                    case 1:
                                        nTrisToAdd = TriangleClipAgainstPlane(new Vec3D(0.0f, 1.0f, 0.0f), new Vec3D(0.0f, -1.0f, 0.0f), ref test, out edged[0], out edged[1]);
                                        break;
                                    case 2:
                                        nTrisToAdd = TriangleClipAgainstPlane(new Vec3D(-1.0f, 0.0f, 0.0f), new Vec3D(1.0f, 0.0f, 0.0f), ref test, out edged[0], out edged[1]);
                                        break;
                                    case 3:
                                        nTrisToAdd = TriangleClipAgainstPlane(new Vec3D(1.0f, 0.0f, 0.0f), new Vec3D(-1.0f, 0.0f, 0.0f), ref test, out edged[0], out edged[1]);
                                        break;
                                }

                                for (int w = 0; w < nTrisToAdd; w++)
                                {
                                    edged[w].p = TestReverse(edged[w].p);
                                    listTriangles.Add(new Tria() { p = new Vec3D[] { new Vec3D(edged[w].p[0]), new Vec3D(edged[w].p[1]), new Vec3D(edged[w].p[2]) } });
                                    trisToRaster.Add(new Tria() { p = new Vec3D[] { new Vec3D(edged[w].p[0]), new Vec3D(edged[w].p[1]), new Vec3D(edged[w].p[2]) } });
                                }
                            }
                            nNewTriangles = listTriangles.Count;
                        }
                    }
                }
            }
        }

        private void Render4DWireFrame(Frame frame, Mat4x4 world, Mat4x4 view, Vec3D trans, Mat4x4 rot, Mat4x4 scale)
        {
            for (int i = 0; i < frame.p.Count; i += 2)
            {
                Line4 wProjected = new Line4(), transformed = new Line4(), viewed = new Line4();
                Line3 zProjected = new Line3();

                for (int k = 0; k < 2; k++)
                {
                    transformed.p[k] = frame.p[i + k];
                    //transformed.p[k] = RotYZ * (RotXZ * (RotYW * (RotXW * transformed.p[k])));
                    transformed.p[k] = rot * transformed.p[k];

                    wProjected.p[k] = transformed.p[k] / transformed.p[k].w;

                    viewed.p[k] = view * wProjected.p[k];
                }

                if (viewed.p[0].z < 0.1f && viewed.p[1].z < 0.1f)
                    continue;
                else if (viewed.p[0].z > 0.1f && viewed.p[1].z < 0.1f)
                    viewed.p[1] = VectorIntersectPlan(new Vec3D(0.0f, 0.0f, 0.1f), new Vec3D(0.0f, 0.0f, 1.0f), viewed.p[0], viewed.p[1]);
                else if (viewed.p[0].z < 0.1f && viewed.p[1].z > 0.1f)
                    viewed.p[0] = VectorIntersectPlan(new Vec3D(0.0f, 0.0f, 0.1f), new Vec3D(0.0f, 0.0f, 1.0f), viewed.p[1], viewed.p[0]);
                for (int k = 0; k < 2; k++)
                {
                    zProjected.p[k] = matProj * viewed.p[k];
                    zProjected.p[k] /= zProjected.p[k].w;
                    //zProjected.p[k] = viewed.p[k] / viewed.p[k].z;
                    //zProjected.p[k].x *= (float)Height / Width;
                }
                for (int k = 0; k < 2; k++)
                {
                    if (zProjected.p[k].x > 1.0f)
                        zProjected.p[k] = VectorIntersectPlan(new Vec3D(1.0f, 0.0f, 0.0f), new Vec3D(-1.0f, 0.0f, 0.0f), zProjected.p[(k + 1) % 2], zProjected.p[k]);
                    if (zProjected.p[k].x < -1.0f)
                        zProjected.p[k] = VectorIntersectPlan(new Vec3D(-1.0f, 0.0f, 0.0f), new Vec3D(1.0f, 0.0f, 0.0f), zProjected.p[(k + 1) % 2], zProjected.p[k]);
                    if (zProjected.p[k].y > 1.0f)
                        zProjected.p[k] = VectorIntersectPlan(new Vec3D(0.0f, 1.0f, 0.0f), new Vec3D(0.0f, -1.0f, 0.0f), zProjected.p[(k + 1) % 2], zProjected.p[k]);
                    if (zProjected.p[k].y < -1.0f)
                        zProjected.p[k] = VectorIntersectPlan(new Vec3D(0.0f, -1.0f, 0.0f), new Vec3D(0.0f, 1.0f, 0.0f), zProjected.p[(k + 1) % 2], zProjected.p[k]);
                    vertexp.Add(new VertexPositionColor(new Vector3(zProjected.p[k].x, zProjected.p[k].y, 0.0f), Color4.White));
                }
            }
        }

        private Vec3D VectorIntersectPlan(Vec3D planeP, Vec3D planeN, Vec3D lineStart, Vec3D lineEnd)
        {
            planeN = planeN.Normalize();
            float planeD = Vec3D.Dot(planeN, planeP);
            float ad = Vec3D.Dot(lineStart, planeN);
            float bd = Vec3D.Dot(lineEnd, planeN);
            float t = (planeD - ad) / (bd - ad);
            Vec3D lineStartToEnd = lineEnd - lineStart;
            Vec3D lineToIntersect = lineStartToEnd * t;
            return lineStart + lineToIntersect;
        }

        private Vec3D VectorIntersectPlane(Vec3D planeP, Vec3D planeN, Vec3D lineStart, Vec3D lineEnd)
        {
            planeN = planeN.Normalize();
            float planeD = Vec3D.Dot(planeN, planeP);
            float ad = Vec3D.Dot(lineStart, planeN);
            float bd = Vec3D.Dot(lineEnd, planeN);
            float t = (planeD - ad) / (bd - ad);
            Vec3D lineStartToEnd = lineEnd - lineStart;
            Vec3D lineToIntersect = lineStartToEnd * t;
            Color4 color = Color4.Lerp(lineStart.color, lineEnd.color, t);
            float lumin = lineStart.lumination * (1 - t) + lineEnd.lumination * t;
            Vec3D output = new Vec3D(lineStart + lineToIntersect, color) { lumination = lumin };
            return output;
        }

        private int TriangleClipAgainstPlane(Vec3D planeP, Vec3D planeN, ref Tria tri, out Tria tri1, out Tria tri2)
        {
            planeN = planeN.Normalize();

            float dist(Vec3D p)
            {
                return (Vec3D.Dot(planeN, p) - Vec3D.Dot(planeN, planeP));
            }

            Vec3D[] insidep = new Vec3D[3];  int nInsidePointCount = 0;
            Vec3D[] outsidep = new Vec3D[3]; int nOutsidePointCount = 0;

            float d0 = dist(tri.p[0]);
            float d1 = dist(tri.p[1]);
            float d2 = dist(tri.p[2]);

            if (d0 >= 0) { insidep[nInsidePointCount++] = tri.p[0]; }
            else { outsidep[nOutsidePointCount++] = tri.p[0]; }
            if (d1 >= 0) { insidep[nInsidePointCount++] = tri.p[1]; }
            else { outsidep[nOutsidePointCount++] = tri.p[1]; }
            if (d2 >= 0) { insidep[nInsidePointCount++] = tri.p[2]; }
            else { outsidep[nOutsidePointCount++] = tri.p[2]; }

            if (nInsidePointCount == 0)
            {
                tri1 = null;
                tri2 = null;
                return 0;
            }
            else if (nInsidePointCount == 3)
            {
                tri1 = tri;
                tri2 = null;
                return 1;
            }
            else if (nInsidePointCount == 1 && nOutsidePointCount == 2)
            {
                tri1 = new Tria();
                tri2 = null;
                tri1.p[0] = new Vec3D(insidep[0], insidep[0].color) { lumination = insidep[0].lumination };
                tri1.p[1] = VectorIntersectPlane(planeP, planeN, insidep[0], outsidep[0]);
                tri1.p[2] = VectorIntersectPlane(planeP, planeN, insidep[0], outsidep[1]);
                if (Clip)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        tri1.p[i].color = Color.Blue;
                        tri1.p[i].lumination = 1;
                    }
                }
                return 1;
            }
            else if (nInsidePointCount == 2 && nOutsidePointCount == 1)
            {
                tri1 = new Tria();
                tri2 = new Tria();
                tri1.p[0] = new Vec3D(insidep[0], insidep[0].color) { lumination = insidep[0].lumination };
                tri1.p[1] = new Vec3D(insidep[1], insidep[1].color) { lumination = insidep[1].lumination };
                tri1.p[2] = VectorIntersectPlane(planeP, planeN, insidep[1], outsidep[0]);
                tri2.p[0] = new Vec3D(insidep[0], insidep[0].color) { lumination = insidep[0].lumination };
                tri2.p[1] = VectorIntersectPlane(planeP, planeN, insidep[1], outsidep[0]);
                tri2.p[2] = VectorIntersectPlane(planeP, planeN, insidep[0], outsidep[0]);
                if (Clip)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        tri1.p[i].color = Color.Green;
                        tri2.p[i].color = Color.Red;
                        tri1.p[i].lumination = tri2.p[i].lumination = 1;
                    }
                }
                return 2;
            }
            else { tri1 = null; tri2 = null; return 0; }
        }

        ////////////////////////////////////
        
        public class Vec4D
        {
            public float x, y, z, w;

            public Vec4D()
            {
                x = y = z = w = 0;
            }

            public Vec4D(float x, float y, float z, float w)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                this.w = w;
            }

            public static Vec4D operator +(Vec4D a, Vec4D b)
            {
                return new Vec4D(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
            }

            public static Vec4D operator -(Vec4D a, Vec4D b)
            {
                return new Vec4D(a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w);
            }

            public static Vec4D operator *(Vec4D v, float k)
            {
                return new Vec4D(v.x * k, v.y * k, v.z * k, v.w * k);
            }

            public static Vec4D operator *(Mat4x4 m, Vec4D i)
            {
                return new Vec4D
                    (
                    i.x * m.m[0][0] + i.y * m.m[1][0] + i.z * m.m[2][0] + i.w * m.m[3][0],
                    i.x * m.m[0][1] + i.y * m.m[1][1] + i.z * m.m[2][1] + i.w * m.m[3][1],
                    i.x * m.m[0][2] + i.y * m.m[1][2] + i.z * m.m[2][2] + i.w * m.m[3][2],
                    i.x * m.m[0][3] + i.y * m.m[1][3] + i.z * m.m[2][3] + i.w * m.m[3][3]
                    );
            }

            public static Vec4D operator /(Vec4D v, float k)
            {
                return new Vec4D(v.x / k, v.y / k, v.z / k, v.w / k);
            }

            public static implicit operator Vec3D(Vec4D v)
            {
                return new Vec3D(v.x, v.y, v.z, v.w);
            }
        }

        public class Line4
        {
            public Vec4D[] p = new Vec4D[2];

            public Line4() { }
        }

        public class Vec3D
        {
            public float x, y, z, w;
            public Color4 color;
            public float lumination;

            public Vec3D()
            {
                x = y = z = 0.0f; w = 1.0f;
                color = Color4.White;
                lumination = 1.0f;
            }

            public Vec3D(Vec3D v, Color4 color)
            {
                x = v.x;
                y = v.y;
                z = v.z;
                w = 1.0f;
                this.color = color;
                lumination = 1.0f;
            }

            public Vec3D(float x, float y, float z, float w = 1)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                this.w = w;
                color = Color4.White;
                lumination = 1.0f;
            }

            public Vec3D(float x, float y, float z, Color4 color)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                w = 1.0f;
                this.color = color;
                lumination = 1.0f;
            }

            public Vec3D(float x, float y, float z, Color4 color, float lumination)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                w = 1.0f;
                this.color = color;
                this.lumination = lumination;
            }

            public Vec3D(Vec3D v)
            {
                x = v.x;
                y = v.y;
                z = v.z;
                w = v.w;
                color = v.color;
                lumination = v.lumination;
            }

            public float Length()
            {
                return (float)Math.Sqrt(x * x + y * y + z * z);
            }

            public float LengthSquared()
            {
                return x * x + y * y + z * z;
            }

            public Vec3D Normalize()
            {
                return this / Length();
            }

            public static float Dot(Vec3D a, Vec3D b)
            {
                return (a.x * b.x + a.y * b.y + a.z * b.z);
            }

            public static Vec3D Cross(Vec3D a, Vec3D b)
            {
                return new Vec3D
                    (
                    a.y * b.z - a.z * b.y,
                    a.z * b.x - a.x * b.z,
                    a.x * b.y - a.y * b.x
                    );
            }

            public static Vec3D operator +(Vec3D a, Vec3D b)
            {
                return new Vec3D(a.x + b.x, a.y + b.y, a.z + b.z);
            }

            public static Vec3D operator -(Vec3D a, Vec3D b)
            {
                return new Vec3D(a.x - b.x, a.y - b.y, a.z - b.z);
            }

            public static Vec3D operator *(Vec3D a, float k)
            {
                return new Vec3D(a.x * k, a.y * k, a.z * k);
            }

            public static Vec3D operator *(Mat4x4 m, Vec3D i)
            {
                return new Vec3D()
                {
                    x = i.x * m.m[0][0] + i.y * m.m[1][0] + i.z * m.m[2][0] + i.w * m.m[3][0],
                    y = i.x * m.m[0][1] + i.y * m.m[1][1] + i.z * m.m[2][1] + i.w * m.m[3][1],
                    z = i.x * m.m[0][2] + i.y * m.m[1][2] + i.z * m.m[2][2] + i.w * m.m[3][2],
                    w = i.x * m.m[0][3] + i.y * m.m[1][3] + i.z * m.m[2][3] + i.w * m.m[3][3]
                };
            }

            public static Vec3D operator /(Vec3D a, float k)
            {
                return new Vec3D(a.x / k, a.y / k, a.z / k);
            }

            public static implicit operator Vec4D(Vec3D v)
            {
                return new Vec4D(v.x, v.y, v.z, v.w);
            }

            public override string ToString()
            {
                return "(" + x + ", " + y + ", " + z + ")";
            } 
        }

        public class Line3
        {
            public Vec3D[] p = new Vec3D[2];

            public Line3() { }
        }

        public class Tria : IComparable<Tria>
        {
            public Vec3D[] p = new Vec3D[3];

            public Tria() { }

            public Tria(Tria t)
            {
                p[0] = new Vec3D(t.p[0]);
                p[1] = new Vec3D(t.p[1]);
                p[2] = new Vec3D(t.p[2]);
            }

            public Tria(Vec3D[] p)
            {
                this.p = p;
            }

            public int CompareTo(Tria t)
            {
                float z1 = (p[0].z + p[1].z + p[2].z) / 3.0f;
                float z2 = (t.p[0].z + t.p[1].z + t.p[2].z) / 3.0f;
                if (z1 < z2)
                    return 1;
                if (z1 > z2)
                    return -1;
                else
                    return 0;
            }
        }

        public class Mesh
        {
            public List<Tria> tris = new List<Tria>();

            public bool LoadFromObjectFile(string FileName)
            {
                if (!File.Exists(FileName))
                    return false;
                string[] document = File.ReadAllLines(FileName);
                if (document == null)
                    return false;

                List<Vec3D> verts = new List<Vec3D>();

                for (int i = 0; i < document.Length; i++)
                {
                    if (document[i] == "")
                        continue;
                    else if (document[i][0] == 'v')
                    {
                        List<int> values = new List<int>();
                        for (int j = 1; j < document[i].Length; j++)
                        {
                            if (document[i][j] == ' ')
                                values.Add(j + 1);
                            if (values.Count == 4)
                                break;
                        }

                        Vec3D v = new Vec3D
                        {
                            x = float.Parse(document[i].Substring(values[0], values[1] - values[0] - 1)),
                            y = float.Parse(document[i].Substring(values[1], values[2] - values[1] - 1)),
                            z = float.Parse(document[i].Substring(values[2]))
                        };
                        verts.Add(v);
                    }
                    else if (document[i][0] == 'f')
                    {
                        List<int> values = new List<int>();
                        for (int j = 1; j < document[i].Length; j++)
                        {
                            if (document[i][j] == ' ')
                                values.Add(j + 1);
                        }
                        int[] lineNum = new int[3]
                        {
                        int.Parse(document[i].Substring(values[0], values[1] - values[0] - 1)),
                        int.Parse(document[i].Substring(values[1], values[2] - values[1] - 1)),
                        int.Parse(document[i].Substring(values[2]))
                        };
                        tris.Add(new Tria() { p = new Vec3D[] { verts[lineNum[0] - 1], verts[lineNum[1] - 1], verts[lineNum[2] - 1] } });
                    }
                }

                return true;
            }

            public bool LoadFromObjectFile(string FileName, Color4 color)
            {
                if (!File.Exists(FileName))
                    return false;
                string[] document = File.ReadAllLines(FileName);
                if (document == null)
                    return false;

                List<Vec3D> verts = new List<Vec3D>();

                for (int i = 0; i < document.Length; i++)
                {
                    if (document[i] == "")
                        continue;
                    else if (document[i][0] == 'v')
                    {
                        List<int> values = new List<int>();
                        for (int j = 1; j < document[i].Length; j++)
                        {
                            if (document[i][j] == ' ')
                                values.Add(j + 1);
                        }

                        Vec3D v = new Vec3D
                        {
                            x = float.Parse(document[i].Substring(values[0], values[1] - values[0] - 1)),
                            y = float.Parse(document[i].Substring(values[1], values[2] - values[1] - 1)),
                            z = float.Parse(document[i].Substring(values[2]))
                        };
                        verts.Add(v);
                    }
                    else if (document[i][0] == 'f')
                    {
                        List<int> values = new List<int>();
                        for (int j = 1; j < document[i].Length; j++)
                        {
                            if (document[i][j] == ' ')
                                values.Add(j + 1);
                        }
                        int[] lineNum = new int[3]
                        {
                        int.Parse(document[i].Substring(values[0], values[1] - values[0] - 1)),
                        int.Parse(document[i].Substring(values[1], values[2] - values[1] - 1)),
                        int.Parse(document[i].Substring(values[2]))
                        };
                        tris.Add(new Tria() { p = new Vec3D[] { new Vec3D(verts[lineNum[0] - 1], color), new Vec3D(verts[lineNum[1] - 1], color), new Vec3D(verts[lineNum[2] - 1], color) } });
                    }
                }

                return true;
            }

            public bool LoadFromObjectFile(string ObjectPath, string MaterialPath)
            {
                if (!File.Exists(ObjectPath) || !File.Exists(MaterialPath))
                    return false;
                string[] obj = File.ReadAllLines(ObjectPath);
                string[] mat = File.ReadAllLines(MaterialPath);
                if (obj == null || mat == null)
                    return false;

                List<int> values = new List<int>();
                for (int i = mat[3].Length - 1; i > -1; i--)
                {
                    bool test = mat[3][i] == ' ';
                    if (mat[3][i] == ' ')
                        values.Add(i - 1);
                    if (values.Count == 3)
                        break;
                }
                Color objColor = new Color(float.Parse(mat[3].Substring(values[0] + 1)),
                    float.Parse(mat[3].Substring(values[1] + 1, values[0] - values[1] - 1)), 
                    float.Parse(mat[3].Substring(values[2] + 1, values[1] - values[2] - 1)));
                if (Clip) objColor = Color.White;
                List<Vec3D> verts = new List<Vec3D>();

                for (int i = 0; i < obj.Length; i++)
                {
                    if (obj[i] == "")
                        continue;
                    else if (obj[i][0] == 'v')
                    {
                        values = new List<int>();
                        for (int j = 1; j < obj[i].Length; j++)
                        {
                            if (obj[i][j] == ' ')
                                values.Add(j + 1);
                        }

                        Vec3D v = new Vec3D
                        {
                            x = float.Parse(obj[i].Substring(values[0], values[1] - values[0] - 1)),
                            y = float.Parse(obj[i].Substring(values[1], values[2] - values[1] - 1)),
                            z = float.Parse(obj[i].Substring(values[2]))
                        };
                        verts.Add(v);
                    }
                    else if (obj[i][0] == 'f')
                    {
                        values = new List<int>();
                        for (int j = 1; j < obj[i].Length; j++)
                        {
                            if (obj[i][j] == ' ')
                                values.Add(j + 1);
                        }
                        int[] lineNum = new int[3]
                        {
                        int.Parse(obj[i].Substring(values[0], values[1] - values[0] - 1)),
                        int.Parse(obj[i].Substring(values[1], values[2] - values[1] - 1)),
                        int.Parse(obj[i].Substring(values[2]))
                        };
                        tris.Add(new Tria()
                        {
                            p = new Vec3D[]
                            {
                            new Vec3D(verts[lineNum[0] - 1], objColor), new Vec3D(verts[lineNum[1] - 1], objColor), new Vec3D(verts[lineNum[2] - 1], objColor)
                            }
                        });
                    }
                }
                return true;
            }
        }

        public class Frame
        {
            public List<Vec3D> p = new List<Vec3D>();
        }

        public class Mat4x4
        {
            public float[][] m = new float[4][]
                {
                new float[4],
                new float[4],
                new float[4],
                new float[4]
                };

            public static Mat4x4 operator *(Mat4x4 a, Mat4x4 b)
            {
                Mat4x4 mat4 = new Mat4x4();
                for (int i = 0; i < 4; i++)
                    for (int j = 0; j < 4; j++)
                        mat4.m[j][i] = a.m[j][0] * b.m[0][i] + a.m[j][1] * b.m[1][i] + a.m[j][2] * b.m[2][i] + a.m[j][3] * b.m[3][i];
                return mat4;
            }
        }

        private class Chey
        {
            public Key key;
            public bool Down, Up, Held, Raised;

            public Chey(Key key)
            {
                this.key = key;
                Down = Up = Held = false;
                Raised = true;
            }
        }

        private class Button
        {
            // 0 is left
            // 1 is right
            // 
            public bool Down, Up, Held, Raised;

            public Button()
            {
                Down = Up = Held = false;
                Raised = true;
            }
        }
    }
}