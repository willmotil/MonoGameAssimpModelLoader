using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

//using Assimp;

/* Read the below notes for the basics on adding / using assimp directly in monogame via visual studio nuget.net.
 */

// Add the Assimp.net nuget to your solution thru the packet manager in visual stuido.
// The first thing we will need to do in visual studio is ensure assimp.net is installed. 
// in the solution explorer 
// right click on the project
// select manage nuget packages
// click browse
// type in assimp.net and install it to the solution and project via the checkbox on the right.
//
// create a new folder called Assets to initially load fbx or other models well load them directly in from a project folder.
//  this may not be neccessary.
// add... using Assimp; ... to the class files you will create that use the assimp model reader.

/*
 */

// Adding fbx files.
// Add the .Fbx models to the Assets Folder that you want to load.
// If there is no Assets folder right click on the project in the solution explorer an add a new folder by that name.
// Important...
// Select each of the .fbx files you add to that asset folder and by (right clicking on them) 
// Select the "Properties" at the bottom of the list that is presented. 
// Then in the below properties box that opens select the option tiltled "Copy To Output Directory" 
// Select -> "copy if newer" in that drop down shown to the right.
// Leave the build action set to none.

/*
 */

// Optionally you make want to turn on the console for the project to see extra information.
// Do that in visual studio by selecting 
// Debug -> properties at the bottom -> Application on the left -> OutputType drop down select Console.
//

namespace AssimpLoaderExample
{
 
    public class Game1 : Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        SpriteFont currentFont;

        RiggedModel model;
        Effect riggedEffect;  // im using my own skinned effect here.
        Texture2D texture;

        // unrelated stuff to help

        public Basic3dExampleCamera cam;

        public SamplerState ss_standard = new SamplerState() { Filter = TextureFilter.Point, AddressU = TextureAddressMode.Clamp, AddressV = TextureAddressMode.Clamp };
        public DepthStencilState ds_standard_always = new DepthStencilState() { DepthBufferEnable = true, DepthBufferFunction = CompareFunction.Always };
        public DepthStencilState ds_standard_less = new DepthStencilState() { DepthBufferEnable = true, DepthBufferFunction = CompareFunction.Less };
        public DepthStencilState ds_standard_more = new DepthStencilState() { DepthBufferEnable = true, DepthBufferFunction = CompareFunction.Greater };
        public RasterizerState rs_nocullwireframe = new RasterizerState() { CullMode = CullMode.None, FillMode = FillMode.WireFrame };
        public RasterizerState rs_nocullsolid = new RasterizerState() { CullMode = CullMode.None, FillMode = FillMode.Solid };
        public RasterizerState rs_cull_ccw_solid = new RasterizerState() { CullMode = CullMode.CullCounterClockwiseFace, FillMode = FillMode.Solid };
        public RasterizerState rs_cull_cw_solid = new RasterizerState() { CullMode = CullMode.CullClockwiseFace, FillMode = FillMode.Solid };

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            Window.AllowUserResizing = true;
            this.IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

            base.Initialize();
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
            currentFont = Content.Load<SpriteFont>("MgGenFont");

            LoadUpDefaultCamera();

            LoadEffects();

            // load the model

