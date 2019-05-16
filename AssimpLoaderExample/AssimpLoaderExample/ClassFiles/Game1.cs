using System;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

/*Read Me's  ...Various Controls...  Keyboard camera controls q thru c are all look command keys   q and e are forward and back the left right up down arrows are strafe movement.
   This camera class is pretty wonky i sort rushed the controls setup and mucked up how i thought it should work the mouse right click can control the camera but its wonky.
 
    Press R to run a animation F to stop it  N next frame O for next matrix node to display. and Space Bar to step thru each frame.
    F5 for wireframe F6 to view the models normals.
     */

/* Read the below notes for the basics on adding / using assimp directly in monogame via visual studio nuget.net.
 */

/* Add the Assimp.net nuget to your solution thru the packet manager in visual stuido.
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
*/

/* Adding fbx files.
// Add the .Fbx models to the Assets Folder that you want to load.
// If there is no Assets folder right click on the project in the solution explorer an add a new folder by that name.
// Important...
// Select each of the .fbx files you add to that asset folder and by (right clicking on them) 
// Select the "Properties" at the bottom of the list that is presented. 
// Then in the below properties box that opens select the option tiltled "Copy To Output Directory" 
// Select -> "copy if newer" in that drop down shown to the right.
// Leave the build action set to none.
//
// Add the textures for the model to the content pipeline as you would any other images.
*/

/* Optionally you make want to turn on the console for the project to see extra information.
// Do that in visual studio by selecting 
// Debug -> properties at the bottom -> Application on the left -> OutputType drop down select Console.
*/

namespace AssimpLoaderExample
{
 
    public class Game1 : Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        SpriteFont currentFont;
        StringBuilder currentMsg = new StringBuilder();

        RiggedModel model;
        Effect riggedEffect; // im using my own rigged model effect here.
        Texture2D defaultTexture;
        Texture2D orientationArrows;

        // unrelated stuff to help

        public Basic3dExampleCamera cam;

        // a variable that tracks the rotation of the light.
        float lightrot = 0f;
        // a variable that defines the initial position of the light.
        Vector3 initialLightPosition = new Vector3(0f, 0f, 1200f);
        // the light transform
        Matrix lightTransform = Matrix.Identity;

        // pimitives to help see were the light is and what its effects are on a smooth surface.
        SpherePNTT bigShere;
        SpherePNTT littleSphere;
        // a simple line to show were the light position is in our 3d world.
        LinePCT lineToLight;
        // some normal visualization primitive models to ensure the models normals have been loaded correctly as they are very important.
        ModelsVisualNormals modelVisualNormals;
        NormalArrow primitiveNormalArrows;

        bool WireframeOnOrOff = false;
        bool ShowNormals = false;

        public SamplerState ss_point = new SamplerState() { Filter = TextureFilter.Point };
        public SamplerState ss_point_clamp = new SamplerState() { Filter = TextureFilter.Point, AddressU = TextureAddressMode.Clamp, AddressV = TextureAddressMode.Clamp };
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
            defaultTexture = Content.Load<Texture2D>("CheckerBoardTemplateImage");
            orientationArrows = Content.Load<Texture2D>("OrientationImage_PartiallyTransparent");

            LoadUpDefaultCamera();

            LoadEffects();

            bigShere = new SpherePNTT(false, 12, 25f, false, true);
            littleSphere = new SpherePNTT(false, 12, 5f, true);
            lineToLight = new LinePCT(.1f, Color.White, Vector3.Zero, initialLightPosition);

            // prep model reader.
            RiggedModelLoader modelReader = new RiggedModelLoader(Content, riggedEffect);
            RiggedModelLoader.DefaultTexture = defaultTexture;
            modelReader.AddAdditionalLoopingTime = true;
            modelReader.AddedLoopingDuration = .1f;
            //
            //model = modelReader.LoadAsset("dude.fbx", 24);
            //model = modelReader.LoadAsset("new_thin_zombie.fbx", 24); 
            //model = modelReader.LoadAsset("AnimatedCube5.fbx", 24);
            model = modelReader.LoadAsset("PipeFromCube16VGrpsLCRetexAnimKeyframesMovedUp.fbx", 24); // damn its hard to find models that work.
            
