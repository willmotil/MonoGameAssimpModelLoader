using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

/*Read Me's  ...Various Controls...  Keyboard camera controls q thru c are all look command keys   q and e are forward and back the left right up down arrows are strafe movement.
   This camera class is pretty wonky i sort rushed the controls setup and mucked up how i thought it should work the mouse right click can control the camera but its wonky.
 
    Press R to run a animation F to stop it and Space Bar to step thru each frame.
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

        RiggedModel model;
        Effect riggedEffect; // im using my own rigged model effect here.
        Texture2D defaultTexture;
        Texture2D orientationArrows;

        // unrelated stuff to help

        public Basic3dExampleCamera cam;

        // a variable that tracks the rotation of the light.
        float lightrot = 0f;
        // a variable that defines the initial position of the light.
        Vector3 LightPosition = new Vector3(0f, 10f, 500f);

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
            defaultTexture = Content.Load<Texture2D>("CheckerBoardTemplateImage");
            orientationArrows = Content.Load<Texture2D>("OrientationImage_PartiallyTransparent");

            LoadUpDefaultCamera();

            LoadEffects();

            bigShere = new SpherePNTT(false, 12, 25f, false, true);
            littleSphere = new SpherePNTT(false, 12, 5f, true);

            // prep model reader.
            RiggedModelLoader modelReader = new RiggedModelLoader(Content, riggedEffect);
            RiggedModelLoader.DefaultTexture = defaultTexture;
            model = modelReader.LoadAsset("dude.fbx", 24);
            //model = modelReader.LoadAsset("PipeFromCube15VGrpsLCRetexAnimKeyframesMovedUp.fbx", 24); // damn its hard to find models that work.
            //model = modelReader.LoadAsset("Victoria-hat-dance.FBX", 24);
            //model = modelReader.LoadAsset("Futuristic combat jet.fbx", 24);
            //model = modelReader.LoadAsset("astroBoy_walk_Max.dae", 24);

            modelVisualNormals = new ModelsVisualNormals(model, orientationArrows, 1f, 1f);
            primitiveNormalArrows = new NormalArrow(bigShere.vertices, bigShere.indices, orientationArrows, 1f);

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

            riggedEffect.Parameters["World"].SetValue(Matrix.Identity);
            riggedEffect.Parameters["View"].SetValue(cam.View);
            riggedEffect.Parameters["Projection"].SetValue(cam.Projection);
            riggedEffect.Parameters["TextureA"].SetValue(defaultTexture);
            riggedEffect.Parameters["WorldLightPosition"].SetValue(LightPosition);
            // set up the effect initially to change how you want the shader to behave.
            riggedEffect.Parameters["AmbientAmt"].SetValue(.1f);
            riggedEffect.Parameters["DiffuseAmt"].SetValue(.7f);
            riggedEffect.Parameters["SpecularAmt"].SetValue(.5f);
            riggedEffect.Parameters["SpecularSharpness"].SetValue(.68f);
            riggedEffect.Parameters["SpecularLightVsTexelInfluence"].SetValue(.50f);
            riggedEffect.Parameters["LightColor"].SetValue( new Vector4(.999f,.999f, .999f, 1.0f) );

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
            if (Keyboard.GetState().IsKeyDown(Keys.R) )
            {
                model.BeginAnimation(0, gameTime);
                model.overrideFrame = -1;
            }

            // next animation
            if (Keyboard.GetState().IsKeyDown(Keys.N))
            {
                model.CurrentRunAnimation = model.CurrentRunAnimation++;
                model.overrideFrame = -1;
            }

            // stop animation
            if (Keyboard.GetState().IsKeyDown(Keys.F) )
                model.StopAnimation();

            // override on and set override animation
            if (Keyboard.GetState().IsKeyDown(Keys.Space))
                model.overrideFrame++;

            // WireframeOnOrOff
            if (Keyboard.GetState().IsKeyDown(Keys.F5))
                WireframeOnOrOff = ! WireframeOnOrOff;

            // ShowNormals
            // WireframeOnOrOff
            if (Keyboard.GetState().IsKeyDown(Keys.F6))
                ShowNormals = !ShowNormals;

            lightrot += (float)gameTime.ElapsedGameTime.TotalSeconds * .25f;  //slow down the light rotation.
            if (lightrot > 6.2831f)
                lightrot = 0;

            cam.Update(gameTime);

            if(model != null)
                model.Update(gameTime);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black); 
            GraphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Black, 1f, 1);
            GraphicsDevice.SamplerStates[0] = ss_standard;
            GraphicsDevice.DepthStencilState = ds_standard_less;
            if(WireframeOnOrOff)
                GraphicsDevice.RasterizerState = rs_nocullwireframe;
            else
                GraphicsDevice.RasterizerState = rs_cull_cw_solid;

            // rotate light
            Matrix lightTransform = Matrix.CreateRotationY(lightrot);
            var light = Vector3.Transform(LightPosition, lightTransform);

            // model and normals

            riggedEffect.Parameters["World"].SetValue(Matrix.Identity);
            riggedEffect.Parameters["View"].SetValue(cam.View);
            riggedEffect.Parameters["CameraPosition"].SetValue(cam.Position); // cam.Position // cam.View.Translation
            riggedEffect.Parameters["WorldLightPosition"].SetValue(light);

            // draw a line to the light source.

            riggedEffect.Parameters["TextureA"].SetValue(defaultTexture);
            lineToLight = new LinePCT(.1f, Color.White, Vector3.Zero, light);
            lineToLight.Draw(GraphicsDevice, riggedEffect);

            // draw models.

            if (model != null)
            {
                riggedEffect.CurrentTechnique = riggedEffect.Techniques["RiggedModelDraw"];
                model.effect = riggedEffect;
                model.Draw(GraphicsDevice);
            }

            // draw models normals.

            riggedEffect.CurrentTechnique = riggedEffect.Techniques["RiggedModelNormalDraw"];
            GraphicsDevice.RasterizerState = rs_nocullsolid;

            if (ShowNormals)
                modelVisualNormals.Draw(GraphicsDevice, riggedEffect);

            // move the spheres down a bit first
            var m = Matrix.Identity; m.Translation = new Vector3(0, -20f, 0f);
            riggedEffect.Parameters["World"].SetValue(m);

            // ok this is maddening... as far as i can tell the bones are somehow performing a complete vertice normal inversion or reflection in 3d.
            // so these model normals have to be inverted in the shader for the lighting calculations which is super confusing.

            //// draw sphere normals
            //if (ShowNormals)
            //    primitiveNormalArrows.Draw(GraphicsDevice, riggedEffect);

            // draw sphere.

            riggedEffect.CurrentTechnique = riggedEffect.Techniques["ColorTextureLightingDraw"];
            riggedEffect.Parameters["TextureA"].SetValue(defaultTexture);

            littleSphere.Draw(GraphicsDevice, riggedEffect);

            bigShere.Draw(GraphicsDevice, riggedEffect);

            // draw sphere normals
            if (ShowNormals)
                primitiveNormalArrows.Draw(GraphicsDevice, riggedEffect);

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

}
