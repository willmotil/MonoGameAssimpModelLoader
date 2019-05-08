using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AssimpLoaderExample
{
    public class VisualizationModelHelperClasses
    {
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
        public float RotationDegreesPerSecond { get; set; } = 60f;

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
            var radians = RotationDegreesPerSecond * (float)gameTime.ElapsedGameTime.TotalSeconds;
            Matrix matrix = Matrix.CreateFromAxisAngle(camerasWorld.Right, MathHelper.ToRadians(radians));
            LookAtDirection = Vector3.TransformNormal(LookAtDirection, matrix);
            ReCreateWorldAndView();
        }
        public void RotateDown(GameTime gameTime)
        {
            var radians = -RotationDegreesPerSecond * (float)gameTime.ElapsedGameTime.TotalSeconds;
            Matrix matrix = Matrix.CreateFromAxisAngle(camerasWorld.Right, MathHelper.ToRadians(radians));
            LookAtDirection = Vector3.TransformNormal(LookAtDirection, matrix);
            ReCreateWorldAndView();
        }
        public void RotateLeft(GameTime gameTime)
        {
            var radians = RotationDegreesPerSecond * (float)gameTime.ElapsedGameTime.TotalSeconds;
            Matrix matrix = Matrix.CreateFromAxisAngle(camerasWorld.Up, MathHelper.ToRadians(radians));
            LookAtDirection = Vector3.TransformNormal(LookAtDirection, matrix);
            ReCreateWorldAndView();
        }
        public void RotateRight(GameTime gameTime)
        {
            var radians = -RotationDegreesPerSecond * (float)gameTime.ElapsedGameTime.TotalSeconds;
            Matrix matrix = Matrix.CreateFromAxisAngle(camerasWorld.Up, MathHelper.ToRadians(radians));
            LookAtDirection = Vector3.TransformNormal(LookAtDirection, matrix);
            ReCreateWorldAndView();
        }
        public void RotateRollClockwise(GameTime gameTime)
        {
            var radians = RotationDegreesPerSecond * (float)gameTime.ElapsedGameTime.TotalSeconds;
            var pos = camerasWorld.Translation;
            camerasWorld *= Matrix.CreateFromAxisAngle(camerasWorld.Forward, MathHelper.ToRadians(radians));
            camerasWorld.Translation = pos;
            ReCreateWorldAndView();
        }
        public void RotateRollCounterClockwise(GameTime gameTime)
        {
            var radians = -RotationDegreesPerSecond * (float)gameTime.ElapsedGameTime.TotalSeconds;
            var pos = camerasWorld.Translation;
            camerasWorld *= Matrix.CreateFromAxisAngle(camerasWorld.Forward, MathHelper.ToRadians(radians));
            camerasWorld.Translation = pos;
            ReCreateWorldAndView();
        }

        // just for example this is the same as the above rotate left or right.
        public void RotateLeftOrRight(GameTime gameTime, float amount)
        {
            var radians = amount * -RotationDegreesPerSecond * (float)gameTime.ElapsedGameTime.TotalSeconds;
            Matrix matrix = Matrix.CreateFromAxisAngle(camerasWorld.Up, MathHelper.ToRadians(radians));
            LookAtDirection = Vector3.TransformNormal(LookAtDirection, matrix);
            ReCreateWorldAndView();
        }
        public void RotateUpOrDown(GameTime gameTime, float amount)
        {
            var radians = amount * -RotationDegreesPerSecond * (float)gameTime.ElapsedGameTime.TotalSeconds;
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
            var radians = amount * -RotationDegreesPerSecond * (float)gameTime.ElapsedGameTime.TotalSeconds;
            Matrix matrix = Matrix.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(radians));
            LookAtDirection = Vector3.TransformNormal(LookAtDirection, matrix);
            ReCreateWorldAndView();
        }
        /// <summary>
        /// These aren't typically useful and you would just use create world for a camera snap to a new view.  I leave them for completeness.
        /// </summary>
        public void NonLocalRotateUpOrDown(GameTime gameTime, float amount)
        {
            var radians = amount * -RotationDegreesPerSecond * (float)gameTime.ElapsedGameTime.TotalSeconds;
            Matrix matrix = Matrix.CreateFromAxisAngle(Vector3.Right, MathHelper.ToRadians(radians));
            LookAtDirection = Vector3.TransformNormal(LookAtDirection, matrix);
            ReCreateWorldAndView();
        }

        #endregion
    }

    public class ModelsVisualNormals
    {
        Texture2D texture;
        RiggedAlignedNormalArrows[] modelNormalArrows;

        public ModelsVisualNormals(RiggedModel model, Texture2D t, float thickness, float scale)
        {
            modelNormalArrows = new RiggedAlignedNormalArrows[model.meshes.Length];
            for (int i = 0; i < model.meshes.Length; i++)
            {
                modelNormalArrows[i] = new RiggedAlignedNormalArrows();
                modelNormalArrows[i].texture = t;
                modelNormalArrows[i].CreateVisualNormalsForPrimitiveMesh(model.meshes[i].vertices, model.meshes[i].indices, t, 1f, 1f);
            }
        }

        public void Draw(GraphicsDevice device, Effect effect)
        {
            effect.CurrentTechnique = effect.Techniques["RiggedModelNormalDraw"];
            for (int i = 0; i < modelNormalArrows.Length; i++)
            {
                modelNormalArrows[i].Draw(device, effect);
            }
        }
    }

    public class RiggedAlignedNormalArrows
    {
        VertexPositionTextureNormalTangentWeights[] vertices;
        int[] indices;

        public Texture2D texture;
        RiggedAlignedNormalArrows[] modelNormalArrows;

        public RiggedAlignedNormalArrows()
        {
        }

        public void CreateVisualNormalsForPrimitiveMesh(VertexPositionTextureNormalTangentWeights[] inVertices, int[] inIndices, Texture2D t, float thickness, float scale)
        {
            texture = t;
            int len = inVertices.Length;

            VertexPositionTextureNormalTangentWeights[] nverts = new VertexPositionTextureNormalTangentWeights[len * 4];
            int[] nindices = new int[len * 6];

            for (int j = 0; j < len; j++)
            {
                int v = j * 4;
                int i = j * 6;
                //
                //ReCreateForwardNormalQuad(vertices[i].Position, vertices[i].Normal);
                //nverts[v + 0].Color = inVertices[j].Color;
                //nverts[v + 1].Color = inVertices[j].Color;
                //nverts[v + 2].Color = inVertices[j].Color;
                //nverts[v + 3].Color = inVertices[j].Color;
                //
                nverts[v + 0].TextureCoordinate = new Vector2(0f, 0f);//vertices[v + 0].TextureCoordinateA;
                nverts[v + 1].TextureCoordinate = new Vector2(0f, .33f); //vertices[v + 1].TextureCoordinateA;
                nverts[v + 2].TextureCoordinate = new Vector2(1f, .0f);//vertices[v + 2].TextureCoordinateA;
                nverts[v + 3].TextureCoordinate = new Vector2(1f, .33f);//vertices[v + 3].TextureCoordinateA;
                //
                nverts[v + 0].Position = new Vector3(0f, 0f, 0f) + inVertices[j].Position;
                nverts[v + 1].Position = new Vector3(0f, -.2f * thickness, 0f) + inVertices[j].Position;
                nverts[v + 2].Position = new Vector3(0f, 0f, 0f) + inVertices[j].Position + inVertices[j].Normal * scale;
                nverts[v + 3].Position = new Vector3(0f, -.2f * thickness, 0f) + inVertices[j].Position + inVertices[j].Normal * scale;
                //
                nverts[v + 0].Color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
                nverts[v + 1].Color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
                nverts[v + 2].Color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
                nverts[v + 3].Color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
                //
                nverts[v + 0].Normal = inVertices[j].Normal;
                nverts[v + 1].Normal = inVertices[j].Normal;
                nverts[v + 2].Normal = inVertices[j].Normal;
                nverts[v + 3].Normal = inVertices[j].Normal;
                //
                nverts[v + 0].BlendIndices = inVertices[j].BlendIndices;
                nverts[v + 1].BlendIndices = inVertices[j].BlendIndices;
                nverts[v + 2].BlendIndices = inVertices[j].BlendIndices;
                nverts[v + 3].BlendIndices = inVertices[j].BlendIndices;
                //
                nverts[v + 0].BlendWeights = inVertices[j].BlendWeights;
                nverts[v + 1].BlendWeights = inVertices[j].BlendWeights;
                nverts[v + 2].BlendWeights = inVertices[j].BlendWeights;
                nverts[v + 3].BlendWeights = inVertices[j].BlendWeights;

                // indices
                nindices[i + 0] = 0 + v;
                nindices[i + 1] = 1 + v;
                nindices[i + 2] = 2 + v;
                nindices[i + 3] = 2 + v;
                nindices[i + 4] = 1 + v;
                nindices[i + 5] = 3 + v;
            }
            this.vertices = nverts;
            this.indices = nindices;
        }

        public void Draw(GraphicsDevice gd, Effect effect)
        {
            effect.Parameters["TextureA"].SetValue(texture);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length, indices, 0, (indices.Length / 3), VertexPositionTextureNormalTangentWeights.VertexDeclaration);
            }
        }
    }

    public class NormalArrow
    {
        VertexPositionColorNormalTexture[] vertices;
        int[] indices;
        public Texture2D texture;

        public NormalArrow(VertexPositionNormalTexture[] inVertices, int[] inIndices, Texture2D t, float scale)
        {
            CreateVisualNormalsForPrimitiveMesh(inVertices, inIndices, t, scale);
        }
        public NormalArrow(VertexPositionColorNormalTextureTangent[] inVertices, int[] inIndices, Texture2D t, float scale)
        {
            VertexPositionNormalTexture[] v = new VertexPositionNormalTexture[inVertices.Length];
            int[] i = new int[inIndices.Length];
            for(int n =0; n < inVertices.Length; n++)
            {
                v[n].Position = inVertices[n].Position;
                v[n].Normal = inVertices[n].Normal;
            }
            for (int n = 0; n < inIndices.Length; n++)
            {
                i[n] = inIndices[n];
            }
            CreateVisualNormalsForPrimitiveMesh(v, i, t, scale);
        }
        public NormalArrow(VertexPositionTextureNormalTangentWeights[] inVertices, int[] inIndices, Texture2D t, float scale)
        {
            VertexPositionNormalTexture[] v = new VertexPositionNormalTexture[inVertices.Length];
            int[] i = new int[inIndices.Length];
            for (int n = 0; n < inVertices.Length; n++)
            {
                v[n].Position = inVertices[n].Position;
                v[n].Normal = inVertices[n].Normal;
            }
            for (int n = 0; n < inIndices.Length; n++)
            {
                i[n] = inIndices[n];
            }
            CreateVisualNormalsForPrimitiveMesh(v, i, t, scale);
        }     

        public void CreateVisualNormalsForPrimitiveMesh(VertexPositionNormalTexture[] inVertices, int[] inIndices , Texture2D t, float scale)
        {
            texture = t;
            int len = inVertices.Length;

            VertexPositionColorNormalTexture[] nverts = new VertexPositionColorNormalTexture[len * 4];
            int[] nindices = new int[len * 6];

            for (int j = 0; j < len; j++)
            {
                int v = j * 4;
                int i = j * 6;
                //ReCreateForwardNormalQuad(vertices[i].Position, vertices[i].Normal);
                nverts[v + 0].Color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
                nverts[v + 1].Color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
                nverts[v + 2].Color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
                nverts[v + 3].Color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
                //
                nverts[v + 0].TextureCoordinate = new Vector2(0f, 0f);//vertices[v + 0].TextureCoordinateA;
                nverts[v + 1].TextureCoordinate = new Vector2(0f, .33f); //vertices[v + 1].TextureCoordinateA;
                nverts[v + 2].TextureCoordinate = new Vector2(1f, .0f);//vertices[v + 2].TextureCoordinateA;
                nverts[v + 3].TextureCoordinate = new Vector2(1f, .33f);//vertices[v + 3].TextureCoordinateA;
                //
                nverts[v + 0].Position = new Vector3(0f, 0f, 0f) + inVertices[j].Position;
                nverts[v + 1].Position = new Vector3(0f, -.2f, 0f) + inVertices[j].Position;
                nverts[v + 2].Position = new Vector3(0f, 0f, 0f) + inVertices[j].Position + inVertices[j].Normal * scale;
                nverts[v + 3].Position = new Vector3(0f, -.2f, 0f) + inVertices[j].Position + inVertices[j].Normal * scale;
                //
                nverts[v + 0].Normal = inVertices[j].Normal;
                nverts[v + 1].Normal = inVertices[j].Normal;
                nverts[v + 2].Normal = inVertices[j].Normal;
                nverts[v + 3].Normal = inVertices[j].Normal;

                // indices
                nindices[i + 0] = 0 + v;
                nindices[i + 1] = 1 + v;
                nindices[i + 2] = 2 + v;
                nindices[i + 3] = 2 + v;
                nindices[i + 4] = 1 + v;
                nindices[i + 5] = 3 + v;
            }
            this.vertices = nverts;
            this.indices = nindices;
        }

        public void Draw(GraphicsDevice gd, Effect effect)
        {
            effect.Parameters["TextureA"].SetValue(texture);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length, indices, 0, (indices.Length / 3), VertexPositionColorNormalTexture.VertexDeclaration);
            }
        }
    }

    public class LinePCT
    {
        VertexPositionColorTexture[] vertices;
        int[] indices;

        public Vector3 camUp = Vector3.Up;

        public LinePCT(float linewidth, Color c, Vector3 start, Vector3 end)
        {
            CreateLine(linewidth, c, c, start, end);
        }
        public LinePCT(float linewidth, Color colorStart, Color colorEnd, Vector3 start, Vector3 end)
        {
            CreateLine(linewidth, colorStart, colorEnd, start, end);
        }

        private void CreateLine(float linewidth, Color cs, Color ce, Vector3 start, Vector3 end)
        {
            var a = end - start;
            a.Normalize();
            var b = Vector3.Up;
            float n = Vector3.Dot(a, b);
            if (n * n > .95f)
                b = Vector3.Right;
            var su = Vector3.Cross(a, b);
            var sr = Vector3.Cross(a, su);
            var offsetup = su * linewidth;
            var offsetright = sr * linewidth;

            Vector3 s0 = start + offsetright - offsetup;
            Vector3 s1 = start - offsetright - offsetup;
            Vector3 s2 = start + offsetup;

            Vector3 e0 = end + offsetright - offsetup;
            Vector3 e1 = end - offsetright - offsetup;
            Vector3 e2 = end + offsetup;

            Vector2 uv0 = new Vector2(0f, 1f);
            Vector2 uv1 = new Vector2(0f, 0f);
            Vector2 uv2 = new Vector2(1f, 0f);
            Vector2 uv3 = new Vector2(1f, 1f);

            vertices = new VertexPositionColorTexture[12];
            indices = new int[18];

            int v = 0;
            int i = 0;
            // q1
            vertices[v].Position = s0; vertices[v].Color = cs; vertices[v].TextureCoordinate = uv0; v++;
            vertices[v].Position = s1; vertices[v].Color = cs; vertices[v].TextureCoordinate = uv1; v++;
            vertices[v].Position = e0; vertices[v].Color = ce; vertices[v].TextureCoordinate = uv2; v++;
            vertices[v].Position = e1; vertices[v].Color = ce; vertices[v].TextureCoordinate = uv3; v++;

            var vi = v - 4;
            indices[i + 0] = vi + 0; indices[i + 1] = vi + 1; indices[i + 2] = vi + 2;
            indices[i + 3] = vi + 2; indices[i + 4] = vi + 1; indices[i + 5] = vi + 3;
            i += 6;

            // q2
            vertices[v].Position = s1; vertices[v].Color = cs; vertices[v].TextureCoordinate = uv0; v++;
            vertices[v].Position = s2; vertices[v].Color = cs; vertices[v].TextureCoordinate = uv1; v++;
            vertices[v].Position = e1; vertices[v].Color = ce; vertices[v].TextureCoordinate = uv2; v++;
            vertices[v].Position = e2; vertices[v].Color = ce; vertices[v].TextureCoordinate = uv3; v++;

            vi = v - 4;
            indices[i + 0] = vi + 0; indices[i + 1] = vi + 1; indices[i + 2] = vi + 2;
            indices[i + 3] = vi + 2; indices[i + 4] = vi + 1; indices[i + 5] = vi + 3;
            i += 6;

            //q3
            vertices[v].Position = s2; vertices[v].Color = cs; vertices[v].TextureCoordinate = uv0; v++;
            vertices[v].Position = s0; vertices[v].Color = cs; vertices[v].TextureCoordinate = uv1; v++;
            vertices[v].Position = e2; vertices[v].Color = ce; vertices[v].TextureCoordinate = uv2; v++;
            vertices[v].Position = e0; vertices[v].Color = ce; vertices[v].TextureCoordinate = uv3; v++;

            vi = v - 4;
            indices[i + 0] = vi + 0; indices[i + 1] = vi + 1; indices[i + 2] = vi + 2;
            indices[i + 3] = vi + 2; indices[i + 4] = vi + 1; indices[i + 5] = vi + 3;
            i += 6;
        }
        public void Draw(GraphicsDevice gd, Effect effect)
        {
            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length, indices, 0, (indices.Length / 3), VertexPositionColorTexture.VertexDeclaration);
            }
        }
    }

    /// <summary>
    /// This is basically my super sphere created by and last updated willmotil 2019.
    /// This is a sphere or a sky sphere. A face resolution of 2 is also a cube or sky cube.
    /// It can use 6 seperate images on 6 faces or a cross or blender block type texture..
    /// Both Sphere and skyShere Uses CCW culling in regular operation.
    /// It generates positions normals texture and tangents for normal maping.
    /// It tesselates face points into sphereical coordinates on creation.
    /// It can also switch tangent or normal directions or u v that shouldn't be needed though.
    /// </summary>
    public class SpherePNTT
    {
        bool changeToSkySphere = false;
        bool useSingleImageTexture = false;
        bool blenderStyleElseCross = false;
        bool flipTangentSign = false;
        bool flipVerticeWindingToCW = false;
        bool flipNormalDirection = false;
        bool flipU = false;
        bool flipV = false;
        int verticeFaceResolution = 3;
        float scale = 1f;

        int verticeFaceDrawOffset = 0;
        int indiceFaceDrawOffset = 0;
        int verticesPerFace = 0;
        int indicesPerFace = 0;
        int primitivesPerFace = 0;

        // face identifiers
        const int FaceFront = 0;
        const int FaceBack = 1;
        const int FaceLeft = 2;
        const int FaceRight = 3;
        const int FaceTop = 4;
        const int FaceBottom = 5;

        public VertexPositionColorNormalTextureTangent[] vertices = new VertexPositionColorNormalTextureTangent[24];
        public int[] indices = new int[36];

        /// <summary>
        /// Defaults to a single image hexahedron used on all faces. 
        /// Use the other overloads if you want something more specific like a sphere by increasing vertexResolutionPerFace.
        /// The spheres are counter clockwise wound that can be changed by setting the skysphere bool or fliping the winding direction.
        /// The skySphere is clockwise wound normally 
        /// if you are using opposite or strange orders for normals or whatever this has option to match just about everything.
        /// </summary>
        public SpherePNTT()
        {
            CreateSixFaceSphere(true, false, false, false, false,false, false, false, verticeFaceResolution, scale);
        }
        // seperate faces
        public SpherePNTT(bool changeToSkySphere)
        {
            CreateSixFaceSphere(changeToSkySphere, false, false, false,false, false, false, false, verticeFaceResolution, scale);
        }
        // seperate faces at resolution
        public SpherePNTT(bool changeToSkySphere, int vertexResolutionPerFace, float scale)
        {
            CreateSixFaceSphere(changeToSkySphere, false, false, false, false, false, false, false, vertexResolutionPerFace, scale);
        }
        public SpherePNTT(bool changeToSkySphere, int vertexResolutionPerFace, float scale, bool flipWindingDirection)
        {
            CreateSixFaceSphere(changeToSkySphere, false, false, false, flipWindingDirection,false, false, false, vertexResolutionPerFace, scale);
        }
        public SpherePNTT(bool changeToSkySphere, int vertexResolutionPerFace, float scale, bool flipNormalDirection, bool flipWindingDirection)
        {
            CreateSixFaceSphere(changeToSkySphere, false, false, flipNormalDirection, flipWindingDirection, false, false, false, vertexResolutionPerFace, scale);
        }
        /// <summary>
        /// Set the type, if the faces are in a single image or six seperate images and if the single image is a cross or blender type image.
        /// Additionally specify the number of vertices per face this value is squared as it is used for rows and columns.
        /// </summary>
        public SpherePNTT(bool changeToSkySphere, bool changeToSingleImageTexture, bool blenderStyleSkyBox, int vertexResolutionPerFace, float scale)
        {
            CreateSixFaceSphere(changeToSkySphere, changeToSingleImageTexture, blenderStyleSkyBox, false,false, false, false, false, vertexResolutionPerFace, scale);
        }
        public SpherePNTT(bool changeToSkySphere, bool changeToSingleImageTexture, bool blenderStyleSkyBox, int vertexResolutionPerFace, float scale, bool flipWindingDirection)
        {
            CreateSixFaceSphere(changeToSkySphere, changeToSingleImageTexture, blenderStyleSkyBox, false, flipWindingDirection, false, false, false, vertexResolutionPerFace, scale);
        }
        public SpherePNTT(bool changeToSkySphere, bool changeToSingleImageTexture, bool blenderStyleSkyBox, int vertexResolutionPerFace, float scale, bool flipNormalDirection, bool flipWindingDirection)
        {
            CreateSixFaceSphere(changeToSkySphere, changeToSingleImageTexture, blenderStyleSkyBox, flipNormalDirection, flipWindingDirection, false, false, false, vertexResolutionPerFace, scale);
        }
        public SpherePNTT(bool changeToSkySphere, bool changeToSingleImageTexture, bool blenderStyleSkyBox, bool flipNormalDirection , bool flipWindingDirection, bool flipTangentDirection, bool flipTextureDirectionU, bool flipTextureDirectionV, int vertexResolutionPerFace, float scale)
        {
            CreateSixFaceSphere(changeToSkySphere, changeToSingleImageTexture, blenderStyleSkyBox, flipNormalDirection, flipWindingDirection, flipTangentDirection, flipTextureDirectionU, flipTextureDirectionV, vertexResolutionPerFace, scale);
        }

        void CreateSixFaceSphere(bool changeToSkySphere, bool changeToSingleImageTexture , bool blenderStyleElseCross, bool flipNormalDirection , bool flipWindingDirection, bool flipTangentDirection, bool flipU, bool flipV, int vertexResolutionPerFace, float scale)
        {
            this.scale = scale;
            this.changeToSkySphere = changeToSkySphere;
            this.useSingleImageTexture = changeToSingleImageTexture;
            this.blenderStyleElseCross = blenderStyleElseCross;
            this.flipVerticeWindingToCW = flipWindingDirection;
            this.flipNormalDirection = flipNormalDirection;
            this.flipTangentSign = flipTangentDirection;
            this.flipU = flipU;
            this.flipV = flipV;
            if (vertexResolutionPerFace < 2)
                vertexResolutionPerFace = 2;
            this.verticeFaceResolution = vertexResolutionPerFace;
            Vector3 offset = new Vector3(.5f, .5f, .5f);
            // 8 vertice points ill label them, then reassign them for clarity.
            Vector3 LT_f = new Vector3(0, 1, 0) - offset; Vector3 A = LT_f * scale;
            Vector3 LB_f = new Vector3(0, 0, 0) - offset; Vector3 B = LB_f * scale;
            Vector3 RT_f = new Vector3(1, 1, 0) - offset; Vector3 C = RT_f * scale;
            Vector3 RB_f = new Vector3(1, 0, 0) - offset; Vector3 D = RB_f * scale;
            Vector3 LT_b = new Vector3(0, 1, 1) - offset; Vector3 E = LT_b * scale;
            Vector3 LB_b = new Vector3(0, 0, 1) - offset; Vector3 F = LB_b * scale;
            Vector3 RT_b = new Vector3(1, 1, 1) - offset; Vector3 G = RT_b * scale;
            Vector3 RB_b = new Vector3(1, 0, 1) - offset; Vector3 H = RB_b * scale;
            if (flipVerticeWindingToCW)
            {
                LT_f = new Vector3(0, 1, 0) - offset; H = LT_f * scale;
                LB_f = new Vector3(0, 0, 0) - offset; G = LB_f * scale;
                RT_f = new Vector3(1, 1, 0) - offset; F = RT_f * scale;
                RB_f = new Vector3(1, 0, 0) - offset; E = RB_f * scale;
                LT_b = new Vector3(0, 1, 1) - offset; D = LT_b * scale;
                LB_b = new Vector3(0, 0, 1) - offset; C = LB_b * scale;
                RT_b = new Vector3(1, 1, 1) - offset; B = RT_b * scale;
                RB_b = new Vector3(1, 0, 1) - offset; A = RB_b * scale;
            }

            // Six faces to a cube or sphere
            // each face of the cube wont actually share vertices as each will use its own texture.
            // unless it is actually using single skybox texture

            // we will need to precalculate the grids size now
            int vw = vertexResolutionPerFace;
            int vh = vertexResolutionPerFace;
            int vlen = vw * vh * 6; // the extra six here is the number of faces
            int iw = vw - 1;
            int ih = vh - 1;
            int ilen = iw * ih * 6 * 6; // the extra six here is the number of faces
            vertices = new VertexPositionColorNormalTextureTangent[vlen];
            indices = new int[ilen];
            verticeFaceDrawOffset = vlen = vw * vh;
            indiceFaceDrawOffset = ilen = iw * ih * 6;
            verticesPerFace = vertexResolutionPerFace * vertexResolutionPerFace;
            indicesPerFace = iw * ih * 6;
            primitivesPerFace = iw * ih * 2; // 2 triangles per quad

            if (changeToSkySphere)
            {
                // passed uv texture coordinates.
                Vector2 uv0 = new Vector2(1f, 1f);
                Vector2 uv1 = new Vector2(0f, 1f);
                Vector2 uv2 = new Vector2(1f, 0f);
                Vector2 uv3 = new Vector2(0f, 0f);
                SetFaceGrid(FaceFront, D, B, C, A, uv0, uv1, uv2, uv3, vertexResolutionPerFace);
                SetFaceGrid(FaceBack, F, H, E, G, uv0, uv1, uv2, uv3, vertexResolutionPerFace);
                SetFaceGrid(FaceLeft, B, F, A, E, uv0, uv1, uv2, uv3, vertexResolutionPerFace);
                SetFaceGrid(FaceRight, H, D, G, C, uv0, uv1, uv2, uv3, vertexResolutionPerFace);
                SetFaceGrid(FaceTop, C, A, G, E, uv0, uv1, uv2, uv3, vertexResolutionPerFace);
                SetFaceGrid(FaceBottom, H, F, D, B, uv0, uv1, uv2, uv3, vertexResolutionPerFace);
            }
            else // regular cube 
            {
                Vector2 uv0 = new Vector2(0f, 0f);
                Vector2 uv1 = new Vector2(0f, 1f);
                Vector2 uv2 = new Vector2(1f, 0f);
                Vector2 uv3 = new Vector2(1f, 1f);
                SetFaceGrid(FaceFront, A, B, C, D, uv0, uv1, uv2, uv3, vertexResolutionPerFace);
                SetFaceGrid(FaceBack, G, H, E, F, uv0, uv1, uv2, uv3, vertexResolutionPerFace);
                SetFaceGrid(FaceLeft, E, F, A, B, uv0, uv1, uv2, uv3, vertexResolutionPerFace);
                SetFaceGrid(FaceRight, C, D, G, H, uv0, uv1, uv2, uv3, vertexResolutionPerFace);
                SetFaceGrid(FaceTop, E, A, G, C, uv0, uv1, uv2, uv3, vertexResolutionPerFace);
                SetFaceGrid(FaceBottom, B, F, D, H, uv0, uv1, uv2, uv3, vertexResolutionPerFace);
            }
        }

        void SetFaceGrid(int faceMultiplier, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3, int vertexResolution)
        {
            if (useSingleImageTexture)
                UvSkyTextureReassignment(faceMultiplier, ref uv0, ref uv1, ref uv2, ref uv3);
            int vw = vertexResolution;
            int vh = vertexResolution;
            int vlen = vw * vh;
            int iw = vw - 1;
            int ih = vh - 1;
            int ilen = iw * ih * 6;
            // actual start index's
            int vIndex = faceMultiplier * vlen;
            int iIndex = faceMultiplier * ilen;
            // we now must build the grid/
            float ratio = 1f / (float)(vertexResolution - 1);
            // well do it all simultaneously no point in spliting it up
            for (int y = 0; y < vertexResolution; y++)
            {
                float ratioY = (float)y * ratio;
                for (int x = 0; x < vertexResolution; x++)
                {
                    // index
                    int index = vIndex + (y * vertexResolution + x);
                    float ratioX = (float)x * ratio;
                    // calculate uv_n_p tangent comes later
                    var uv = InterpolateUv(uv0, uv1, uv2, uv3, ratioX, ratioY);
                    var n = InterpolateToNormal(v0, v1, v2, v3, ratioX, ratioY);
                    var p = n * .5f * scale; // displace to distance
                    if (changeToSkySphere)
                        n = -n;
                    if (flipNormalDirection)
                        n = -n;
                    // handle u v fliping if its desired.
                    if (flipU)
                        uv.X = 1.0f - uv.X;
                    if (flipV)
                        uv.Y = 1.0f - uv.Y;
                    // assign
                    vertices[index].Position = p;
                    vertices[index].Color = new Vector4(1.0f,1.0f,1.0f,1.0f);
                    vertices[index].TextureCoordinate = uv;
                    vertices[index].Normal = n;
                }
            }

            // ToDo... 
            // We could loop all the vertices which are nearly the exact same and make sure they are the same place but seperate.
            // sort of redundant but floating point errors happen under interpolation, well get back to that later on.
            // not sure i really need to it looks pretty spot on.

            // ok so now we have are positions our normal and uv per vertice we need to loop again and handle the tangents
            for (int y = 0; y < (vertexResolution - 1); y++)
            {
                for (int x = 0; x < (vertexResolution - 1); x++)
                {
                    //
                    int indexV0 = vIndex + (y * vertexResolution + x);
                    int indexV1 = vIndex + ((y + 1) * vertexResolution + x);
                    int indexV2 = vIndex + (y * vertexResolution + (x + 1));
                    int indexV3 = vIndex + ((y + 1) * vertexResolution + (x + 1));
                    var p0 = vertices[indexV0].Position;
                    var p1 = vertices[indexV1].Position;
                    var p2 = vertices[indexV2].Position;
                    var p3 = vertices[indexV3].Position;
                    var t = -(p0 - p1);
                    if (changeToSkySphere)
                        t = -t;
                    t.Normalize();
                    if (flipTangentSign)
                        t = -t;
                    vertices[indexV0].Tangent = t; vertices[indexV1].Tangent = t; vertices[indexV2].Tangent = t; vertices[indexV3].Tangent = t;
                    //
                    // set our indices while were at it.
                    int indexI = iIndex + ((y * (vertexResolution - 1) + x) * 6);
                    int via = indexV0, vib = indexV1, vic = indexV2, vid = indexV3;
                    indices[indexI + 0] = via; indices[indexI + 1] = vib; indices[indexI + 2] = vic;
                    indices[indexI + 3] = vic; indices[indexI + 4] = vib; indices[indexI + 5] = vid;
                }
            }
        }

        // this allows for the use of a single texture skybox.
        void UvSkyTextureReassignment(int faceMultiplier, ref Vector2 uv0, ref Vector2 uv1, ref Vector2 uv2, ref Vector2 uv3)
        {
            if (useSingleImageTexture)
            {
                Vector2 tupeBuvwh = new Vector2(.250000000f, .333333333f); // this is a 8 square left sided skybox
                Vector2 tupeAuvwh = new Vector2(.333333333f, .500000000f); // this is a 6 square blender type skybox
                Vector2 currentuvWH = tupeBuvwh;
                Vector2 uvStart = Vector2.Zero;
                Vector2 uvEnd = Vector2.Zero;

                // crossstyle
                if (blenderStyleElseCross == false)
                {
                    currentuvWH = tupeBuvwh;
                    switch (faceMultiplier)
                    {
                        case FaceFront:
                            uvStart = new Vector2(currentuvWH.X * 1f, currentuvWH.Y * 1f);
                            uvEnd = uvStart + currentuvWH;
                            break;
                        case FaceBack:
                            uvStart = new Vector2(currentuvWH.X * 3f, currentuvWH.Y * 1f);
                            uvEnd = uvStart + currentuvWH;
                            break;
                        case FaceRight:
                            uvStart = new Vector2(currentuvWH.X * 2f, currentuvWH.Y * 1f);
                            uvEnd = uvStart + currentuvWH;
                            break;
                        case FaceLeft:
                            uvStart = new Vector2(currentuvWH.X * 0f, currentuvWH.Y * 1f);
                            uvEnd = uvStart + currentuvWH;
                            break;
                        case FaceTop:
                            uvStart = new Vector2(currentuvWH.X * 1f, currentuvWH.Y * 0f);
                            uvEnd = uvStart + currentuvWH;
                            break;
                        case FaceBottom:
                            uvStart = new Vector2(currentuvWH.X * 1f, currentuvWH.Y * 2f);
                            uvEnd = uvStart + currentuvWH;
                            break;
                    }
                    if (changeToSkySphere)
                    {
                        uv0 = new Vector2(uvEnd.X, uvEnd.Y); uv1 = new Vector2(uvStart.X, uvEnd.Y); uv2 = new Vector2(uvEnd.X, uvStart.Y); uv3 = new Vector2(uvStart.X, uvStart.Y);
                    }
                    else
                    {
                        uv0 = new Vector2(uvStart.X, uvStart.Y); uv1 = new Vector2(uvStart.X, uvEnd.Y); uv2 = new Vector2(uvEnd.X, uvStart.Y); uv3 = new Vector2(uvEnd.X, uvEnd.Y);
                    }
                }
                else
                {
                    currentuvWH = tupeAuvwh;
                    switch (faceMultiplier)
                    {
                        case FaceLeft:
                            uvStart = new Vector2(currentuvWH.X * 0f, currentuvWH.Y * 0f);
                            uvEnd = uvStart + currentuvWH;
                            break;
                        case FaceBack:
                            uvStart = new Vector2(currentuvWH.X * 1f, currentuvWH.Y * 0f);
                            uvEnd = uvStart + currentuvWH;
                            break;
                        case FaceRight:
                            uvStart = new Vector2(currentuvWH.X * 2f, currentuvWH.Y * 0f);
                            uvEnd = uvStart + currentuvWH;
                            break;
                        case FaceBottom:
                            uvStart = new Vector2(currentuvWH.X * 0f, currentuvWH.Y * 1f);
                            uvEnd = uvStart + currentuvWH;
                            break;
                        case FaceTop:
                            uvStart = new Vector2(currentuvWH.X * 1f, currentuvWH.Y * 1f);
                            uvEnd = uvStart + currentuvWH;
                            break;
                        case FaceFront:
                            uvStart = new Vector2(currentuvWH.X * 2f, currentuvWH.Y * 1f);
                            uvEnd = uvStart + currentuvWH;
                            break;
                    }
                    if (changeToSkySphere)
                    {
                        uv0 = new Vector2(uvEnd.X, uvEnd.Y); uv2 = new Vector2(uvEnd.X, uvStart.Y); uv1 = new Vector2(uvStart.X, uvEnd.Y); uv3 = new Vector2(uvStart.X, uvStart.Y);
                    }
                    else
                    {
                        uv0 = new Vector2(uvStart.X, uvStart.Y); uv1 = new Vector2(uvStart.X, uvEnd.Y); uv2 = new Vector2(uvEnd.X, uvStart.Y); uv3 = new Vector2(uvEnd.X, uvEnd.Y);
                    }
                }
            }
        }

        Vector3 InterpolateToNormal(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, float timeX, float timeY)
        {
            var y0 = ((v1 - v0) * timeY + v0);
            var y1 = ((v3 - v2) * timeY + v2);
            var n = ((y1 - y0) * timeX + y0) * 10f; // * 10f ensure its sufficiently denormalized.
            n.Normalize();
            return n;
        }
        Vector2 InterpolateUv(Vector2 v0, Vector2 v1, Vector2 v2, Vector2 v3, float timeX, float timeY)
        {
            var y0 = ((v1 - v0) * timeY + v0);
            var y1 = ((v3 - v2) * timeY + v2);
            return ((y1 - y0) * timeX + y0);
        }

        public void Draw(GraphicsDevice gd, Effect effect)
        {
            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length, indices, 0, (indices.Length / 3), VertexPositionColorNormalTextureTangent.VertexDeclaration);
            }
        }

        /// <summary>
        /// Seperate faced cube or sphere or sky
        /// This method is pretty dependant on being able to pass to textureA not good but....
        /// </summary>
        public void Draw(GraphicsDevice gd, Effect effect, Texture2D front, Texture2D back, Texture2D left, Texture2D right, Texture2D top, Texture2D bottom)
        {
            int FaceFront = 0;
            int FaceBack = 1;
            int FaceLeft = 2;
            int FaceRight = 3;
            int FaceTop = 4;
            int FaceBottom = 5;
            for (int t = 0; t < 6; t++)
            {
                if (t == FaceFront) effect.Parameters["TextureA"].SetValue(front);
                if (t == FaceBack) effect.Parameters["TextureA"].SetValue(back);
                if (t == FaceLeft) effect.Parameters["TextureA"].SetValue(left);
                if (t == FaceRight) effect.Parameters["TextureA"].SetValue(right);
                if (t == FaceTop) effect.Parameters["TextureA"].SetValue(top);
                if (t == FaceBottom) effect.Parameters["TextureA"].SetValue(bottom);
                int ifoffset = t * indicesPerFace;
                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length, indices, ifoffset, primitivesPerFace, VertexPositionColorNormalTextureTangent.VertexDeclaration);
                }
            }
        }

        /// <summary>
        /// Single texture multi faced cube or sphere or sky
        /// This method is pretty dependant on being able to pass to textureA not good but....
        /// </summary>
        public void Draw(GraphicsDevice gd, Effect effect, Texture2D cubeTexture)
        {
            effect.Parameters["TextureA"].SetValue(cubeTexture);
            for (int t = 0; t < 6; t++)
            {
                int ifoffset = t * indicesPerFace;
                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length, indices, ifoffset, primitivesPerFace, VertexPositionColorNormalTextureTangent.VertexDeclaration);
                }
            }
        }

        /// <summary>
        /// This method is pretty dependant on being able to pass to textureA not good but....
        /// </summary>
        public void DrawWithBasicEffect(GraphicsDevice gd, BasicEffect effect, Texture2D front, Texture2D back, Texture2D left, Texture2D right, Texture2D top, Texture2D bottom)
        {
            int FaceFront = 0;
            int FaceBack = 1;
            int FaceLeft = 2;
            int FaceRight = 3;
            int FaceTop = 4;
            int FaceBottom = 5;
            for (int t = 0; t < 6; t++)
            {
                if (t == FaceFront) effect.Texture = front;
                if (t == FaceBack) effect.Texture = back;
                if (t == FaceLeft) effect.Texture = left;
                if (t == FaceRight) effect.Texture = right;
                if (t == FaceTop) effect.Texture = top;
                if (t == FaceBottom) effect.Texture = bottom;
                int ifoffset = t * indicesPerFace;
                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length, indices, ifoffset, primitivesPerFace, VertexPositionColorNormalTextureTangent.VertexDeclaration);
                }
            }
        }

        /// <summary>
        /// Single texture multi faced cube or sphere or sky
        /// This method is pretty dependant on being able to pass to textureA not good but....
        /// </summary>
        public void DrawWithBasicEffect(GraphicsDevice gd, BasicEffect effect, Texture2D cubeTexture)
        {
            effect.Texture = cubeTexture;
            for (int t = 0; t < 6; t++)
            {
                int ifoffset = t * indicesPerFace;
                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length, indices, ifoffset, primitivesPerFace, VertexPositionColorNormalTextureTangent.VertexDeclaration);
                }
            }
        }

        public Vector3 Norm(Vector3 n)
        {
            return Vector3.Normalize(n);
        }

        /// <summary>
        /// Positional cross product, Counter Clock wise positive.
        /// </summary>
        public static Vector3 CrossVectors3d(Vector3 a, Vector3 b, Vector3 c)
        {
            // no point in doing reassignments the calculation is straight forward.
            return new Vector3
                (
                ((b.Y - a.Y) * (c.Z - b.Z)) - ((c.Y - b.Y) * (b.Z - a.Z)),
                ((b.Z - a.Z) * (c.X - b.X)) - ((c.Z - b.Z) * (b.X - a.X)),
                ((b.X - a.X) * (c.Y - b.Y)) - ((c.X - b.X) * (b.Y - a.Y))
                );
        }

        /// <summary>
        /// use the vector3 cross
        /// </summary>
        public static Vector3 CrossXna(Vector3 a, Vector3 b, Vector3 c)
        {
            var v1 = a - b;
            var v2 = c - b;

            return Vector3.Cross(v1, v2);
        }

        //// vertex structure data.
        //public struct VertexPositionColorNormalTextureTangent : IVertexType
        //{
        //    public Vector3 Position;
        //    public Vector4 Color;
        //    public Vector3 Normal;
        //    public Vector2 TextureCoordinate;
        //    public Vector3 Tangent;

        //    public static VertexDeclaration VertexDeclaration = new VertexDeclaration
        //    (
        //          new VertexElement(VertexElementByteOffset.PositionStartOffset(), VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        //          new VertexElement(VertexElementByteOffset.OffsetVector4(), VertexElementFormat.Vector4, VertexElementUsage.Color, 0),
        //          new VertexElement(VertexElementByteOffset.OffsetVector3(), VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        //          new VertexElement(VertexElementByteOffset.OffsetVector2(), VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        //          new VertexElement(VertexElementByteOffset.OffsetVector3(), VertexElementFormat.Vector3, VertexElementUsage.Normal, 1)
        //    );
        //    VertexDeclaration IVertexType.VertexDeclaration { get { return VertexDeclaration; } }
        //}
        ///// <summary>
        ///// This is a helper struct for tallying byte offsets
        ///// </summary>
        //public struct VertexElementByteOffset
        //{
        //    public static int currentByteSize = 0;
        //    [STAThread]
        //    public static int PositionStartOffset() { currentByteSize = 0; var s = sizeof(float) * 3; currentByteSize += s; return currentByteSize - s; }
        //    public static int Offset(float n) { var s = sizeof(float); currentByteSize += s; return currentByteSize - s; }
        //    public static int Offset(Vector2 n) { var s = sizeof(float) * 2; currentByteSize += s; return currentByteSize - s; }
        //    public static int Offset(Color n) { var s = sizeof(int); currentByteSize += s; return currentByteSize - s; }
        //    public static int Offset(Vector3 n) { var s = sizeof(float) * 3; currentByteSize += s; return currentByteSize - s; }
        //    public static int Offset(Vector4 n) { var s = sizeof(float) * 4; currentByteSize += s; return currentByteSize - s; }

        //    public static int OffsetFloat() { var s = sizeof(float); currentByteSize += s; return currentByteSize - s; }
        //    public static int OffsetColor() { var s = sizeof(int); currentByteSize += s; return currentByteSize - s; }
        //    public static int OffsetVector2() { var s = sizeof(float) * 2; currentByteSize += s; return currentByteSize - s; }
        //    public static int OffsetVector3() { var s = sizeof(float) * 3; currentByteSize += s; return currentByteSize - s; }
        //    public static int OffsetVector4() { var s = sizeof(float) * 4; currentByteSize += s; return currentByteSize - s; }
        //}
    }


    // vertex structure data.
    public struct VertexPositionColorNormalTextureTangent : IVertexType
    {
        public Vector3 Position;
        public Vector4 Color;
        public Vector3 Normal;
        public Vector2 TextureCoordinate;
        public Vector3 Tangent;

        public static VertexDeclaration VertexDeclaration = new VertexDeclaration
        (
              new VertexElement(VertexElementByteOffset.PositionStartOffset(), VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
              new VertexElement(VertexElementByteOffset.OffsetVector4(), VertexElementFormat.Vector4, VertexElementUsage.Color, 0),
              new VertexElement(VertexElementByteOffset.OffsetVector3(), VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
              new VertexElement(VertexElementByteOffset.OffsetVector2(), VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
              new VertexElement(VertexElementByteOffset.OffsetVector3(), VertexElementFormat.Vector3, VertexElementUsage.Normal, 1)
        );
        VertexDeclaration IVertexType.VertexDeclaration { get { return VertexDeclaration; } }
    }
    public struct VertexPositionColorNormalTexture : IVertexType
    {
        public Vector3 Position;
        public Vector4 Color;
        public Vector3 Normal;
        public Vector2 TextureCoordinate;

        public static VertexDeclaration VertexDeclaration = new VertexDeclaration
        (
              new VertexElement(VertexElementByteOffset.PositionStartOffset(), VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
              new VertexElement(VertexElementByteOffset.OffsetVector4(), VertexElementFormat.Vector4, VertexElementUsage.Color, 0),
              new VertexElement(VertexElementByteOffset.OffsetVector3(), VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
              new VertexElement(VertexElementByteOffset.OffsetVector2(), VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0)
        );
        VertexDeclaration IVertexType.VertexDeclaration { get { return VertexDeclaration; } }
    }

}