            modelVisualNormals = new ModelsVisualNormals(model, orientationArrows, .5f, 1f);
            primitiveNormalArrows = new NormalArrow(bigShere.vertices, bigShere.indices, orientationArrows, 1f);
        }
        public void LoadUpDefaultCamera()
        {
            // i really need to shore up my camera classes into one and fix them.
            cam = new Basic3dExampleCamera(GraphicsDevice, this.Window, false);
            cam.FieldOfViewDegrees = 85;
            cam.MovementUnitsPerSecond = 40f;
            cam.CameraUi(Basic3dExampleCamera.CAM_TYPE_OPTION_FREE);
            cam.CameraType(Basic3dExampleCamera.CAM_UI_OPTION_EDIT_LAYOUT);
            cam.Position = new Vector3(2, 33, 402);
            cam.LookAtTargetPosition = Vector3.Zero - cam.Position;
        }
        public void LoadEffects()
        {
            // the debug version to see a single bone.
            //riggedEffect = Content.Load<Effect>("RiggedModelEffect");
            //riggedEffect.CurrentTechnique = riggedEffect.Techniques["SkinedDebugModelDraw"];
            //riggedEffect.Parameters["boneIdToSee"].SetValue(0f);

            riggedEffect = Content.Load<Effect>("RiggedModelEffect");
            riggedEffect.CurrentTechnique = riggedEffect.Techniques["RiggedModelDraw"];

            riggedEffect.Parameters["World"].SetValue(Matrix.Identity);
            riggedEffect.Parameters["View"].SetValue(cam.View);
            riggedEffect.Parameters["Projection"].SetValue(cam.Projection);
            riggedEffect.Parameters["TextureA"].SetValue(defaultTexture);
            riggedEffect.Parameters["WorldLightPosition"].SetValue(initialLightPosition);
            // set up the effect initially to change how you want the shader to behave.
            riggedEffect.Parameters["AmbientAmt"].SetValue(.15f);
            riggedEffect.Parameters["DiffuseAmt"].SetValue(.6f);
            riggedEffect.Parameters["SpecularAmt"].SetValue(.25f);
            riggedEffect.Parameters["SpecularSharpness"].SetValue(.88f);
            riggedEffect.Parameters["SpecularLightVsTexelInfluence"].SetValue(.40f);
            riggedEffect.Parameters["LightColor"].SetValue( new Vector4(.099f,.099f, .999f, 1.0f) );

        }

        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            // run the animation
            if (Keyboard.GetState().IsKeyDown(Keys.R) && Pause(gameTime))
            {
                model.BeginAnimation(0, gameTime);
                model.overrideAnimationFrameTime = -1;
            }

            // next animation
            if (Keyboard.GetState().IsKeyDown(Keys.N) && Pause(gameTime))
            {
                model.CurrentPlayingAnimationIndex = model.CurrentPlayingAnimationIndex +1;
                if (model.CurrentPlayingAnimationIndex >= model.originalAnimations.Count)
                    model.CurrentPlayingAnimationIndex = 0;
                model.overrideAnimationFrameTime = -1;
            }

            // stop animation
            if (Keyboard.GetState().IsKeyDown(Keys.F) && Pause(gameTime))
                model.StopAnimation();

            // change interpolation type animation
            if (Keyboard.GetState().IsKeyDown(Keys.U) && Pause(gameTime))
                model.UseStaticGeneratedFrames = true;
            if (Keyboard.GetState().IsKeyDown(Keys.I) && Pause(gameTime))
                model.UseStaticGeneratedFrames = false;

            // override on and set override animation
            if (Keyboard.GetState().IsKeyDown(Keys.Space))
                model.overrideAnimationFrameTime+= (float)gameTime.ElapsedGameTime.TotalSeconds * .1f;

            // WireframeOnOrOff
            if (Keyboard.GetState().IsKeyDown(Keys.F5) && Pause(gameTime))
                WireframeOnOrOff = ! WireframeOnOrOff;

            // ShowNormals
            // WireframeOnOrOff
            if (Keyboard.GetState().IsKeyDown(Keys.F6) && Pause(gameTime))
                ShowNormals = !ShowNormals;

            if (Keyboard.GetState().IsKeyDown(Keys.O) && Pause(gameTime))
            {
                nodeToShow++;
                if(nodeToShow >= model.flatListToAllNodes.Count)
                {
                    nodeToShow = 0;
                }
            }

            // z
            if (Keyboard.GetState().IsKeyDown(Keys.NumPad1) && Pause(gameTime))
            {
                cam.Position = new Vector3(0f, 0f, 100f);
                cam.Up = Vector3.Up;
                cam.LookAtTargetPosition = new Vector3(0f, 0f, 0f);
            }
            if (Keyboard.GetState().IsKeyDown(Keys.NumPad2) && Pause(gameTime))
            {
                cam.Position = new Vector3(0f, 0f, 400f);
                cam.Up = Vector3.Up;
                cam.LookAtTargetPosition = new Vector3(0f, 0f, 0f);
            }
            if (Keyboard.GetState().IsKeyDown(Keys.NumPad3) && Pause(gameTime))
            {
                cam.Position = new Vector3(0f, 0f, 900f);
                cam.Up = Vector3.Up;
                cam.LookAtTargetPosition = new Vector3(0f, 0f, 0f);
            }
            // x
            if (Keyboard.GetState().IsKeyDown(Keys.NumPad7) && Pause(gameTime))
            {
                cam.Position = new Vector3(100f, 0f, 0f);
                cam.Up = Vector3.Up;
                cam.LookAtTargetPosition = new Vector3(0f, 0f, 0f);
            }
            if (Keyboard.GetState().IsKeyDown(Keys.NumPad8) && Pause(gameTime))
            {
                cam.Position = new Vector3(400f, 0f, 0f);
                cam.Up = Vector3.Up;
                cam.LookAtTargetPosition = new Vector3(0f, 0f, 0f);
            }
            if (Keyboard.GetState().IsKeyDown(Keys.NumPad9) && Pause(gameTime))
            {
                cam.Position = new Vector3(900f, 0f, 0f);
                cam.Up = Vector3.Up;
                cam.LookAtTargetPosition = new Vector3(0f, 0f, 0f);
            }
            // y
            if (Keyboard.GetState().IsKeyDown(Keys.NumPad4) && Pause(gameTime))
            {
                cam.Position = new Vector3(0f, 100f, 1f);
                cam.Up = Vector3.Cross(cam.World.Forward, cam.World.Right);
                cam.LookAtTargetPosition = new Vector3(0f, 0f, 0f);
            }
            if (Keyboard.GetState().IsKeyDown(Keys.NumPad5) && Pause(gameTime))
            {
                cam.Position = new Vector3(0f, 400f, 1f);
                cam.Up = Vector3.Cross(cam.World.Forward, cam.World.Right);
                cam.LookAtTargetPosition = new Vector3(0f, 0f, 0f);
            }
            if (Keyboard.GetState().IsKeyDown(Keys.NumPad6) && Pause(gameTime))
            {
                cam.Position = new Vector3(0f, 900f, 1f);
                cam.Up = Vector3.Cross(cam.World.Forward, cam.World.Right);
                cam.LookAtTargetPosition = new Vector3(0f, 0f, 0f);
            }

            lightrot += (float)gameTime.ElapsedGameTime.TotalSeconds * .25f;  //slow down the light rotation.
            if (lightrot > 6.2831f)
                lightrot = 0;
            // light transform matrix.
            lightTransform = Matrix.CreateRotationY(lightrot);

            cam.Update(gameTime);

            if(model != null)
                model.Update(gameTime);

            base.Update(gameTime);
        }



        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black); 
            GraphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Black, 1f, 1);
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.SamplerStates[0] = ss_point_clamp;
            GraphicsDevice.DepthStencilState = ds_standard_less;
            if(WireframeOnOrOff)
                GraphicsDevice.RasterizerState = rs_nocullwireframe;
            else
                GraphicsDevice.RasterizerState = rs_cull_cw_solid;

            // set model world
            var modelWorld = Matrix.Identity;
            //modelWorld = Matrix.CreateRotationY(1.5f);
            //modelWorld.Translation = new Vector3(5f, -0f, 0f);
            // set sphere orientation
            var sphereOrientation = Matrix.Identity * lightTransform;
            sphereOrientation.Translation = new Vector3(0, -50f, 0f);
            // light pos
            var lightpos = Vector3.Transform(initialLightPosition, lightTransform);
            // set light to shader
            riggedEffect.Parameters["WorldLightPosition"].SetValue(lightpos);
            // set view to shader
            riggedEffect.Parameters["View"].SetValue(cam.View);
            // set camera world position to shader
            riggedEffect.Parameters["CameraPosition"].SetValue(cam.Position); // cam.Position // cam.View.Translation
            // remake the line
            lineToLight = new LinePCT(.1f, Color.White, sphereOrientation.Translation, lightpos);

            //_______________________________
            // draw a line to the light source.
            //_______________________________

            riggedEffect.CurrentTechnique = riggedEffect.Techniques["ColorDraw"];
            riggedEffect.Parameters["World"].SetValue(Matrix.Identity);
            riggedEffect.Parameters["TextureA"].SetValue(defaultTexture);

            lineToLight.Draw(GraphicsDevice, riggedEffect);

            //_______________________________
            // draw sphere.
            //_______________________________

            // move the spheres down a bit first
            riggedEffect.Parameters["World"].SetValue(sphereOrientation);

            riggedEffect.CurrentTechnique = riggedEffect.Techniques["ColorTextureLightingDraw"];

            littleSphere.Draw(GraphicsDevice, riggedEffect);
            bigShere.Draw(GraphicsDevice, riggedEffect);

            // draw sphere normals
            riggedEffect.CurrentTechnique = riggedEffect.Techniques["ColorTextureLightingNormalsDraw"];
            GraphicsDevice.RasterizerState = rs_nocullsolid;
            if (ShowNormals)
                primitiveNormalArrows.Draw(GraphicsDevice, riggedEffect);

            if (WireframeOnOrOff)
                GraphicsDevice.RasterizerState = rs_nocullwireframe;
            else
                GraphicsDevice.RasterizerState = rs_cull_cw_solid;

            //_______________________________
            // model and normals
            //_______________________________


            // draw a little sphere in the model just make sure the model isnt backface.
            var m = Matrix.Identity;
            m.Translation = new Vector3(0, 20, 0);
            riggedEffect.Parameters["World"].SetValue(m);
            littleSphere.Draw(GraphicsDevice, riggedEffect);

            // draw models.

            if (model != null)
            {
                riggedEffect.CurrentTechnique = riggedEffect.Techniques["RiggedModelDraw"];
                modelWorld = Matrix.Identity;
                riggedEffect.Parameters["World"].SetValue(modelWorld);
                model.effect = riggedEffect;
                model.Draw(GraphicsDevice, modelWorld);
            }

            // draw models normals.

            riggedEffect.CurrentTechnique = riggedEffect.Techniques["RiggedModelNormalDraw"];
            GraphicsDevice.RasterizerState = rs_nocullsolid;
            if (ShowNormals)
                modelVisualNormals.Draw(GraphicsDevice, riggedEffect);


            DrawSpriteBatches(gameTime);
            base.Draw(gameTime);
        }

        int bonenodetoshow = 1000;
        int nodeToShow = 0;
        protected void DrawSpriteBatches(GameTime gameTime)
        {
            // ok so now i gotta figure out out to specify if a mesh is animated by bones or not.
            currentMsg.Clear();
            currentMsg.Append(" Camera: ").Append(cam.Position.ToStringTrimed());
            currentMsg.Append("\n Forward: ").Append(cam.Forward.ToStringTrimed()).Append(" Up: ").Append(cam.Up.ToStringTrimed());
            currentMsg.Append("\n Current Animation [").Append(model.CurrentPlayingAnimationIndex).Append("] of "+ model.originalAnimations.Count + "   Name: ").Append(model.originalAnimations[model.CurrentPlayingAnimationIndex].animationName);
            currentMsg.Append("\n Current Frame: ").Append(model.currentFrame.ToStringTrimed()).Append(" / Total Frames: ").Append(model.originalAnimations[model.CurrentPlayingAnimationIndex].TotalFrames);
            currentMsg.Append("\n Current AnimationTime: ").Append(model.currentAnimationFrameTime.ToStringTrimed());
            currentMsg.Append(" Total Animation Durration: ").Append(model.originalAnimations[model.CurrentPlayingAnimationIndex].DurationInSeconds.ToStringTrimed());
            
            if (model.flatListToBoneNodes.Count > bonenodetoshow)
            {
                currentMsg.Append("\n\n Flat BoneId[" + bonenodetoshow + "] of "+ model.flatListToBoneNodes.Count + "  Name: " + model.flatListToBoneNodes[bonenodetoshow].name + " ");
                currentMsg.Append(model.flatListToBoneNodes[bonenodetoshow].LocalTransformMg.SrtInfoToString(""));
            }
            
            if (model.flatListToAllNodes.Count > nodeToShow)
            {
                currentMsg.Append("\n\n Flat NodeId[" + nodeToShow + "] of " + model.flatListToAllNodes.Count + " Name: " + model.flatListToAllNodes[nodeToShow].name + " ");
                currentMsg.Append(model.flatListToAllNodes[nodeToShow].LocalTransformMg.SrtInfoToString(""));
                //currentMsg.Append(model.flatListToAllNodes[nodeToShow].CombinedTransformMg.SrtInfoToString(""));
            }

            spriteBatch.Begin(SpriteSortMode.Immediate);
            spriteBatch.DrawString(currentFont, currentMsg, new Vector2(10, 10), Color.Wheat);
            spriteBatch.End();
        }


        float pause = 0f;
        bool Pause(GameTime gametime)
        {
            if (pause < 0)
            {
                pause = .5f;
                return true;
            }
            else
            {
                pause -= (float)gametime.ElapsedGameTime.TotalSeconds;
                return false;
            }
        }

    }

}