            AssimpFileReader modelReader = new AssimpFileReader(Content, riggedEffect);
            model = modelReader.LoadAsset("dude.fbx", 24);

        }
        public void LoadUpDefaultCamera()
        {
            // i really need to shore up my camera classes into one and fix them.
            cam = new Basic3dExampleCamera(GraphicsDevice, this.Window);
            cam.FieldOfViewDegrees = 85;
            cam.CameraUi(Basic3dExampleCamera.CAM_TYPE_OPTION_FREE);
            cam.CameraType(Basic3dExampleCamera.CAM_UI_OPTION_EDIT_LAYOUT);
            cam.Position = new Vector3(2, 33, 52);
            cam.LookAtDirection = Vector3.Forward;
        }
        public void LoadEffects()
        {
            // the debug version to see a single bone.
            //riggedEffect = Content.Load<Effect>("RiggedModelEffect");
            //riggedEffect.CurrentTechnique = riggedEffect.Techniques["SkinedDebugModelDraw"];
            //riggedEffect.Parameters["boneIdToSee"].SetValue(0f);

            riggedEffect = Content.Load<Effect>("RiggedModelEffect");
            riggedEffect.CurrentTechnique = riggedEffect.Techniques["RiggedModelDraw"];

            riggedEffect.Parameters["WorldLightPosition"].SetValue(new Vector3(100f, 100f, 10f));
            riggedEffect.Parameters["TextureA"].SetValue(texture);
            riggedEffect.Parameters["World"].SetValue(Matrix.Identity);
            riggedEffect.Parameters["View"].SetValue(cam.View);
            riggedEffect.Parameters["Projection"].SetValue(cam.Projection);
        }


        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            if (Keyboard.GetState().IsKeyDown(Keys.R) )
            {
                model.BeginAnimation(0, gameTime);
                model.overrideFrame = -1;
            }
            if (Keyboard.GetState().IsKeyDown(Keys.F) )
                model.StopAnimation();

            if (Keyboard.GetState().IsKeyDown(Keys.Space))
                model.overrideFrame++;


            cam.Update(gameTime);

            model.Update(gameTime);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black); 
            GraphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Black, 1f, 1);
            GraphicsDevice.SamplerStates[0] = ss_standard;
            GraphicsDevice.DepthStencilState = ds_standard_less;
            GraphicsDevice.RasterizerState = rs_cull_cw_solid;

            if (model != null)
            {
                riggedEffect.Parameters["World"].SetValue(Matrix.Identity);
                riggedEffect.Parameters["View"].SetValue(cam.View);
                model.effect = riggedEffect;
                model.Draw(GraphicsDevice);
            }


            DrawSpriteBatches(gameTime);
            base.Draw(gameTime);
        }

        protected void DrawSpriteBatches(GameTime gameTime)
        {
            //currentFrameMsg.Clear();
            //currentFrameMsg.Append("CurrentFrame ").Append(model.currentFrame);
            //spriteBatch.DrawString(Gu.currentFont, currentFrameMsg, new Vector2(10, 400), Color.Wheat);
        }


    }


    /// <summary>
    /// This is a camera i basically remade to make it work. 
    /// Using quite a bit of stuff from my camera class its nearly the same as mine but its a bit simpler. 
    /// I have bunches of cameras at this point and i need to combine them into a fully hard core non basic camera.
    /// That said simple makes for a better example and a better basis to combine them later.
    /// </summary>
    public class Basic3dExampleCamera
    {
        private GraphicsDevice graphicsDevice = null;
        private GameWindow gameWindow = null;

        private MouseState oldmState = default(MouseState);
        private KeyboardState oldkbState = default(KeyboardState);
        MouseState state = default(MouseState);
        KeyboardState kstate = default(KeyboardState);

        public float MovementUnitsPerSecond { get; set; } = 30f;
        public float RotationRadiansPerSecond { get; set; } = 60f;

        public float FieldOfViewDegrees { get; set; } = 80f;
        public float NearClipPlane { get; set; } = .05f;
        public float FarClipPlane { get; set; } = 999900f;

        private float yMouseAngle = 0f;
        private float xMouseAngle = 0f;
        private bool mouseLookIsUsed = true;

        private int KeyboardLayout = 1;
        private int cameraTypeOption = 1;

        /// <summary>
        /// operates pretty much like a fps camera.
        /// </summary>
        public const int CAM_UI_OPTION_FPS_STRAFE_LAYOUT = 0;
        /// <summary>
        /// I put this one on by default.
        /// free cam i use this for editing its more like a air plane or space camera.
        /// the horizon is not corrected for in this one so use the z and c keys to roll 
        /// hold the right mouse to look with it.
        /// </summary>
        public const int CAM_UI_OPTION_EDIT_LAYOUT = 1;
        /// <summary>
        /// Determines how the camera behaves fixed 0  free 1
        /// </summary>

        /// <summary>
        /// A fixed camera is typically used in fps games. It is called a fixed camera because the up is stabalized to the system vectors up.
        /// However be aware that this means if the forward vector or were you are looking is directly up or down you will gimble lock.
        /// Typically this is not allowed in many fps or rather it is faked so you can never truely look directly up or down.
        /// </summary>
        public const int CAM_TYPE_OPTION_FIXED = 0;
        /// <summary>
        /// A free camera has its up vector unlocked perfect for a space sim fighter game or editing. 
        /// It won't gimble lock. Provided the up is crossed from the right forward occasionally it can't gimble lock.
        /// The draw back is the horizon stabilization needs to be handled for some types of games.
        /// </summary>
        public const int CAM_TYPE_OPTION_FREE = 1;


        /// <summary>
        /// Constructs the camera.
        /// </summary>
        public Basic3dExampleCamera(GraphicsDevice gfxDevice, GameWindow window)
        {
            graphicsDevice = gfxDevice;
            gameWindow = window;
            ReCreateWorldAndView();
            ReCreateThePerspectiveProjectionMatrix(gfxDevice, FieldOfViewDegrees);
            oldmState = default(MouseState);
            oldkbState = default(KeyboardState);
        }

        /// <summary>
        /// Select how you want the ui to feel or how to control the camera using Basic3dExampleCamera. CAM_UI_ options
        /// </summary>
        /// <param name="UiOption"></param>
        public void CameraUi(int UiOption)
        {
            KeyboardLayout = UiOption;
        }
        /// <summary>
        /// Select a camera type fixed free or other. using Basic3dExampleCamera. CAM_TYPE_ options
        /// </summary>
        /// <param name="cameraOption"></param>
        public void CameraType(int cameraOption)
        {
            cameraTypeOption = cameraOption;
        }

        /// <summary>
        /// This serves as the cameras up for fixed cameras this might not change at all ever for free cameras it changes constantly.
        /// A fixed camera keeps a fixed horizon but can gimble lock under normal rotation when looking straight up or down.
        /// A free camera has no fixed horizon but can't gimble lock under normal rotation as the up changes as the camera moves.
        /// Most hybrid cameras are a blend of the two but all are based on one or both of the above.
        /// </summary>
        private Vector3 up = Vector3.Up;
        /// <summary>
        /// this serves as the cameras world orientation 
        /// it holds all orientational values and is used to move the camera properly thru the world space as well.
        /// </summary>
        private Matrix camerasWorld = Matrix.Identity;
        /// <summary>
        /// The view matrix is created from the cameras world matrixs but it has special propertys.
        /// Using create look at to create this matrix you move from the world space into the view space.
        /// If you are working on world objects you should not take individual elements from this to directly operate on world matrix components.
        /// As well the multiplication of a view matrix by a world matrix moves the resulting matrix into view space itself.
        /// </summary>
        private Matrix viewMatrix = Matrix.Identity;
        /// <summary>
        /// The projection matrix.
        /// </summary>
        private Matrix projectionMatrix = Matrix.Identity;

        /// <summary>
        /// Gets or sets the the camera's position in the world.
        /// </summary>
        public Vector3 Position
        {
            set
            {
                camerasWorld.Translation = value;
                // since we know here that a change has occured to the cameras world orientations we can update the view matrix.
                ReCreateWorldAndView();
            }
            get { return camerasWorld.Translation; }
        }
        /// <summary>
        /// Gets or Sets the direction the camera is looking at in the world.
        /// The forward is the same as the look at direction it i a directional vector not a position.
        /// </summary>
        public Vector3 Forward
        {
            set
            {
                camerasWorld = Matrix.CreateWorld(camerasWorld.Translation, value, up);
                // since we know here that a change has occured to the cameras world orientations we can update the view matrix.
                ReCreateWorldAndView();
            }
            get { return camerasWorld.Forward; }
        }
        /// <summary>
        /// Get or Set the cameras up vector. Don't set the up unless you understand gimble lock.
        /// </summary>
        public Vector3 Up
        {
            set
            {
                up = value;
                camerasWorld = Matrix.CreateWorld(camerasWorld.Translation, camerasWorld.Forward, value);
                // since we know here that a change has occured to the cameras world orientations we can update the view matrix.
                ReCreateWorldAndView();
            }
            get { return up; }
        }

        /// <summary>
        /// Gets or Sets the direction the camera is looking at in the world as a directional vector.
        /// </summary>
        public Vector3 LookAtDirection
        {
            set
            {
                camerasWorld = Matrix.CreateWorld(camerasWorld.Translation, value, up);
                // since we know here that a change has occured to the cameras world orientations we can update the view matrix.
                ReCreateWorldAndView();
            }
            get { return camerasWorld.Forward; }
        }
        /// <summary>
        /// Sets a positional target in the world to look at.
        /// </summary>
        public Vector3 TargetPositionToLookAt
        {
            set
            {
                camerasWorld = Matrix.CreateWorld(camerasWorld.Translation, Vector3.Normalize(value - camerasWorld.Translation), up);
                // since we know here that a change has occured to the cameras world orientations we can update the view matrix.
                ReCreateWorldAndView();
            }
        }
        /// <summary>
        /// Turns the camera to face the target this method just takes in the targets matrix for convienience.
        /// </summary>
        public Matrix LookAtTheTargetMatrix
        {
            set
            {
                camerasWorld = Matrix.CreateWorld(camerasWorld.Translation, Vector3.Normalize(value.Translation - camerasWorld.Translation), up);
                // since we know here that a change has occured to the cameras world orientations we can update the view matrix.
                ReCreateWorldAndView();
            }
        }

        /// <summary>
        /// Directly set or get the world matrix this also updates the view matrix
        /// </summary>
        public Matrix World
        {
            get
            {
                return camerasWorld;
            }
            set
            {
                camerasWorld = value;
                viewMatrix = Matrix.CreateLookAt(camerasWorld.Translation, camerasWorld.Forward + camerasWorld.Translation, camerasWorld.Up);
            }
        }

        /// <summary>
        /// Gets the view matrix we never really set the view matrix ourselves outside this method just get it.
        /// The view matrix is remade internally when we know the world matrix forward or position is altered.
        /// </summary>
        public Matrix View
        {
            get
            {
                return viewMatrix;
            }
        }

        /// <summary>
        /// Gets the projection matrix.
        /// </summary>
        public Matrix Projection
        {
            get
            {
                return projectionMatrix;
            }
        }

        /// <summary>
        /// When the cameras position or orientation changes, we call this to ensure that the cameras world matrix is orthanormal.
        /// We also set the up depending on our choices of is fix or free camera and we then update the view matrix.
        /// </summary>
        private void ReCreateWorldAndView()
        {
            if (cameraTypeOption == 0)
                up = Vector3.Up;
            if (cameraTypeOption == 1)
                up = camerasWorld.Up;

            camerasWorld = Matrix.CreateWorld(camerasWorld.Translation, camerasWorld.Forward, up);
            viewMatrix = Matrix.CreateLookAt(camerasWorld.Translation, camerasWorld.Forward + camerasWorld.Translation, camerasWorld.Up);
        }

        /// <summary>
        /// Changes the perspective matrix to a new near far and field of view.
        /// </summary>
        public void ReCreateThePerspectiveProjectionMatrix(GraphicsDevice gd, float fovInDegrees)
        {
            projectionMatrix = Matrix.CreatePerspectiveFieldOfView(fovInDegrees * (float)((3.14159265358f) / 180f), gd.Viewport.Width / gd.Viewport.Height, NearClipPlane, FarClipPlane);
        }
        /// <summary>
        /// Changes the perspective matrix to a new near far and field of view.
        /// The projection matrix is typically only set up once at the start of the app.
        /// </summary>
        public void ReCreateThePerspectiveProjectionMatrix(float fieldOfViewInDegrees, float nearPlane, float farPlane)
        {
            // create the projection matrix.
            this.FieldOfViewDegrees = MathHelper.ToRadians(fieldOfViewInDegrees);
            NearClipPlane = nearPlane;
            FarClipPlane = farPlane;
            float aspectRatio = graphicsDevice.Viewport.Width / (float)graphicsDevice.Viewport.Height;
            projectionMatrix = Matrix.CreatePerspectiveFieldOfView(this.FieldOfViewDegrees, aspectRatio, NearClipPlane, FarClipPlane);
        }

        /// <summary>
        /// update the camera.
        /// </summary>
        public void Update(GameTime gameTime)
        {
            oldmState = state;
            oldkbState = kstate;
            state = Mouse.GetState(); //gameWindow);
            kstate = Keyboard.GetState();
            if (KeyboardLayout == CAM_UI_OPTION_FPS_STRAFE_LAYOUT)
                FpsStrafeUiControlsLayout(gameTime);
            if (KeyboardLayout == CAM_UI_OPTION_EDIT_LAYOUT)
                EditingUiControlsLayout(gameTime);
        }

        /// <summary>
        /// like a fps game.
        /// </summary>
        /// <param name="gameTime"></param>
        private void FpsStrafeUiControlsLayout(GameTime gameTime)
        {

            if (kstate.IsKeyDown(Keys.W))
            {
                MoveUp(gameTime);
            }
            else if (kstate.IsKeyDown(Keys.S) == true)
            {
                MoveDown(gameTime);
            }
            // strafe. 
            if (kstate.IsKeyDown(Keys.A) == true)
            {
                MoveLeft(gameTime);
            }
            else if (kstate.IsKeyDown(Keys.D) == true)
            {
                MoveRight(gameTime);
            }

            if (kstate.IsKeyDown(Keys.Q) == true)
            {
                MoveBackward(gameTime);
            }
            else if (kstate.IsKeyDown(Keys.E) == true)
            {
                MoveForward(gameTime);
            }

            // roll counter clockwise
            if (kstate.IsKeyDown(Keys.Z) == true)
            {
                RotateRollCounterClockwise(gameTime);
            }
            // roll clockwise
            else if (kstate.IsKeyDown(Keys.C) == true)
            {
                RotateRollClockwise(gameTime);
            }


            if (state.RightButton == ButtonState.Pressed)
            {
                if (mouseLookIsUsed == false)
                    mouseLookIsUsed = true;
                else
                    mouseLookIsUsed = false;
            }

            if (mouseLookIsUsed)
            {
                Vector2 diff = MouseChange(graphicsDevice, oldmState, mouseLookIsUsed, 2.0f);
                if (diff.X != 0f)
                    RotateLeftOrRight(gameTime, diff.X);
                if (diff.Y != 0f)
                    RotateUpOrDown(gameTime, diff.Y);
            }
        }

        /// <summary>
        /// when working like programing editing and stuff.
        /// </summary>
        /// <param name="gameTime"></param>
        private void EditingUiControlsLayout(GameTime gameTime)
        {
            if (kstate.IsKeyDown(Keys.E))
            {
                MoveForward(gameTime);
            }
            else if (kstate.IsKeyDown(Keys.Q) == true)
            {
                MoveBackward(gameTime);
            }
            if (kstate.IsKeyDown(Keys.W))
            {
                RotateUp(gameTime);
            }
            else if (kstate.IsKeyDown(Keys.S) == true)
            {
                RotateDown(gameTime);
            }
            if (kstate.IsKeyDown(Keys.A) == true)
            {
                RotateLeft(gameTime);
            }
            else if (kstate.IsKeyDown(Keys.D) == true)
            {
                RotateRight(gameTime);
            }

            if (kstate.IsKeyDown(Keys.Left) == true)
            {
                MoveLeft(gameTime);
            }
            else if (kstate.IsKeyDown(Keys.Right) == true)
            {
                MoveRight(gameTime);
            }
            // rotate 
            if (kstate.IsKeyDown(Keys.Up) == true)
            {
                MoveUp(gameTime);
            }
            else if (kstate.IsKeyDown(Keys.Down) == true)
            {
                MoveDown(gameTime);
            }

            // roll counter clockwise
            if (kstate.IsKeyDown(Keys.Z) == true)
            {
                if (cameraTypeOption == CAM_TYPE_OPTION_FREE)
                    RotateRollCounterClockwise(gameTime);
            }
            // roll clockwise
            else if (kstate.IsKeyDown(Keys.C) == true)
            {
                if (cameraTypeOption == CAM_TYPE_OPTION_FREE)
                    RotateRollClockwise(gameTime);
            }

            if (state.RightButton == ButtonState.Pressed)
                mouseLookIsUsed = true;
            else
                mouseLookIsUsed = false;
            if (mouseLookIsUsed)
            {
                Vector2 diff = MouseChange(graphicsDevice, oldmState, mouseLookIsUsed, 2.0f);
                if (diff.X != 0f)
                    RotateLeftOrRight(gameTime, diff.X);
                if (diff.Y != 0f)
                    RotateUpOrDown(gameTime, diff.Y);
            }
        }

        public Vector2 MouseChange(GraphicsDevice gd, MouseState m, bool isCursorSettingPosition, float sensitivity)
        {
            var center = new Point(gd.Viewport.Width / 2, gd.Viewport.Height / 2);
            var delta = m.Position.ToVector2() - center.ToVector2();
            if (isCursorSettingPosition)
            {
                Mouse.SetPosition((int)center.X, center.Y);
            }
            return delta * sensitivity;
        }

        /// <summary>
        /// This function can be used to check if gimble is about to occur in a fixed camera.
        /// If this value returns 1.0f you are in a state of gimble lock, However even as it gets near to 1.0f you are in danger of problems.
        /// In this case you should interpolate towards a free camera. Or begin to handle it.
        /// Earlier then .9 in some manner you deem to appear fitting otherwise you will get a hard spin effect. Though you may want that.
        /// </summary>
        public float GetGimbleLockDangerValue()
        {
            var c0 = Vector3.Dot(World.Forward, World.Up);
            if (c0 < 0f) c0 = -c0;
            return c0;
        }

        #region Local Translations and Rotations.

        public void MoveForward(GameTime gameTime)
        {
            Position += (camerasWorld.Forward * MovementUnitsPerSecond) * (float)gameTime.ElapsedGameTime.TotalSeconds;
        }
        public void MoveBackward(GameTime gameTime)
        {
            Position += (camerasWorld.Backward * MovementUnitsPerSecond) * (float)gameTime.ElapsedGameTime.TotalSeconds;
        }
        public void MoveLeft(GameTime gameTime)
        {
            Position += (camerasWorld.Left * MovementUnitsPerSecond) * (float)gameTime.ElapsedGameTime.TotalSeconds;
        }
        public void MoveRight(GameTime gameTime)
        {
            Position += (camerasWorld.Right * MovementUnitsPerSecond) * (float)gameTime.ElapsedGameTime.TotalSeconds;
        }
        public void MoveUp(GameTime gameTime)
        {
            Position += (camerasWorld.Up * MovementUnitsPerSecond) * (float)gameTime.ElapsedGameTime.TotalSeconds;
        }
        public void MoveDown(GameTime gameTime)
        {
            Position += (camerasWorld.Down * MovementUnitsPerSecond) * (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        public void RotateUp(GameTime gameTime)
        {
            var radians = RotationRadiansPerSecond * (float)gameTime.ElapsedGameTime.TotalSeconds;
            Matrix matrix = Matrix.CreateFromAxisAngle(camerasWorld.Right, MathHelper.ToRadians(radians));
            LookAtDirection = Vector3.TransformNormal(LookAtDirection, matrix);
            ReCreateWorldAndView();
        }
        public void RotateDown(GameTime gameTime)
        {
            var radians = -RotationRadiansPerSecond * (float)gameTime.ElapsedGameTime.TotalSeconds;
            Matrix matrix = Matrix.CreateFromAxisAngle(camerasWorld.Right, MathHelper.ToRadians(radians));
            LookAtDirection = Vector3.TransformNormal(LookAtDirection, matrix);
            ReCreateWorldAndView();
        }
        public void RotateLeft(GameTime gameTime)
        {
            var radians = RotationRadiansPerSecond * (float)gameTime.ElapsedGameTime.TotalSeconds;
            Matrix matrix = Matrix.CreateFromAxisAngle(camerasWorld.Up, MathHelper.ToRadians(radians));
            LookAtDirection = Vector3.TransformNormal(LookAtDirection, matrix);
            ReCreateWorldAndView();
        }
        public void RotateRight(GameTime gameTime)
        {
            var radians = -RotationRadiansPerSecond * (float)gameTime.ElapsedGameTime.TotalSeconds;
            Matrix matrix = Matrix.CreateFromAxisAngle(camerasWorld.Up, MathHelper.ToRadians(radians));
            LookAtDirection = Vector3.TransformNormal(LookAtDirection, matrix);
            ReCreateWorldAndView();
        }
        public void RotateRollClockwise(GameTime gameTime)
        {
            var radians = RotationRadiansPerSecond * (float)gameTime.ElapsedGameTime.TotalSeconds;
            var pos = camerasWorld.Translation;
            camerasWorld *= Matrix.CreateFromAxisAngle(camerasWorld.Forward, MathHelper.ToRadians(radians));
            camerasWorld.Translation = pos;
            ReCreateWorldAndView();
        }
        public void RotateRollCounterClockwise(GameTime gameTime)
        {
            var radians = -RotationRadiansPerSecond * (float)gameTime.ElapsedGameTime.TotalSeconds;
            var pos = camerasWorld.Translation;
            camerasWorld *= Matrix.CreateFromAxisAngle(camerasWorld.Forward, MathHelper.ToRadians(radians));
            camerasWorld.Translation = pos;
            ReCreateWorldAndView();
        }

        // just for example this is the same as the above rotate left or right.
        public void RotateLeftOrRight(GameTime gameTime, float amount)
        {
            var radians = amount * -RotationRadiansPerSecond * (float)gameTime.ElapsedGameTime.TotalSeconds;
            Matrix matrix = Matrix.CreateFromAxisAngle(camerasWorld.Up, MathHelper.ToRadians(radians));
            LookAtDirection = Vector3.TransformNormal(LookAtDirection, matrix);
            ReCreateWorldAndView();
        }
        public void RotateUpOrDown(GameTime gameTime, float amount)
        {
            var radians = amount * -RotationRadiansPerSecond * (float)gameTime.ElapsedGameTime.TotalSeconds;
            Matrix matrix = Matrix.CreateFromAxisAngle(camerasWorld.Right, MathHelper.ToRadians(radians));
            LookAtDirection = Vector3.TransformNormal(LookAtDirection, matrix);
            ReCreateWorldAndView();
        }

        #endregion

        #region Non Local System Translations and Rotations.

        public void MoveForwardInNonLocalSystemCoordinates(GameTime gameTime)
        {
            Position += (Vector3.Forward * MovementUnitsPerSecond) * (float)gameTime.ElapsedGameTime.TotalSeconds;
        }
        public void MoveBackwardsInNonLocalSystemCoordinates(GameTime gameTime)
        {
            Position += (Vector3.Backward * MovementUnitsPerSecond) * (float)gameTime.ElapsedGameTime.TotalSeconds;
        }
        public void MoveUpInNonLocalSystemCoordinates(GameTime gameTime)
        {
            Position += (Vector3.Up * MovementUnitsPerSecond) * (float)gameTime.ElapsedGameTime.TotalSeconds;
        }
        public void MoveDownInNonLocalSystemCoordinates(GameTime gameTime)
        {
            Position += (Vector3.Down * MovementUnitsPerSecond) * (float)gameTime.ElapsedGameTime.TotalSeconds;
        }
        public void MoveLeftInNonLocalSystemCoordinates(GameTime gameTime)
        {
            Position += (Vector3.Left * MovementUnitsPerSecond) * (float)gameTime.ElapsedGameTime.TotalSeconds;
        }
        public void MoveRightInNonLocalSystemCoordinates(GameTime gameTime)
        {
            Position += (Vector3.Right * MovementUnitsPerSecond) * (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        /// <summary>
        /// These aren't typically useful and you would just use create world for a camera snap to a new view. I leave them for completeness.
        /// </summary>
        public void NonLocalRotateLeftOrRight(GameTime gameTime, float amount)
        {
            var radians = amount * -RotationRadiansPerSecond * (float)gameTime.ElapsedGameTime.TotalSeconds;
            Matrix matrix = Matrix.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(radians));
            LookAtDirection = Vector3.TransformNormal(LookAtDirection, matrix);
            ReCreateWorldAndView();
        }
        /// <summary>
        /// These aren't typically useful and you would just use create world for a camera snap to a new view.  I leave them for completeness.
        /// </summary>
        public void NonLocalRotateUpOrDown(GameTime gameTime, float amount)
        {
            var radians = amount * -RotationRadiansPerSecond * (float)gameTime.ElapsedGameTime.TotalSeconds;
            Matrix matrix = Matrix.CreateFromAxisAngle(Vector3.Right, MathHelper.ToRadians(radians));
            LookAtDirection = Vector3.TransformNormal(LookAtDirection, matrix);
            ReCreateWorldAndView();
        }

        #endregion
    }
}
