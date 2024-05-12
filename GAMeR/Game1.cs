using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using SimpleEngine;

namespace GAMeR
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        public struct Triangle
        {
            public Vector3 Top;
            public Vector3 Left;
            public Vector3 Right;

            public Triangle(Vector3 top, Vector3 left, Vector3 right)
            {
                Top = top;
                Left = left;
                Right = right;
            }
        };

        /* Controls Stuff */
        MouseState previousMouse;
        KeyboardState previousKey;

        /* Camera Stuff */
        float distance = 1f;
        Vector3 initCamPos = Vector3.Forward;
        Vector3 cameraPosition;
        Vector2 camAngle = new Vector2(0f, MathHelper.Pi / 3f);
        Vector3 initTarget = Vector3.Zero;
        Vector2 camTrans = Vector2.Zero;

        /* Shading and Font */
        Effect effect;
        SpriteFont font;

        /* Skybox stuff */
        Skybox skybox;
        float reflectivity = 0.1f;

        /* 3D Models */
        Model[] models;
        int currModel = 2;

        /* Matrices */
        Matrix world;
        Matrix view;
        Matrix projection;

        /* UI Stuff */
        bool showControls = true;
        bool showShaderInfo = true;
        bool toggleWireframe = true;
        bool drawOG = false;

        /* Depth Tag and Displacement */
        List<int> tags;
        int depthTag = 1;
        int depthTagCalc = 0;
        int displaceFunc = 2;

        /* Sine Function Parameters */
        float amplitude = 1;
        float period = 3;
        float phaseShift = 0;
        float verticalShift = 0;

        /* Tesselated Triangles */
        int MaxDepth = 5;
        int[,,] ARPNumIndexes;
        int[,,] ARPIndexOffset;
        VertexPosition[] vertexArr;
        uint[] indexArr;
        VertexBuffer vertexBuffer;
        IndexBuffer indexBuffer;

        /* Model Attributes */
        List<Vector3>[,,] positions;
        List<Vector3>[,,] normals;
        List<ushort>[,,] indexes;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            /* Needed to use shader files */
            _graphics.GraphicsProfile = GraphicsProfile.HiDef;
        }

        protected override void Initialize()
        {
            /* Initialize Camera Position */
            cameraPosition = distance * initCamPos;

            /* Vectors to be used for calculations */
            Vector3 newUp = Vector3.Transform(Vector3.Up,
                Matrix.CreateRotationX(camAngle.Y) *
                Matrix.CreateRotationY(camAngle.X));
            Vector3 newLeft = Vector3.Transform(Vector3.Left,
                Matrix.CreateRotationX(camAngle.Y) *
                Matrix.CreateRotationY(camAngle.X));

            /* Transform Camera Position */
            cameraPosition = Vector3.Transform(distance * initCamPos,
                Matrix.CreateRotationX(camAngle.Y) *
                Matrix.CreateRotationY(camAngle.X) *
                Matrix.CreateTranslation(newLeft * camTrans.X) *
                Matrix.CreateTranslation(newUp * camTrans.Y));

            /* Initialize Matrices */
            world = Matrix.Identity;
            view = Matrix.CreateLookAt(
                cameraPosition,
                initTarget,
                Vector3.Up);
            projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(90),
                GraphicsDevice.Viewport.AspectRatio,
                0.1f, 500);

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            /* Load Stuff */
            font = Content.Load<SpriteFont>("Font");
            models = new Model[]
            {
                Content.Load<Model>("models/box"),
                Content.Load<Model>("models/sphere"),
                Content.Load<Model>("models/Torus"),
                Content.Load<Model>("models/pyramid"),
            };
            effect = Content.Load<Effect>("ARS");

            /* Load Skybox Stuff */
            string[] skyBoxTextures = new string[]
            {
                "skybox/nvlobby_new_posx",
                "skybox/nvlobby_new_negx",
                "skybox/nvlobby_new_posy",
                "skybox/nvlobby_new_negy",
                "skybox/nvlobby_new_posz",
                "skybox/nvlobby_new_negz"
            };
            skybox = new Skybox(skyBoxTextures, Content, _graphics.GraphicsDevice);

            /* Initialization */
            ExtractVertices(); // Get Positions, Normals, and Indexes from all of the models
            PreComputeARP();  // Compute Adaptive Refinement Pattern
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            bool hasMoved = false;
            bool recompute = false;

            /* Mouse Controls */
            //-----------------------------------------------------------------------------------------------------------
            MouseState currentMouse = Mouse.GetState();
            if (currentMouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Pressed)
            {
                camAngle.X += (previousMouse.X - currentMouse.X) / 100.0f;
                camAngle.Y += (previousMouse.Y - currentMouse.Y) / 100.0f;
                hasMoved = true;
            }
            if (currentMouse.MiddleButton == ButtonState.Pressed && previousMouse.MiddleButton == ButtonState.Pressed)
            {
                camTrans.X += (previousMouse.X - currentMouse.X) / 100.0f;
                camTrans.Y -= (previousMouse.Y - currentMouse.Y) / 100.0f;
                hasMoved = true;
            }
            if (currentMouse.RightButton == ButtonState.Pressed && previousMouse.RightButton == ButtonState.Pressed)
            {
                distance -= (previousMouse.Y - currentMouse.Y) / 100.0f;
                hasMoved = true;
            }
            previousMouse = currentMouse;
            //-----------------------------------------------------------------------------------------------------------

            /* Keyboard Controls */
            //--------------------------------------------------------------------------------------------------------------------------------------------------------------------
            KeyboardState currentKey = Keyboard.GetState();
            bool shift = currentKey.IsKeyDown(Keys.LeftShift) || currentKey.IsKeyDown(Keys.RightShift);

            /* Reset Camera, Light & Reflection Stuff */
            if (currentKey.IsKeyDown(Keys.S))
            {
                distance = 1f;
                camTrans = Vector2.Zero;
                camAngle = new Vector2(0f, MathHelper.Pi / 3f);
                hasMoved = true;
                phaseShift = 0f;
                verticalShift = 0f;
            }

            /* Switch Models */
            if (currentKey.IsKeyDown(Keys.D1) || currentKey.IsKeyDown(Keys.NumPad1)) currModel = 0;
            if (currentKey.IsKeyDown(Keys.D2) || currentKey.IsKeyDown(Keys.NumPad2)) currModel = 1;
            if (currentKey.IsKeyDown(Keys.D3) || currentKey.IsKeyDown(Keys.NumPad3)) currModel = 2;
            if (currentKey.IsKeyDown(Keys.D4) || currentKey.IsKeyDown(Keys.NumPad4)) currModel = 3;

            /* Calculate Depth Tag */
            if (currentKey.IsKeyDown(Keys.F1)) depthTagCalc = 0;
            if (currentKey.IsKeyDown(Keys.F2)) depthTagCalc = 1;

            /* Calculate Displacement */
            if (currentKey.IsKeyDown(Keys.F3)) displaceFunc = 0;
            if (currentKey.IsKeyDown(Keys.F4)) displaceFunc = 1;
            if (currentKey.IsKeyDown(Keys.F5)) displaceFunc = 2;

            /* Change Uniform Depth Tag and Maximum Depth */
            if (!shift && currentKey.IsKeyDown(Keys.D) && previousKey.IsKeyUp(Keys.D)) depthTag = MathHelper.Min(depthTag + 1, MaxDepth - 1);
            if ( shift && currentKey.IsKeyDown(Keys.D) && previousKey.IsKeyUp(Keys.D)) depthTag = MathHelper.Max(depthTag - 1, 0);
            if (!shift && currentKey.IsKeyDown(Keys.M) && previousKey.IsKeyUp(Keys.M)) { MaxDepth = MathHelper.Min(MaxDepth + 1, 6); recompute = true; }
            if ( shift && currentKey.IsKeyDown(Keys.M) && previousKey.IsKeyUp(Keys.M)) { MaxDepth = MathHelper.Max(MaxDepth - 1, 1); recompute = true; depthTag = MathHelper.Clamp(depthTag, 0, MaxDepth-1); }

            /* Change Skybox reflectivity */
            if (!shift && currentKey.IsKeyDown(Keys.R)) reflectivity += 0.01f;
            if ( shift && currentKey.IsKeyDown(Keys.R)) reflectivity -= 0.01f;

            /* Change Sine Displacement Parameters */
            if (!shift && currentKey.IsKeyDown(Keys.A)) amplitude = MathHelper.Clamp(amplitude + 0.01f, 0, 5);
            if ( shift && currentKey.IsKeyDown(Keys.A)) amplitude = MathHelper.Clamp(amplitude - 0.01f, 0, 5);
            if (!shift && currentKey.IsKeyDown(Keys.P)) period += 0.01f;
            if ( shift && currentKey.IsKeyDown(Keys.P)) period -= 0.01f;
            if (!shift && currentKey.IsKeyDown(Keys.X)) phaseShift += 0.01f;
            if ( shift && currentKey.IsKeyDown(Keys.X)) phaseShift -= 0.01f;
            if (!shift && currentKey.IsKeyDown(Keys.V)) verticalShift += 0.01f;
            if ( shift && currentKey.IsKeyDown(Keys.V)) verticalShift -= 0.01f;

            //phaseShift += 0.01f;

            /* Toggle Drawing original mesh and wireframe */
            if (currentKey.IsKeyDown(Keys.O) && previousKey.IsKeyUp(Keys.O)) drawOG = !drawOG;
            if (currentKey.IsKeyDown(Keys.W) && previousKey.IsKeyUp(Keys.W)) toggleWireframe = !toggleWireframe;

            /* Show debug info */
            if (currentKey.IsKeyDown(Keys.OemQuestion) && previousKey.IsKeyUp(Keys.OemQuestion)) showControls = !showControls;
            if (currentKey.IsKeyDown(Keys.H) && previousKey.IsKeyUp(Keys.H)) showShaderInfo = !showShaderInfo;
            
            previousKey = currentKey;
            //--------------------------------------------------------------------------------------------------------------------------------------------------------------------

            /* Variable Clamping */
            //-----------------------------------------------------------------------------------
            distance = MathHelper.Clamp(distance, 0.15f, 75f); // Clamp the distance

            camAngle.X = camAngle.X % MathHelper.TwoPi;
            camAngle.Y = MathHelper.Clamp(camAngle.Y, -MathHelper.Pi / 2, MathHelper.Pi / 2);

            phaseShift = MathHelper.Clamp(phaseShift, -period / (2*MathHelper.TwoPi), period / MathHelper.TwoPi);
            //-----------------------------------------------------------------------------------

            /* If the maximum depth has changed, recompute the ARP */
            if (recompute)
            {
                PreComputeARP();
            }

            /* Only Update these when there is movement */
            if (hasMoved)
            {
                /* Vectors to be used for calculations */
                Vector3 newUp = Vector3.Transform(Vector3.Up,
                    Matrix.CreateRotationX(camAngle.Y) *
                    Matrix.CreateRotationY(camAngle.X));
                Vector3 newLeft = Vector3.Transform(Vector3.Left,
                    Matrix.CreateRotationX(camAngle.Y) *
                    Matrix.CreateRotationY(camAngle.X));

                /* Transform Camera Position */
                cameraPosition = Vector3.Transform(distance * initCamPos,
                    Matrix.CreateRotationX(camAngle.Y) *
                    Matrix.CreateRotationY(camAngle.X) *
                    Matrix.CreateTranslation(newLeft * camTrans.X) *
                    Matrix.CreateTranslation(newUp * camTrans.Y));

                /* Transform Camera */
                view = Matrix.CreateLookAt(
                    cameraPosition,
                    Vector3.Transform(initTarget,
                        Matrix.CreateTranslation(newLeft * camTrans.X) *
                        Matrix.CreateTranslation(newUp * camTrans.Y)),
                        newUp);
            }

            /* Compute Tags */
            //------------------------------------------------------------------------------
            int currMesh = 0;
            int currMeshPart = 0;
            tags = new List<int>();
            foreach (ModelMesh mesh in models[currModel].Meshes)
            {
                foreach (ModelMeshPart part in mesh.MeshParts)
                {
                    foreach (Vector3 vertex in positions[currModel, currMesh, currMeshPart])
                    {
                        tags.Add(ComputeRefinementDepth(Vector3.Transform(vertex, mesh.ParentBone.Transform)));
                    }
                    currMeshPart++;
                }
                currMeshPart = 0;
                currMesh++;
            }
            //------------------------------------------------------------------------------
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            /* Needed so that sprite batch doesn't mess with the 3D rendering */
            _graphics.GraphicsDevice.BlendState = BlendState.Opaque;
            _graphics.GraphicsDevice.DepthStencilState = DepthStencilState.Default;

            /* Draw Skybox */
            //------------------------------------------------------
            RasterizerState rasterizerState = new RasterizerState();
            rasterizerState.CullMode = CullMode.CullClockwiseFace;
            GraphicsDevice.RasterizerState = rasterizerState;
            skybox.Draw(view, projection, cameraPosition);
            //------------------------------------------------------

            rasterizerState = new RasterizerState();
            rasterizerState.CullMode = CullMode.None;
            if (toggleWireframe) rasterizerState.FillMode = FillMode.WireFrame;
            else rasterizerState.FillMode = FillMode.Solid;
            GraphicsDevice.RasterizerState = rasterizerState;

            int currMesh = 0;
            int currMeshPart = 0;
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                foreach (ModelMesh mesh in models[currModel].Meshes)
                {
                    foreach (ModelMeshPart part in mesh.MeshParts)
                    {
                        /* Grab Coarse Triangle from Mesh */
                        for (int i = 0; i < indexes[currModel, currMesh, currMeshPart].Count; i+=3)
                        {
                            pass.Apply();

                            effect.Parameters["CameraPosition"].SetValue(cameraPosition);

                            effect.Parameters["World"].SetValue(mesh.ParentBone.Transform);
                            effect.Parameters["View"].SetValue(view);
                            effect.Parameters["Projection"].SetValue(projection);
                            Matrix worldInverseTranspose = Matrix.Transpose(Matrix.Invert(mesh.ParentBone.Transform));
                            effect.Parameters["WorldInverseTranspose"].SetValue(worldInverseTranspose);

                            effect.Parameters["displaceFunc"].SetValue(displaceFunc);

                            effect.Parameters["environmentMap"].SetValue(skybox.skyBoxTexture);
                            effect.Parameters["reflectivity"].SetValue(reflectivity);

                            effect.Parameters["amplitude"].SetValue(amplitude);
                            effect.Parameters["period"].SetValue(period);
                            effect.Parameters["phaseShift"].SetValue(phaseShift);
                            effect.Parameters["verticalShift"].SetValue(verticalShift);

                            ushort index1 = indexes[currModel, currMesh, currMeshPart].ElementAt(i);
                            ushort index2 = indexes[currModel, currMesh, currMeshPart].ElementAt(i+1);
                            ushort index3 = indexes[currModel, currMesh, currMeshPart].ElementAt(i+2);

                            Vector3 vertex1 = positions[currModel, currMesh, currMeshPart].ElementAt(index1);
                            Vector3 vertex2 = positions[currModel, currMesh, currMeshPart].ElementAt(index2);
                            Vector3 vertex3 = positions[currModel, currMesh, currMeshPart].ElementAt(index3);

                            Vector3 normal1 = normals[currModel, currMesh, currMeshPart].ElementAt(index1);
                            Vector3 normal2 = normals[currModel, currMesh, currMeshPart].ElementAt(index2);
                            Vector3 normal3 = normals[currModel, currMesh, currMeshPart].ElementAt(index3);

                            int tag1 = tags[index1];
                            int tag2 = tags[index1];
                            int tag3 = tags[index1];

                            effect.Parameters["p0"].SetValue(vertex1);
                            effect.Parameters["n0"].SetValue(normal1);
                            effect.Parameters["p1"].SetValue(vertex2);
                            effect.Parameters["n1"].SetValue(normal2);
                            effect.Parameters["p2"].SetValue(vertex3);
                            effect.Parameters["n2"].SetValue(normal3);
                            
                            GraphicsDevice.SetVertexBuffer(vertexBuffer);
                            GraphicsDevice.Indices = indexBuffer;

                            GraphicsDevice.DrawIndexedPrimitives(
                                PrimitiveType.TriangleList,
                                0,
                                ARPIndexOffset[tag1, tag2, tag3],
                                ARPNumIndexes[tag1, tag2, tag3] / 3);
                        }
                        currMeshPart++;
                    }
                    currMeshPart = 0;
                    currMesh++;
                }
            }

            /* Draw Original Model */
            if (drawOG) models[currModel].Draw(world, view, projection);


            /* Draw UI */
            //-----------------------------------
            _spriteBatch.Begin();
            if (showControls) DrawControls();
            if (showShaderInfo) DrawShaderInfo();
            _spriteBatch.End();
            //-----------------------------------

            base.Draw(gameTime);
        }

        private void DrawControls()
        {
            int column = 20;
            int row = 1;

            _spriteBatch.DrawString(font, "Controls:", new Vector2(column, 20 * row++), Color.White);
            _spriteBatch.DrawString(font, "Rotate Camera: Mouse Left Drag", new Vector2(column, 20 * row++), Color.White);
            _spriteBatch.DrawString(font, "Change Camera Distance to Center: Mouse Right Drag", new Vector2(column, 20 * row++), Color.White);
            _spriteBatch.DrawString(font, "Translate Camera: Mouse Middle Drag", new Vector2(column, 20 * row++), Color.White);
            _spriteBatch.DrawString(font, "Reset Camera Light & Shader: S Key", new Vector2(column, 20 * row++), Color.White);
            _spriteBatch.DrawString(font, "Increase Uniform Depth Tag: D (+ SHIFT Decrease)", new Vector2(column, 20 * row++), Color.White);
            _spriteBatch.DrawString(font, "Increase Max Depth Tag: M (+ SHIFT Decrease)", new Vector2(column, 20 * row++), Color.White);
            _spriteBatch.DrawString(font, "Reflectivity: R (+ SHIFT Decrease)", new Vector2(column, 20 * row++), Color.White);

            _spriteBatch.DrawString(font, "Sine Displacement", new Vector2(column, 20 * row++), Color.White);
            _spriteBatch.DrawString(font, "     Increase Amplitude: A (+ SHIFT Decrease)", new Vector2(column, 20 * row++), Color.White);
            _spriteBatch.DrawString(font, "     Increase Period: P (+ SHIFT Decrease)", new Vector2(column, 20 * row++), Color.White);
            _spriteBatch.DrawString(font, "     Increase Phase Shift: X (+ SHIFT Decrease)", new Vector2(column, 20 * row++), Color.White);
            _spriteBatch.DrawString(font, "     Increase Vertical Shift: V (+ SHIFT Decrease)", new Vector2(column, 20 * row++), Color.White);

            _spriteBatch.DrawString(font, "Change Model: 1 - 5", new Vector2(column, 20 * row++), Color.White);
            _spriteBatch.DrawString(font, "Change Depth Tag Calculation: F1 - F2", new Vector2(column, 20 * row++), Color.White);
            _spriteBatch.DrawString(font, "Change Displacement Calculation: F3 - F5", new Vector2(column, 20 * row++), Color.White);
            
            _spriteBatch.DrawString(font, "Show Wireframe: W Key", new Vector2(column, 20 * row++), Color.White);
            _spriteBatch.DrawString(font, "Draw Original: O Key", new Vector2(column, 20 * row++), Color.White);
        }

        private void DrawShaderInfo()
        {
            int column = 900;
            int row = 1;

            _spriteBatch.DrawString(font, "Shader Info.:", new Vector2(column, 20 * row++), Color.White);
            _spriteBatch.DrawString(font, "Camera Distance: " + distance.ToString("0.00"), new Vector2(column, 20 * row++), Color.White);
            _spriteBatch.DrawString(font, "Camera Translation: {X: " + camTrans.X.ToString("0.00") + ", Y: " + camTrans.Y.ToString("0.00") + "}", new Vector2(column, 20 * row++), Color.White);
            _spriteBatch.DrawString(font, "Camera Angle: {Yaw: " + camAngle.X.ToString("0.00") + ", Pitch: " + camAngle.Y.ToString("0.00") + "}", new Vector2(column, 20 * row++), Color.White);
            
            _spriteBatch.DrawString(font, "Uniform Depth Tag: " + (depthTag + 1), new Vector2(column, 20 * row++), Color.White);
            _spriteBatch.DrawString(font, "Max Depth Tag: " + MaxDepth, new Vector2(column, 20 * row++), Color.White);
            _spriteBatch.DrawString(font, "Reflectivity: " + reflectivity, new Vector2(column, 20 * row++), Color.White);

            _spriteBatch.DrawString(font, "Sine Displacement", new Vector2(column, 20 * row++), Color.White);
            _spriteBatch.DrawString(font, "     Amplitude: " + amplitude.ToString("0.00"), new Vector2(column, 20 * row++), Color.White);
            _spriteBatch.DrawString(font, "     Period: " + period.ToString("0.00"), new Vector2(column, 20 * row++), Color.White);
            _spriteBatch.DrawString(font, "     Phase Shift: " + phaseShift.ToString("0.00"), new Vector2(column, 20 * row++), Color.White);
            _spriteBatch.DrawString(font, "     Vertical Shift: " + verticalShift.ToString("0.00"), new Vector2(column, 20 * row++), Color.White);

            switch (currModel)
            {
                case 0:
                    _spriteBatch.DrawString(font, "Model: Cube", new Vector2(column, 20 * row++), Color.White);
                    break;
                case 1:
                    _spriteBatch.DrawString(font, "Model: Circle", new Vector2(column, 20 * row++), Color.White);
                    break;
                case 2:
                    _spriteBatch.DrawString(font, "Model: Torus", new Vector2(column, 20 * row++), Color.White);
                    break;
                case 3:
                    _spriteBatch.DrawString(font, "Model: Pyramid", new Vector2(column, 20 * row++), Color.White);
                    break;
                case 4:
                    _spriteBatch.DrawString(font, "Model: Hand", new Vector2(column, 20 * row++), Color.White);
                    break;
            }

            switch (depthTagCalc)
            {
                case 0:
                    _spriteBatch.DrawString(font, "Depth Tag Calculation: Uniform", new Vector2(column, 20 * row++), Color.White);
                    break;
                case 1:
                    _spriteBatch.DrawString(font, "Depth Tag Calculation: Distance from Camera", new Vector2(column, 20 * row++), Color.White);
                    break;
            }

            switch (displaceFunc)
            {
                case 0:
                    _spriteBatch.DrawString(font, "Displacement Function: None", new Vector2(column, 20 * row++), Color.White);
                    break;
                case 1:
                    _spriteBatch.DrawString(font, "Displacement Function: Bezier", new Vector2(column, 20 * row++), Color.White);
                    break;
                case 2:
                    _spriteBatch.DrawString(font, "Displacement Function: Sine", new Vector2(column, 20 * row++), Color.White);
                    break;
            }
        }

        private int ComputeRefinementDepth(Vector3 vertex)
        {
            switch (depthTagCalc)
            {
                case 0: // Uniform Depth Tag
                    return depthTag;
                case 1: // Camera Distance Depth Tag
                    float distance = MathF.Abs(Vector3.Distance(cameraPosition, vertex));
                    if (distance >= MaxDepth) return 0;
                    else return (int)(MathF.Abs(MaxDepth - distance));
                default:
                    return 0;
            }
        }

        private void PreComputeARP()
        {
            // Initial Barycentric Triangle
            Triangle initialTri = new Triangle(new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 1f), new Vector3(1f, 0f, 0f));

            ARPNumIndexes  = new int[MaxDepth, MaxDepth, MaxDepth]; // Gets number of indexes at each ARP
            ARPIndexOffset = new int[MaxDepth, MaxDepth, MaxDepth]; // Gets offset for indexbuffer drawing

            List<Vector3> unstrippedVertices = new List<Vector3>(); // All vertices needed to draw triangles
            List<Vector3> strippedVertices = new List<Vector3>();   // Minimum vertices needed to draw triangles

            List<VertexPosition> vertexList = new List<VertexPosition>(); // Vertex Positions to send to vertex buffer
            List<uint> indexList = new List<uint>(); // Index list to send to index buffer

            /* Compute ARP's */
            for (int i = 0; i < MaxDepth; i++)
            {
                for (int j = 0; j < MaxDepth; j++)
                {
                    for (int k = 0; k < MaxDepth; k++)
                    {
                        ARPIndexOffset[i, j, k] = unstrippedVertices.Count;
                        List<Vector3> currVertices = refineTriangle(initialTri, i, j, k);
                        ARPNumIndexes[i, j, k] = currVertices.Count;
                        unstrippedVertices.AddRange(currVertices);
                    }
                }
            }

            /* Strip ARPs of duplicate vertices */
            uint currIndex = 0;
            for (int i = 0; i < unstrippedVertices.Count; i++)
            {
                int index = FindDuplicateVertex(strippedVertices, unstrippedVertices.ElementAt(i));
                if (index == -1)
                {
                    strippedVertices.Add(unstrippedVertices.ElementAt(i));
                    VertexPosition vertexPosition = new VertexPosition(unstrippedVertices.ElementAt(i));
                    vertexList.Add(vertexPosition);
                    indexList.Add(currIndex);
                    currIndex++;
                }
                else
                {
                    indexList.Add((uint)index);
                }
            }
            
            /* Create vertex and index arrays to put in vertex buffer and index buffer */
            vertexArr = new VertexPosition[vertexList.Count];
            for (int i = 0; i < vertexList.Count; i++)
            {
                vertexArr[i] = vertexList.ElementAt(i);
            }
            indexArr = new uint[indexList.Count];
            for (int i = 0; i < indexList.Count;i++)
            {
                indexArr[i] = indexList[i];
            }

            /* Create vertex buffer and index buffer */
            vertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPosition), vertexArr.Length, BufferUsage.WriteOnly);
            vertexBuffer.SetData(vertexArr);
            indexBuffer = new IndexBuffer(GraphicsDevice, typeof(uint), indexArr.Length, BufferUsage.WriteOnly);
            indexBuffer.SetData(indexArr);
        }
        private List<Vector3> refineTriangle(Triangle triangle, int refineLeft, int refineBottom, int refineRight)
        {
            int minimum1 = MathHelper.Min(refineLeft, refineBottom);
            int minimum2 = MathHelper.Min(minimum1, refineRight);

            List<Triangle> triangles = uniformRefineTriangle(triangle, minimum2, 0);
            if (triangles.Count == 0) triangles.Add(triangle);

            List<Vector3> vertices = new List<Vector3>();
            for (int i = 0; i < triangles.Count; i++)
            {
                vertices.Add(triangles.ElementAt(i).Top);
                vertices.Add(triangles.ElementAt(i).Left);
                vertices.Add(triangles.ElementAt(i).Right);
            }
            
            return vertices;
        }

        private List<Triangle> uniformRefineTriangle(Triangle triangle, int minimumRefine, int currentRefine)
        {
            List<Triangle> triangles = new List<Triangle>();

            Vector3 leftToTop   = triangle.Left  + ((triangle.Top - triangle.Left) / 2);
            Vector3 rightToTop  = triangle.Right + ((triangle.Top - triangle.Right) / 2);
            Vector3 leftToRight = triangle.Left  + ((triangle.Right - triangle.Left) / 2);

            Triangle triangleTop    = new Triangle(triangle.Top, leftToTop, rightToTop);
            Triangle triangleLeft   = new Triangle(leftToTop, triangle.Left, leftToRight);
            Triangle triangleMiddle = new Triangle(leftToRight, rightToTop, leftToTop);
            Triangle triangleRight  = new Triangle(rightToTop, leftToRight, triangle.Right);

            if (currentRefine < minimumRefine)
            {
                triangles.AddRange(uniformRefineTriangle(triangleTop,    minimumRefine, currentRefine + 1));
                triangles.AddRange(uniformRefineTriangle(triangleLeft,   minimumRefine, currentRefine + 1));
                triangles.AddRange(uniformRefineTriangle(triangleMiddle, minimumRefine, currentRefine + 1));
                triangles.AddRange(uniformRefineTriangle(triangleRight,  minimumRefine, currentRefine + 1));
            }
            else
            {
                triangles.Add(triangle);
            }
            
            return triangles;

        }

        private int FindDuplicateVertex(List<Vector3> vertices, Vector3 vertex)
        {
            for (int i = 0; i < vertices.Count; i++)
            {
                if (vertices.ElementAt(i).Equals(vertex)) return i;
            }
            return -1;
        }

        private void ExtractVertices()
        {
            /* Get total  number of models, meshes and mesh parts */
            int maxModels = 0;
            int maxMeshes = 0;
            int maxMeshparts = 0;
            foreach (Model model in models)
            {
                foreach (ModelMesh mesh in model.Meshes)
                {
                    foreach (ModelMeshPart part in mesh.MeshParts)
                    {
                        maxMeshparts++;
                    }
                    maxMeshes++;
                }
                maxModels++;
            }
            
            /* Create list of positions, normals and indexes */
            positions = new List<Vector3>[maxModels, maxMeshes, maxMeshparts];
            normals   = new List<Vector3>[maxModels, maxMeshes, maxMeshparts];
            indexes   = new List<ushort>[maxModels, maxMeshes, maxMeshparts];
            
            /* Extract positions, normals, and indexes from the models */
            int thecurrModel = 0;
            int currMesh = 0;
            int currMeshPart = 0;
            foreach (Model model in models)
            {
                foreach (ModelMesh mesh in model.Meshes)
                {
                    foreach (ModelMeshPart part in mesh.MeshParts)
                    {
                        /* Get data from vertex buffer */
                        float[] vertexData = new float[part.VertexBuffer.VertexCount * part.VertexBuffer.VertexDeclaration.VertexStride / 4];
                        part.VertexBuffer.GetData(vertexData);

                        VertexElement[] elements = part.VertexBuffer.VertexDeclaration.GetVertexElements();
                        int vertexOffset = elements.First(e => e.VertexElementUsage == VertexElementUsage.Position).Offset / 4;
                        int normalOffset = elements.First(e => e.VertexElementUsage == VertexElementUsage.Normal).Offset / 4;

                        /* Extract vertex positions */
                        positions[thecurrModel, currMesh, currMeshPart] = new List<Vector3>();
                        for (int i = part.VertexOffset; i < part.VertexOffset + part.NumVertices; i++)
                        {
                            int baseIndex = i * part.VertexBuffer.VertexDeclaration.VertexStride / 4;
                            Vector3 vertex = new Vector3(vertexData[baseIndex + vertexOffset],
                                vertexData[baseIndex + vertexOffset + 1],
                                vertexData[baseIndex + vertexOffset + 2]);
                            positions[thecurrModel, currMesh, currMeshPart].Add(vertex);
                        }

                        /* Extract normals */
                        normals[thecurrModel, currMesh, currMeshPart] = new List<Vector3>();
                        for (int i = part.VertexOffset; i < part.VertexOffset + part.NumVertices; i++)
                        {
                            int baseIndex = i * part.VertexBuffer.VertexDeclaration.VertexStride / 4;
                            Vector3 vertex = new Vector3(vertexData[baseIndex + normalOffset],
                               vertexData[baseIndex + normalOffset + 1],
                               vertexData[baseIndex + normalOffset + 2]);
                            normals[thecurrModel, currMesh, currMeshPart].Add(vertex);
                        }

                        /* Combine normals of duplicate vertices */
                        //-----------------------------------------------------------------------------------------------------------------------------------------
                        List<Vector3> combinedNormals = new List<Vector3>();
                        for (int i = 0; i < normals[thecurrModel, currMesh, currMeshPart].Count; i++)
                        {
                            combinedNormals.Add(normals[thecurrModel, currMesh, currMeshPart].ElementAt(i));
                        }
                        for (int i = 0; i < normals[thecurrModel, currMesh, currMeshPart].Count; i++)
                        {
                            Vector3 positionI = positions[thecurrModel, currMesh, currMeshPart].ElementAt(i);
                            List<int> matches = new List<int>();
                            matches.Add(i);
                            for (int j = 0; j < normals[thecurrModel, currMesh, currMeshPart].Count; j++)
                            {
                                Vector3 positionJ = positions[thecurrModel, currMesh, currMeshPart].ElementAt(j);
                                if (i != j && Vector3.Equals(positionI, positionJ))
                                {
                                    matches.Add(j);
                                }
                            }

                            Vector3 combinednormal = normals[thecurrModel, currMesh, currMeshPart].ElementAt(matches.ElementAt(0));
                            for (int j = 1; j < matches.Count; j++)
                            {
                                combinednormal = Vector3.Normalize(combinednormal + normals[thecurrModel, currMesh, currMeshPart].ElementAt(matches.ElementAt(j)));
                            }
                            for (int j = 0; j < matches.Count; j++)
                            {
                                combinedNormals[matches.ElementAt(j)] = combinednormal;
                            }
                        }

                        normals[thecurrModel, currMesh, currMeshPart] = new List<Vector3>();
                        for (int i = 0; i < combinedNormals.Count; i++)
                        {
                            normals[thecurrModel, currMesh, currMeshPart].Add(combinedNormals[i]);
                        }
                        //-----------------------------------------------------------------------------------------------------------------------------------------
                        /* Extract indexes from indexbuffer */
                        ushort[] modelIndexes = new ushort[part.IndexBuffer.IndexCount];
                        part.IndexBuffer.GetData(modelIndexes);

                        indexes[thecurrModel, currMesh, currMeshPart] = new List<ushort>();
                        for (int i = 0; i < part.IndexBuffer.IndexCount; i++)
                        {
                            indexes[thecurrModel, currMesh, currMeshPart].Add(modelIndexes[i]);
                        }
                        currMeshPart++;
                    }
                    currMeshPart = 0;
                    currMesh++;
                }
                currMesh = 0;
                thecurrModel++;
            }
        }
    }
}