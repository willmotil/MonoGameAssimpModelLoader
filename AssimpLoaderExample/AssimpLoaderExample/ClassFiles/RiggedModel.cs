using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
//using Microsoft.Xna.Framework.Input;

//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using System.Diagnostics;


/*  TODO 
//  lots to do still and think about.
// Fix the amimations there is a small discrepancy between my animations and visual studios model viewer which also isn't perfect it is irking me.
// link the textures to the shader in the model 
//  a) change the texture for diffuse to a list so a model can allow for multi texturing i dunno how many or if any models actually use this.
//     ... however assimp holds texture arrays for diffuse uv textures and others so it seems to be something that exists ill have to go back and change that.
//  b) add in the normal mapping to the shader. 
//  c) add in a normal map generator algorithm, this would be nice.
//  d) add in heightmapping humm i never actually got around to writing one yet so that will need a seperate test project with a proof of concept effect.
//
// Other kinds of animations need to be added but since these are primarily world space transforms that will be quite a bit easier.
// I just need to decide how to handle it, could even just use the bone 0 transform for that which i have added as a dummy node.
//
// Deformer animations also need to be added i need to do a little more research before i add that 
// Althought the principal is simple.
// I have no idea how assimp intends these deformations to be used as mesh vertice deforms ir bone deforms for example ect...
// 
// Improve the draw method so it draws itself better improve the effect file so i can test the above stuff.
// I suppose i should read things like the specular and diffuse values ect... 
// but this is minor most people will use there own effect pixel shaders anyways.
// Maybe stick the effect into the model.
//
// There is also the notion of were these steps belong some maybe belong in the loader some maybe the model.
// there is also the question of do i want a seperate temporary model in the loader and then a seperate xnb written and read model later on.
// there is also the question of is that even smart at this point and maybe should it just be bypassed altogether and the files written in csv xml or as dats.
//
 */

/// <summary>
/// 
/// </summary>
namespace AssimpLoaderExample
{
    /// <summary>
    /// The rigged model  i stuffed the classes the rigged model uses in it as nested classes just to keep it all together.
    /// I don't really see anything else using these classes anyways they are really just specific to the model.
    /// Even the loader should go out of scope when its done loading a model and then even it is just going to be a conversion tool.
    /// After i make a content reader and writer for the model class there will be no need for the loader except to change over new models.
    /// However you don't really have to have it in xnb form at all you could use it as is but it does a lot of processing so... meh...
    /// </summary>
    public class RiggedModel
    {
        #region members

        public bool consoleDebug = true;

        public Effect effect;
        public int numberOfBonesInUse = 0;
        public int maxGlobalBones = 128; // 78
        public List<RiggedModelNode> flatListToBoneNodes = new List<RiggedModelNode>();
        public Matrix[] globalShaderMatrixs; // these must be converted to mg type matrices before sending them to the shader i think.
        public RiggedModelMesh[] meshes;
        public RiggedModelNode rootNodeOfTree; // the actual scene root node the basis of the model from here we can locate any node in the chain.
        public RiggedModelNode firstRealBoneInTree; // the actual first bone in the scene the basis of the users skeletal model he created.
        public RiggedModelNode globalPreTransformNode; // the accumulated orientations and scalars prior to the first bone acts as a scalar to the actual bone local transforms from assimp.
        public Matrix meshTransform = Matrix.Identity; // dunno what this is for to be honest.

        // initial assimp animations
        public List<RiggedAnimation> origAnim = new List<RiggedAnimation>();
        int currentAnimation = 0;
        public int currentFrame = 0;
        public bool animationRunning = false;
        bool loopAnimation = true;
        float timeStart = 0f;

        // mainly for testing to step thru each frame.
        public int overrideFrame = -1;

        #endregion

        #region methods

        /// <summary>
        /// Instantiates the model object and the boneShaderFinalMatrix array setting them all to identity.
        /// </summary>
        public RiggedModel()
        {
            globalShaderMatrixs = new Matrix[maxGlobalBones];
            for (int i = 0; i < maxGlobalBones; i++)
            {
                globalShaderMatrixs[i] = Matrix.Identity;
            }
        }

        /// <summary>
        /// As stated
        /// </summary>
        public void SetEffect(Effect effect, Texture2D t, Matrix world, Matrix view, Matrix projection)
        {
            this.effect = effect;
            //texture = t;
            this.effect.Parameters["TextureA"].SetValue(t);
            this.effect.Parameters["World"].SetValue(world);
            this.effect.Parameters["View"].SetValue(view);
            this.effect.Parameters["Projection"].SetValue(projection);
            this.effect.Parameters["Bones"].SetValue(globalShaderMatrixs);
        }

        /// <summary>
        /// As stated
        /// </summary>
        public void SetEffectTexture(Texture2D t)
        {
            this.effect.Parameters["TextureA"].SetValue(t);
        }

        /// <summary>
        /// Convienience method pass the node let it set itself.
        /// This also allows you to call this is in a iterated node tree and just bypass setting non bone nodes.
        /// </summary>
        public void SetGlobalShaderBoneNode(RiggedModelNode n)
        {
            if (n.isThisARealBone)
                globalShaderMatrixs[n.boneShaderFinalTransformIndex] = n.CombinedTransformMg;
        }

        /// <summary>
        /// Update
        /// </summary>
        public void Update(GameTime gameTime)
        {
            if (animationRunning)
                UpdateModelAnimations(gameTime);
            IterateUpdate(rootNodeOfTree);
        }

        /// <summary>
        /// Gets the animation frame corresponding to the elapsed time for all the nodes and loads them into the model node transforms.
        /// </summary>
        public void UpdateModelAnimations(GameTime gameTime)
        {
            if (origAnim.Count > 0 && currentAnimation < origAnim.Count)
            {
                float currentRunning = (float)(gameTime.TotalGameTime.TotalSeconds) - timeStart;
                currentFrame = (int)(currentRunning / origAnim[currentAnimation].SecondsPerFrame);
                int numbOfFrames = origAnim[currentAnimation].TotalFrames;

                // just for seeing a single frame lets us override the current frame.
                if (overrideFrame > -1)
                {
                    currentFrame = overrideFrame;
                    if (overrideFrame > numbOfFrames)
                        overrideFrame = 0;
                }

                // restart the animation.
                if (loopAnimation && timeStart != 0 && currentFrame >= numbOfFrames)
                {
                    currentFrame = 0;
                    timeStart = (float)(gameTime.TotalGameTime.TotalSeconds);
                }

                // set the local node transforms from the frame.
                if (currentFrame < numbOfFrames)
                {
                    int nodeCount = origAnim[currentAnimation].animatedNodes.Count;
                    for (int nodeLooped = 0; nodeLooped < nodeCount; nodeLooped++)
                    {
                        var animNodeframe = origAnim[currentAnimation].animatedNodes[nodeLooped];
                        var node = animNodeframe.nodeRef;
                        node.LocalTransformMg = animNodeframe.frameOrientations[currentFrame];
                    }
                }
            }
        }

        /// <summary>
        /// Updates the node transforms
        /// </summary>
        private void IterateUpdate(RiggedModelNode node)
        {
            if (node.parent != null)
                node.CombinedTransformMg = node.LocalTransformMg * node.parent.CombinedTransformMg;
            else
                node.CombinedTransformMg = node.LocalTransformMg;
            // set to the final shader matrix.
            if (node.isThisARealBone)
                globalShaderMatrixs[node.boneShaderFinalTransformIndex] = node.OffsetMatrixMg * node.CombinedTransformMg;
            // call children
            foreach (RiggedModelNode n in node.children)
                IterateUpdate(n);
        }

        /// <summary>
        /// Sets the global final bone matrices to the shader and draws it.
        /// </summary>
        public void Draw(GraphicsDevice gd)
        {
            effect.Parameters["Bones"].SetValue(globalShaderMatrixs);
            foreach (RiggedModelMesh m in meshes)
            {
                if (m.texture != null)
                    effect.Parameters["TextureA"].SetValue(m.texture);
                var e = this.effect.CurrentTechnique;
                e.Passes[0].Apply();
                gd.DrawUserIndexedPrimitives(Microsoft.Xna.Framework.Graphics.PrimitiveType.TriangleList, m.vertices, 0, m.vertices.Length, m.indices, 0, m.indices.Length / 3, VertexPositionTextureNormalTangentWeights.VertexDeclaration);
            }
        }

        #endregion

        #region Region animation stuff

        public int CurrentRunAnimation
        {
            get { return currentAnimation; }
            set {
                var n = value;
                if (n >= origAnim.Count)
                    n = origAnim.Count - 1;
                currentAnimation = n;
            }
        }

        /// <summary>
        /// This takes the original assimp animations and calculates a complete steady orientation matrix per frame for the fps of the animation duration.
        /// </summary>
        public void CreateAnimationFrames(int fps)
        {
            foreach (var anim in origAnim)
                anim.SetAnimationFpsCreateFrames(fps, this);
        }

        public void BeginAnimation(int animationIndex, GameTime gametime)
        {
            timeStart = (float)gametime.TotalGameTime.TotalSeconds;
            currentAnimation = animationIndex;
            animationRunning = true;
        }

        public void StopAnimation()
        {
            animationRunning = false;
        }

        #endregion

        #region Region not sure these mesh and global transforms are really necessary.

        /// <summary>
        /// This is all the local transforms combined prior to the first bone.
        /// </summary>
        public Matrix GetGlobalPreTransform()
        {
            //return globalPreTransformNode.CombinedLocalTransformAssimp;
            return globalPreTransformNode.LocalTransformMg;
        }

        /// <summary>
        /// This would be primarily used to get a transform that when multiplied by vertice vectors will scale those vertices to the same size it was in the model editor.
        /// As such its questionable if i would really need it much maybe when i dive into the animation it will have a requisite use.
        /// </summary>
        public Matrix GetBoneMeshZeroTransform()
        {
            return meshes[0].MeshTransformMg;
        }

        #endregion

        /// <summary>
        /// Models are composed of meshes each with there own textures and sets of vertices associated to them.
        /// </summary>
        public class RiggedModelMesh
        {
            public string textureName;
            public string textureNormalMapName;
            public string textureHeightMapName;
            public Texture2D texture;
            public Texture2D textureNormalMap;
            public Texture2D textureHeightMap;
            public VertexPositionTextureNormalTangentWeights[] vertices;
            public int[] indices;
            public string nameOfMesh = "";
            public int NumberOfIndices { get { return indices.Length; } }
            public int NumberOfVertices { get { return vertices.Length; } }
            public int MaterialIndex { get; set; }
            public Matrix MeshTransformMg { get; set; }
            /// <summary>
            /// Defines the minimum vertices extent in each direction x y z in system coordinates.
            /// </summary>
            public Vector3 Min { get; set; }
            /// <summary>
            /// Defines the mximum vertices extent in each direction x y z in system coordinates.
            /// </summary>
            public Vector3 Max { get; set; }
            /// <summary>
            /// Defines the center mass point or average of all the vertices.
            /// </summary>
            public Vector3 Centroid { get; set; }
        }

        /// <summary>
        /// A node of the rigged model is really a transform joint some are bones some aren't. These form a heirarchial linked tree structure.
        /// </summary>
        public class RiggedModelNode
        {
            public string name = "";
            public int boneShaderFinalTransformIndex = -1;
            public RiggedModelNode parent;
            public List<RiggedModelNode> children = new List<RiggedModelNode>();

            // probably don't need most of these they are from the debug phase
            public bool isTheRootNode = false;
            public bool isTheGlobalPreTransformNode = false; // marks the node prior to the first bone...   (which is a accumulated pre transform multiplier to other bones)?.
            public bool isTheFirstBone = false; // marked as root bone
            public bool isThisARealBone = false; // a actual bone with a bone offset
            public bool isThisNodeAlongTheBoneRoute = false; // similar to is isThisNodeTransformNecessary but can include the nodes after bones.
            public bool isThisNodeTransformNecessary = false; // has a requisite transformation in this node that a bone will need later.

            /// <summary>
            /// The inverse offset takes one from model space to bone space to say it will have a position were the bone is in the world.
            /// It is of the world space transform type from model space.
            /// </summary>
            public Matrix InvOffsetMatrixMg { get { return Matrix.Invert(OffsetMatrixMg); } set { OffsetMatrixMg = Matrix.Invert(value); } }
            /// <summary>
            /// Typically a chain of local transforms from bone to bone allow one bone to build off the next. 
            /// This is the inverse bind pose position and orientation of a bone or the local inverted bind pose e.g. inverse bone position at a node.
            /// The multiplication of this value by a full transformation chain at that specific node reveals the difference of its current model space orientations to its bind pose orientations.
            /// This is a tranformation from world space towards model space.
            /// </summary>
            public Matrix OffsetMatrixMg { get; set; }
            /// <summary>
            /// The simplest one to understand this is a transformation of a specific bone in relation to the previous bone.
            /// This is a world transformation that has local properties.
            /// </summary>
            public Matrix LocalTransformMg { get; set; }
            /// <summary>
            /// The multiplication of transforms down the tree accumulate this value tracks those accumulations.
            /// While the local transforms affect the particular orientation of a specific bone.
            /// While blender or other apps my allow some scaling or other adjustments from special matrices can be combined with this.
            /// This is a world space transformation. Basically the final world space transform that can be uploaded to the shader after all nodes are processed.
            /// </summary>
            public Matrix CombinedTransformMg { get; set; }
        }

        /// <summary>
        /// Animations for the animation structure i have all the nodes in the rigged animation and the nodes have lists of frames of animations.
        /// </summary>
        public class RiggedAnimation
        {
            public string targetNodeConsoleName = "_none_"; //"L_Hand";

            public string animationName = "";
            public double DurationInTicks = 0;
            public double DurationInSeconds = 0;
            public double TicksPerSecond = 0;
            public double SecondsPerFrame = 0;
            public double TicksPerFramePerSecond = 0;
            public int TotalFrames = 0;

            private int fps = 0;

            //public bool HasMeshAnimations = false;
            //public int MeshAnimationNodeCount;
            public bool HasNodeAnimations = false;
            public List<RiggedAnimationNodes> animatedNodes;

            public void SetAnimationFpsCreateFrames(int animationFramesPerSecond, RiggedModel model)
            {
                fps = animationFramesPerSecond;
                TotalFrames = (int)(DurationInSeconds * (double)(animationFramesPerSecond));
                TicksPerFramePerSecond = TicksPerSecond / (double)(animationFramesPerSecond);
                SecondsPerFrame = (1d / (animationFramesPerSecond));
                CalculateNewInterpolatedAnimationFrames(model);
            }

            private void CalculateNewInterpolatedAnimationFrames(RiggedModel model)
            {
                // Loop nodes.
                for (int i = 0; i < animatedNodes.Count; i++)
                {
                    // Make sure we have enough frame orientations alloted for the number of frames.
                    animatedNodes[i].frameOrientations = new Matrix[TotalFrames];
                    animatedNodes[i].frameOrientationTimes = new double[TotalFrames];

                    // print name of node as we loop
                    Console.WriteLine("name " + animatedNodes[i].nodeName);

                    // Loop destination frames.
                    for (int j = 0; j < TotalFrames; j++)
                    {
                        // Find and set the interpolated value from the s r t elements based on time.
                        var frameTime = j * SecondsPerFrame; // + .0001d;
                        animatedNodes[i].frameOrientations[j] = Interpolate(frameTime, animatedNodes[i]);
                        animatedNodes[i].frameOrientationTimes[j] = frameTime;
                    }
                    Console.WriteLine("");
                }
            }

            /// <summary>
            /// Hummm its just a little bit off it seems..
            /// </summary>
            public Matrix Interpolate(double animTime, RiggedAnimationNodes n)
            {
                while (animTime > DurationInSeconds)
                    animTime -= DurationInSeconds;

                var nodeAnim = n;
                // 
                Quaternion q2 = nodeAnim.qrot[0];
                Vector3 p2 = nodeAnim.position[0];
                Vector3 s2 = nodeAnim.scale[0];
                double tq2 = nodeAnim.qrotTime[0]; // =0;
                double tp2 = nodeAnim.positionTime[0]; ; // =0;
                double ts2 = nodeAnim.scaleTime[0]; //=0;
                // 
                int i1 = 0;
                Quaternion q1 = nodeAnim.qrot[i1];
                Vector3 p1 = nodeAnim.position[i1];
                Vector3 s1 = nodeAnim.scale[i1];
                double tq1 = nodeAnim.qrotTime[i1];
                double tp1 = nodeAnim.positionTime[i1];
                double ts1 = nodeAnim.scaleTime[i1];
                // 
                int qindex2 = 0; int qindex1 = 0; // for output to console only
                int pindex2 = 0; int pindex1 = 0; // for output to console only
                //
                for (int frame2 = nodeAnim.qrot.Count -1; frame2 > -1; frame2--)
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using Assimp;                               // note: install AssimpNET 4.1 via nuget



// TODO's  see the model class for more.  
//
// ARRRRGGG Ok much bigger problem the winding on the vertices are ccw and in many cases ill need cw wound vertices.
// Because i don't know exactly were the normals are being ignored but somewere down the line between monogame to dx gl
// The normals are calculated by the vertice winding and the actual normal data is ignored for the lighting.
//
//
// organize the reading order so it looks a little more readable and clear.
//
//
// read the material textures into the model ... edit... as lists.
// read the deformers
// read the colors its got a weird setup for that with channels.
// read all the values in the model for diffuse specular ect that is low priority most people will overide all that anyways in there own shader.
//
// tons of todos for the model need to be done as well.
// add back in errmm make a better bone visualization class because im going to want to edit animations later on.
// i really do not like blenders animation creator thing its wonky as hell  the principal stuff has already be setup for it in the model.
//
// One thing im definately going to do later is make a rescaler here because these models all seem to load with different sizes.
// Depending on who made them of course, even blender will rescale things funny.
// i still need to figure out how the mesh transform fits in to all this as it doesn't appear consistent doing a rescaler will mean it wont matter.
// The scaler will require that i recalculate the inverse bone offsets the local transforms and rescale all vertices.
// Before all that ill need to calculate the limits of all the vertices combined and then calculate the coefficients to the proportional scalars.
// this will need to be applied to pretty much everything except the rotations.
// then the inverse bind pose matrices for each bone will need to be recalculated.
// this surely belongs here not in the model class.


namespace AssimpLoaderExample
{
    /// <summary>
    /// Uses assimp.net to load a rigged and or animated model.
    /// </summary>
    public class RiggedModelLoader
    {
        Scene scene;

        public static ContentManager content;
        public static Effect effectToUse;
        public static Texture2D DefaultTexture { get; set; }

        /// <summary>
        /// Reverses the models winding typically this will change the model vertices to counter clockwise winding ccw.
        /// </summary>
        public bool ReverseVerticeWinding = false;

        public bool startupconsoleinfo = true;
        public bool startupAnimationConsoleInfo = true;
        public string targetNodeConsoleName = "L_Hand";



        int defaultAnimatedFramesPerSecondLod = 24;

        /// <summary>
        /// Loading content here is just for visualizing but it wont be requisite if we load all the textures in from xnb's at runtime in completed model.
        /// </summary>
        public RiggedModelLoader(ContentManager Content, Effect defaulteffect)
        {
            effectToUse = defaulteffect;
            content = Content;
        }

        public RiggedModel LoadAsset(string filePathorFileName)
        {
            return LoadAsset(filePathorFileName, defaultAnimatedFramesPerSecondLod);
        }

        public RiggedModel LoadAsset(string filePathorFileName, ContentManager Content)
        {
            content = Content;
            return LoadAsset(filePathorFileName, defaultAnimatedFramesPerSecondLod);
        }

        public RiggedModel LoadAsset(string filePathorFileName, int defaultAnimatedFramesPerSecondLod, ContentManager Content)
        {
            content = Content;
            return LoadAsset(filePathorFileName, defaultAnimatedFramesPerSecondLod);
        }

        /// <summary> 
        /// Primary loading method. This method first looks in the Assets folder then in the Content folder for the file.
        /// If that fails it will look to see if the filepath is actually the full path to the file.
        /// The texture itself is expected to be loaded and then attached to the effect atm.
        /// </summary>
        public RiggedModel LoadAsset(string filePathorFileName, int defaultAnimatedFramesPerSecondLod)
        {
            this.defaultAnimatedFramesPerSecondLod = defaultAnimatedFramesPerSecondLod;

            string s = Path.Combine(Path.Combine(Environment.CurrentDirectory, "Assets"), filePathorFileName);
            if (File.Exists(s) == false)
                s = Path.Combine(Path.Combine(Environment.CurrentDirectory, "Content"), filePathorFileName);
            if (File.Exists(s) == false)
                s = Path.Combine(Environment.CurrentDirectory, filePathorFileName);
            if (File.Exists(s) == false)
                s = filePathorFileName;
            Debug.Assert(File.Exists(s), "Could not find the file to load: " + s);
            string filepathorname = s;
            //
            // load the file at path to the scene
            //
            try
            {
                var importer = new AssimpContext();
                
                scene = importer.ImportFile
                                       (
                                        filepathorname,
                                          PostProcessSteps.FlipUVs                   // So far appears necessary
                                        | PostProcessSteps.JoinIdenticalVertices
                                        | PostProcessSteps.Triangulate
                                        | PostProcessSteps.FindInvalidData
                                        | PostProcessSteps.ImproveCacheLocality
                                        | PostProcessSteps.FindDegenerates
                                        | PostProcessSteps.SortByPrimitiveType
                                        | PostProcessSteps.OptimizeMeshes
                                        | PostProcessSteps.OptimizeGraph // normal
                                        //| PostProcessSteps.FixInFacingNormals
                                        | PostProcessSteps.ValidateDataStructure
                                        //| PostProcessSteps.GlobalScale
                                        //| PostProcessSteps.RemoveRedundantMaterials // sketchy
                                        //| PostProcessSteps.PreTransformVertices
                                        //| PostProcessSteps.GenerateUVCoords
                                        // PostProcessSteps.ValidateDataStructure
                                        );
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Debug.Assert(false, filePathorFileName + "\n\n"+ "A problem loading the model occured: \n\n" + e.Message);
                scene = null;
            }

            // check scene is not null and truely loaded
            if (scene == null)
            {
                Console.WriteLine($"AssimpFileModelReader Couldn't load {filePathorFileName}");
                return null;
            }
            else
            {
                Console.WriteLine("AssimpFileModelReader Loaded file: \n" + filepathorname);
            }
            return CreateModel(filepathorname);
        }

        /// <summary> Begins the flow to call methods and do the actual loading.
        /// </summary>
        private RiggedModel CreateModel(string filePath)
        {
            // create model
            RiggedModel model = new RiggedModel();

            // set the models effect to use.
            if(effectToUse != null)
                model.effect = effectToUse;

            // create the models meshes
            Console.WriteLine("\n@@@CreateModelMeshesSetMeshMaterialIndex");
            CreateModelMeshesSetUpMeshMaterialsAndTextures(model, scene, 0);

            // adds (mesh transform) to the model meshes arrays typically array index 0 holds the primary mesh transform
            Console.WriteLine("\n@@@SetNodeToMeshTransformsRecursive");
            SetModelMeshTransformsRecursively(model, scene.RootNode, 0, 0);

            //// take a look at material information.
            Console.WriteLine("\n@@@GetMaterialsInfoForNow");
            GetMaterialsInfoForNow(model, scene);

            // prep to build a models tree.
            Console.WriteLine("\n@@@prep to build a models tree.");
            model.rootNodeOfTree = new RiggedModel.RiggedModelNode();
            
            // set the rootnode and its transform
            model.rootNodeOfTree.name = scene.RootNode.Name;
            // set the rootnode transform
            model.rootNodeOfTree.LocalTransformMg = scene.RootNode.Transform.ToMgTransposed();

            // set up a dummy bone.
            Console.WriteLine("\n@@@CreateDummyStarterNodeZeroInFlatList");
            CreateDummyFlatListNodeZero(model);

            // recursively search and add the nodes to our model from the scene.
            Console.WriteLine("\n@@@BuildModelNodeTreeRecursive");
            CreateModelNodeTreeTransformsRecursively(model, model.rootNodeOfTree, scene.RootNode, 0);

            // find the actual and real first bone with a offset.
            Console.WriteLine("\n@@@FindSetActualBoneInModel");
            FindSetActualBoneInModel(model, scene.RootNode);

            // get the animations in the file into each nodes animations framelist
            Console.WriteLine("\n@@@GetAnimationsAsOriginalFromAssimp\n");
            model = GetOriginalAnimations(model, scene);

            // this is the last thing we will do because we need the nodes set up first.

            // get the vertice data from the meshes.
            Console.WriteLine("\n@@@GetVerticeIndiceData");
            model = GetVerticeIndiceData(model, scene, 0);

            // this calls the models function to create the interpolated animtion frames.
            // for a full set of callable time stamped orientations per frame so indexing and dirty flags can be used when running.
            Console.WriteLine("\n@@@CreateAnimationFrames");
            model.CreateAnimationFrames(defaultAnimatedFramesPerSecondLod);

            // if we want to see the original animation data all this console crap is for debuging.
            if (startupAnimationConsoleInfo)
            {
                Console.WriteLine("\n@@@PrintAnimData");
                PrintAnimData(scene);
            }

            Console.WriteLine("\n");
            Console.WriteLine("\n");
            Console.WriteLine("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
            Console.WriteLine();
            Console.WriteLine("Model Loaded");
            Console.WriteLine();
            Console.WriteLine(filePath);
            Console.WriteLine();
            Console.WriteLine("Model number of bones: " + model.numberOfBonesInUse);
            Console.WriteLine("Model number of animaton: " + model.origAnim.Count);
            Console.WriteLine("BoneRoot's Node Name: " + model.rootNodeOfTree.name);
            Console.WriteLine();
            Console.WriteLine("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");

            return model;
        }

        /// <summary>We create model mesh instances for each mesh in scene.meshes. This is just set up it doesn't load any data.
        /// </summary>
        public void CreateModelMeshesSetUpMeshMaterialsAndTextures(RiggedModel model, Scene scene, int meshIndex)
        {
            model.meshes = new RiggedModel.RiggedModelMesh[scene.Meshes.Count];
            // create a new model.mesh per mesh in scene
            for (int mloop = 0; mloop < scene.Meshes.Count; mloop++)
            {
                Mesh mesh = scene.Meshes[mloop];
                var m = new RiggedModel.RiggedModelMesh();
                m.texture = DefaultTexture;
                m.textureName = "";
                //
                // The material used by this mesh.
                //A mesh uses only a single material. If an imported model uses multiple materials, the import splits up the mesh. Use this value as index into the scene's material list. 
                // http://sir-kimmi.de/assimp/lib_html/structai_mesh.html#aa2807c7ba172115203ed16047ad65f9e
                //
                m.MaterialIndex = mesh.MaterialIndex;
                if(startupconsoleinfo)
                    Console.WriteLine("scene.Meshes[" + mloop + "] " + "  (material associated to this mesh) Material index: "+ m.MaterialIndex + "  Name " + mesh.Name);
                model.meshes[mloop] = m;

                for (int i = 0; i < scene.Materials.Count; i++)
                {
                    if (i == m.MaterialIndex)
                    {
                        Console.WriteLine("  Materials[" + i + "]   get material textures");
                        var material = scene.Materials[i];
                        var t = material.GetAllMaterialTextures();
                        for (int j = 0; j < t.Length; j++)
                        {
                            var tindex = t[j].TextureIndex;
                            var toperation = t[j].Operation;
                            var ttype = t[j].TextureType.ToString();
                            var tfilepath = t[j].FilePath;
                            
                            var tfilename = GetFileName(tfilepath, true);
                            var tfullfilepath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, content.RootDirectory, tfilename + ".xnb");
                            var tfileexists = File.Exists(tfullfilepath);

                            var taltfilename = GetFileName(tfilepath, false);
                            var taltfullpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, content.RootDirectory, tfilename + ".xnb");
                            var taltfileexists = File.Exists(taltfullpath);

                            if (startupconsoleinfo)
                                Console.WriteLine("      Texture[" + j + "] " + "   Index: " + tindex.ToString().PadRight(5) + "   Type: " + ttype.PadRight(15) + "   Filepath: " + tfilepath.PadRight(15) + " Name: "+ tfilename.PadRight(15) + "  ExistsInContent: "+ tfileexists);
                            if (ttype == "Diffuse")
                            {
                                model.meshes[mloop].textureName = tfilename;
                                if (content != null && tfileexists)
                                {
                                    model.meshes[mloop].texture = content.Load<Texture2D>(tfilename);
                                    Console.WriteLine("      ...Texture loaded: ... " + tfilename);
                                }
                            }
                            if (ttype == "Normal")
                            {
                                model.meshes[mloop].textureNormalMapName = tfilename;
                                if (content != null && tfileexists)
                                {
                                    model.meshes[mloop].textureNormalMap = content.Load<Texture2D>(tfilename);
                                    Console.WriteLine("      ...Texture loaded: ... " + tfilename);
                                }
                            }
                            if (ttype == "Height")
                            {
                                model.meshes[mloop].textureHeightMapName = tfilename;
                                if (content != null && tfileexists)
                                {
                                    model.meshes[mloop].textureHeightMap = content.Load<Texture2D>(tfilename);
                                    Console.WriteLine("      ...Texture loaded: ... " + tfilename);
                                }
                            }
                        }
                    }
                }

            }       
        }

        /// <summary>We recursively walk the nodes here to set the mesh name and transforms.
        /// </summary>
        public void SetModelMeshTransformsRecursively(RiggedModel model, Node node, int tabLevel, int meshIndex)
        {
            // when we find a node with meshes.
            if (node.HasMeshes)
            {
                // Ok i think only the first mesh is valid i dunno whats up with that.
                //model.meshTransform = node.Transform;
                // 
                model.meshes[meshIndex].nameOfMesh = node.Name;
                model.meshes[meshIndex].MeshTransformMg = node.Transform.ToMgTransposed();
                meshIndex++;
            }
            // access children
            for (int i = 0; i < node.Children.Count; i++)
            {
                SetModelMeshTransformsRecursively(model, node.Children[i], tabLevel + 1, meshIndex);
            }
        }

        /// <summary>  this isn't really necessary but i do it for debuging reasons. 
        /// </summary>
        public void CreateDummyFlatListNodeZero(RiggedModel model)
        {
            var modelnode = new RiggedModel.RiggedModelNode();
            modelnode.name = "DummyBone0";
            // though we mark this false we add it to the flat bonenodes we index them via the bone count which is incremented below.
            modelnode.isThisARealBone = false;
            modelnode.isThisNodeAlongTheBoneRoute = false;
            modelnode.OffsetMatrixMg = Matrix.Identity;
            modelnode.LocalTransformMg = Matrix.Identity;
            modelnode.CombinedTransformMg = Matrix.Identity;
            modelnode.boneShaderFinalTransformIndex = model.flatListToBoneNodes.Count;
            model.flatListToBoneNodes.Add(modelnode);
            model.numberOfBonesInUse++;
        }

        /// <summary> We recursively walk the nodes here this drops all the info from scene nodes to model nodes.
        /// This gets offset matrices, if its a bone mesh index, global index, marks parents necessary ect..
        /// We mark neccessary also is this a bone, also is this part of a bone chain, children parents ect.
        /// Add node to model
        /// </summary>
        public void CreateModelNodeTreeTransformsRecursively(RiggedModel model, RiggedModel.RiggedModelNode modelnode, Node assimpSceneNode, int tabLevel)
        {
            // model structure creation building here.
            Point indexPair = SearchSceneMeshBonesForName(assimpSceneNode.Name, scene);
            // if the y value here is more then -1 this is then in fact a actual bone in the scene.
            if (indexPair.Y > -1)
            {
                // mark this a bone.
                modelnode.isThisARealBone = true;
                // mark it a requisite transform node.
                modelnode.isThisNodeAlongTheBoneRoute = true;
                // the offset bone matrix
                modelnode.OffsetMatrixMg = SearchSceneMeshBonesForNameGetOffsetMatrix(assimpSceneNode.Name, scene).ToMgTransposed();
                // this maybe a bit redundant but i really don't care once i load it i can convert it to a more streamlined format later on.
                MarkParentsNessecary(modelnode);
                // we are about to add this now to the flat bone nodes list so also denote the index to the final shader transform.
                modelnode.boneShaderFinalTransformIndex = model.flatListToBoneNodes.Count;
                // necessary to keep things in order for the offsets as a way to just iterate thru bones and link to them thru a list.
                model.flatListToBoneNodes.Add(modelnode);
                // increment the number of bones.
                model.numberOfBonesInUse++;
            }

            // set the nodes name.
            modelnode.name = assimpSceneNode.Name;
            // set the initial local node transform.
            modelnode.LocalTransformMg = assimpSceneNode.Transform.ToMgTransposed();

            // access children
            for (int i = 0; i < assimpSceneNode.Children.Count; i++)
            {
                var childAsimpNode = assimpSceneNode.Children[i];
                var childBoneNode = new RiggedModel.RiggedModelNode();
                // set parent before passing.
                childBoneNode.parent = modelnode;
                childBoneNode.name = assimpSceneNode.Children[i].Name;
                if (childBoneNode.parent.isThisNodeAlongTheBoneRoute)
                    childBoneNode.isThisNodeAlongTheBoneRoute = true;
                modelnode.children.Add(childBoneNode);
                CreateModelNodeTreeTransformsRecursively(model, modelnode.children[i], childAsimpNode, tabLevel + 1);
            }
        }

        /// <summary>Get Scene Model Mesh Vertices. Gets all the mesh data into a mesh array. 
        /// </summary>
        public RiggedModel GetVerticeIndiceData(RiggedModel model, Scene scene, int meshIndex)
        {
            // http://sir-kimmi.de/assimp/lib_html/structai_mesh.html#aa2807c7ba172115203ed16047ad65f9e
            // just print out the flat node bones before we start so i can see whats up.
            if (startupconsoleinfo)
            {
                Console.WriteLine();
                Console.WriteLine("Flat bone nodes");
            }
            for (int i = 0; i < model.flatListToBoneNodes.Count(); i++)
            {
                var b = model.flatListToBoneNodes[i];
                if (startupconsoleinfo)
                    Console.WriteLine(b.name);
            }
            if (startupconsoleinfo)
                Console.WriteLine();

            //
            // Loop meshes for Vertice data.
            //
            for (int mloop = 0; mloop < scene.Meshes.Count; mloop++)
            {
                Mesh mesh = scene.Meshes[mloop];
                if (startupconsoleinfo)
                {
                    Console.WriteLine(
                    "\n" + "__________________________" +
                    "\n" + "scene.Meshes[" + mloop + "] " +
                    "\n" + " FaceCount: " + mesh.FaceCount +
                    "\n" + " VertexCount: " + mesh.VertexCount +
                    "\n" + " Normals.Count: " + mesh.Normals.Count +
                    "\n" + " BoneCount: " + mesh.BoneCount +
                    "\n" + " MaterialIndex: " + mesh.MaterialIndex
                    );
                    Console.WriteLine("  mesh.UVComponentCount.Length: " + mesh.UVComponentCount.Length);
                }
                for (int i = 0; i < mesh.UVComponentCount.Length; i++)
                {
                    int val = mesh.UVComponentCount[i];
                    if (startupconsoleinfo)
                        Console.WriteLine("       mesh.UVComponentCount[" + i + "] : " + val);
                }

                // indices
                int[] indexs = new int[mesh.Faces.Count * 3];
                int loopindex = 0;
                for (int k = 0; k < mesh.Faces.Count; k++)
                {
                    var f = mesh.Faces[k];
                    for (int j = 0; j < f.IndexCount; j++)
                    {
                        var ind = f.Indices[j];
                        indexs[loopindex] = ind;
                        loopindex++;
                    }
                }

                // vertices 
                VertexPositionTextureNormalTangentWeights[] v = new VertexPositionTextureNormalTangentWeights[mesh.Vertices.Count];
                for (int k = 0; k < mesh.Vertices.Count; k++)
                {
                    var f = mesh.Vertices[k];
                    v[k].Position = new Vector3(f.X, f.Y, f.Z);
                }
                // normals
                for (int k = 0; k < mesh.Normals.Count; k++)
                {
                    var f = mesh.Normals[k];
                    v[k].Normal = new Vector3(f.X, f.Y, f.Z);
                }

                // Check whether the mesh contains tangent and bitangent vectors It is not possible that it contains tangents and no bitangents (or the other way round). 
                // http://sir-kimmi.de/assimp/lib_html/structai_mesh.html#aa2807c7ba172115203ed16047ad65f9e
                //

                //// TODO need to add this to the vertex declaration or calculate it on the shader.

                // tangents
                for (int k = 0; k < mesh.Tangents.Count; k++)
                {
                    var f = mesh.Tangents[k];
                    v[k].Tangent = new Vector3(f.X, f.Y, f.Z);
                }
                // bi tangents  
                for (int k = 0; k < mesh.BiTangents.Count; k++)
                {
                    var f = mesh.BiTangents[k];
                    v[k].Tangent = f.ToMg();
                }

                // A mesh may contain 0 to AI_MAX_NUMBER_OF_COLOR_SETS vertex colors per vertex. NULL if not present. Each array is mNumVertices in size if present. 
                // http://sir-kimmi.de/assimp/lib_html/structai_mesh.html#aa2807c7ba172115203ed16047ad65f9e

                // TODO colors dunno why there are lists of lists for colors 
                // maybe its multi colored or something ill have to read up on this ...  not sure this is the right way to do it ?
                //  This will have to be made from scratch need v4 to mg and other stuff
                //
                if (mesh.HasVertexColors(0))
                {
                    for (int k = 0; k < mesh.VertexColorChannels[0].Count; k++)
                    {
                        var f = mesh.VertexColorChannels[k];
                        var c = f[k];
                        v[k].Color = new Vector4(c.R, c.G, c.B, c.A);
                    }
                }
                else
                {
                    for (int k = 0; k < mesh.VertexColorChannels[0].Count; k++)
                        v[k].Color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
                }
                

                // Check whether the mesh contains a texture coordinate set. 
                // mNumUVComponents
                // unsigned int aiMesh::mNumUVComponents[AI_MAX_NUMBER_OF_TEXTURECOORDS]
                // Specifies the number of components for a given UV channel.
                // Up to three channels are supported(UVW, for accessing volume or cube maps).If the value is 2 for a given channel n, the component p.z of mTextureCoords[n][p] is set to 0.0f.If the value is 1 for a given channel, p.y is set to 0.0f, too.
                // Note 4D coords are not supported

                // Uv
                Console.WriteLine("");
                var uvchannels = mesh.TextureCoordinateChannels;
                for (int k = 0; k < uvchannels.Length; k++)
                {
                    var f = uvchannels[k];
                    int loopIndex = 0;
                    for (int j = 0; j < f.Count; j++)
                    {
                        var uv = f[j];
                        v[loopIndex].TextureCoordinate = new Microsoft.Xna.Framework.Vector2(uv.X, uv.Y);
                        loopIndex++;
                    }
                }

                // find the min max vertices for a bounding box.
                // this is useful for other stuff which i need right now.

                Vector3 min = Vector3.Zero;
                Vector3 max = Vector3.Zero;
                Vector3 centroid = Vector3.Zero;
                foreach (var vert in v)
                {
                    if (vert.Position.X < min.X){ min.X = vert.Position.X;}
                    if (vert.Position.Y < min.Y) { min.Y = vert.Position.Y; }
                    if (vert.Position.Z < min.Z) { min.Z = vert.Position.Z; }
                    if (vert.Position.X > max.X) { max.X = vert.Position.X; }
                    if (vert.Position.Y > max.Y) { max.Y = vert.Position.Y; }
                    if (vert.Position.Z > max.Z) { max.Z = vert.Position.Z; }
                    centroid += vert.Position;
                }
                model.meshes[mloop].Centroid = centroid / (float)v.Length;
                model.meshes[mloop].Min = min;
                model.meshes[mloop].Max = max;

                // Prep blend weight and indexs this one is just prep for later on.
                for (int k = 0; k < mesh.Vertices.Count; k++)
                {
                    var f = mesh.Vertices[k];
                    v[k].BlendIndices = new Vector4(0f, 0f, 0f, 0f);
                    v[k].BlendWeights = new Vector4(0f, 0f, 0f, 0f);
                }

                // Restructure vertice data to conform to a shader.
                // Iterate mesh bone offsets set the bone Id's and weights to the vertices.
                // This also entails correlating the mesh local bone index names to the flat bone list.
                TempWeightVert[] verts = new TempWeightVert[mesh.Vertices.Count];
                if (mesh.HasBones)
                {
                    var meshBones = mesh.Bones;
                    if (startupconsoleinfo)
                        Console.WriteLine("meshBones.Count: " + meshBones.Count);
                    for (int meshBoneIndex = 0; meshBoneIndex < meshBones.Count; meshBoneIndex++)
                    {
                        var boneInMesh = meshBones[meshBoneIndex]; // ahhhh
                        var boneInMeshName = meshBones[meshBoneIndex].Name;
                        var correspondingFlatBoneListIndex = GetFlatBoneIndexInModel(model, scene, boneInMeshName);

                        if (startupconsoleinfo)
                        {
                            string str = "  mesh.Name: " + mesh.Name + "mesh[" + mloop + "] " + " bone.Name: " + boneInMeshName.PadRight(17) + "     meshLocalBoneListIndex: " + meshBoneIndex.ToString().PadRight(4) + " flatBoneListIndex: " + correspondingFlatBoneListIndex.ToString().PadRight(4) + " WeightCount: " + boneInMesh.VertexWeightCount;
                            Console.WriteLine(str);
                        }

                        // loop thru this bones vertice listings with the weights for it.
                        for (int weightIndex = 0; weightIndex < boneInMesh.VertexWeightCount; weightIndex++)
                        {
                            var verticeIndexTheBoneIsFor = boneInMesh.VertexWeights[weightIndex].VertexID;
                            var boneWeightVal = boneInMesh.VertexWeights[weightIndex].Weight;
                            if (verts[verticeIndexTheBoneIsFor] == null)
                            {
                                verts[verticeIndexTheBoneIsFor] = new TempWeightVert();
                            }
                            // add this vertice its weight and the bone id to the temp verts list.
                            verts[verticeIndexTheBoneIsFor].verticeIndexs.Add(verticeIndexTheBoneIsFor);
                            verts[verticeIndexTheBoneIsFor].verticesFlatBoneId.Add(correspondingFlatBoneListIndex);
                            verts[verticeIndexTheBoneIsFor].verticeBoneWeights.Add(boneWeightVal);
                            verts[verticeIndexTheBoneIsFor].countOfBoneEntrysForThisVertice ++;
                        }
                    }
                }
                else // mesh has no bones
                {
                    // if there is no bone data we will make it set to bone zero.
                    // this is basically a safety measure as if there is no bone data there is no bones.
                    // however the vertices need to have a weight of 1.0 for bone zero which is really identity.
                    for (int i = 0; i < verts.Length; i++)
                    {
                        verts[i] = new TempWeightVert();
                        var ve = verts[i];
                        if (ve.verticeIndexs.Count == 0)
                        {
                            // there is no bone data for this vertice at all then we should set it to bone zero.
                            verts[i].verticeIndexs.Add(i);
                            verts[i].verticesFlatBoneId.Add(0);
                            verts[i].verticeBoneWeights.Add(1.0f);
                        }
                    }
                }

                // Ill need up to 4 values per bone list so if some of the values are empty ill copy zero to them with weight 0.
                // This is to ensure the full key vector4 is populated.
                // The bone weight data aligns to the bones not nodes so it aligns to the offset matrices bone names.

                // loop each temp vertice add the temporary structure we have to the model vertices in sequence.
                for (int i = 0; i < verts.Length; i++)
                {
                    if (verts[i] != null)
                    {
                        var ve = verts[i];
                        //int maxbones = 4;
                        var arrayIndex = ve.verticeIndexs.ToArray();
                        var arrayBoneId = ve.verticesFlatBoneId.ToArray();
                        var arrayWeight = ve.verticeBoneWeights.ToArray();
                        if (arrayBoneId.Count() > 3)
                        {
                            v[arrayIndex[3]].BlendIndices.W = arrayBoneId[3];
                            v[arrayIndex[3]].BlendWeights.W = arrayWeight[3];
                        }
                        if (arrayBoneId.Count() > 2)
                        {
                            v[arrayIndex[2]].BlendIndices.Z = arrayBoneId[2];
                            v[arrayIndex[2]].BlendWeights.Z = arrayWeight[2];
                        }
                        if (arrayBoneId.Count() > 1)
                        {
                            v[arrayIndex[1]].BlendIndices.Y = arrayBoneId[1];
                            v[arrayIndex[1]].BlendWeights.Y = arrayWeight[1];
                        }
                        if (arrayBoneId.Count() > 0)
                        {
                            v[arrayIndex[0]].BlendIndices.X = arrayBoneId[0];
                            v[arrayIndex[0]].BlendWeights.X = arrayWeight[0];
                        }
                    }
                }

                model.meshes[mloop].vertices = v;
                model.meshes[mloop].indices = indexs;

                // last thing reverse the winding if specified.
                if (ReverseVerticeWinding)
                {
                    for (int k = 0; k < model.meshes[mloop].indices.Length; k+=3)
                    {                       
                        var i0 = model.meshes[mloop].indices[k+0];
                        var i1 = model.meshes[mloop].indices[k+1];
                        var i2 = model.meshes[mloop].indices[k+2];
                        model.meshes[mloop].indices[k + 0] = i0;
                        model.meshes[mloop].indices[k + 1] = i2;
                        model.meshes[mloop].indices[k + 2] = i1;
                    }
                }

            }
            return model;
        }

        /// <summary> Gets the assimp animations as the original does it into the model.
        /// </summary>
        public RiggedModel GetOriginalAnimations(RiggedModel model, Scene scene)
        {
            // Nice now i find it after i already figured it out.
            // http://sir-kimmi.de/assimp/lib_html/_animation_overview.html
            // http://sir-kimmi.de/assimp/lib_html/structai_animation.html
            // http://sir-kimmi.de/assimp/lib_html/structai_anim_mesh.html
            // Animations

            // Copy over as assimp has it set up.
            for (int i = 0; i < scene.Animations.Count; i++)
            {
                var anim = scene.Animations[i];
                //________________________________________________
                // Initial copy over.
                var mAnim = new RiggedModel.RiggedAnimation();
                mAnim.animationName = anim.Name;
                mAnim.TicksPerSecond = anim.TicksPerSecond;
                mAnim.DurationInTicks = anim.DurationInTicks;
                mAnim.DurationInSeconds = anim.DurationInTicks / anim.TicksPerSecond;
                // Default.
                mAnim.TotalFrames = (int)(mAnim.DurationInSeconds * (double)(defaultAnimatedFramesPerSecondLod));
                mAnim.TicksPerFramePerSecond = mAnim.TicksPerSecond / (double)(defaultAnimatedFramesPerSecondLod);
                mAnim.SecondsPerFrame = (1d / (defaultAnimatedFramesPerSecondLod));
                // 
                mAnim.animatedNodes = new List<RiggedModel.RiggedAnimationNodes>();
                // Loop the node channels.
                for (int j = 0; j < anim.NodeAnimationChannels.Count; j++)
                {
                    var nodeAnimLists = anim.NodeAnimationChannels[j];
                    var nodeAnim = new RiggedModel.RiggedAnimationNodes();
                    nodeAnim.nodeName = nodeAnimLists.NodeName;

                    // Set the reference to the node for node name by the model method that searches for it.
                    var modelnoderef = ModelGetRefToNode(nodeAnimLists.NodeName, model.rootNodeOfTree);
                    //var modelnoderef = model.SearchNodeTreeByNameGetRefToNode(nodeAnimLists.NodeName);
                    nodeAnim.nodeRef = modelnoderef;

                    // Place all the different keys lists rot scale pos into this nodes elements lists.
                    //foreach (var keyList in nodeAnimLists.RotationKeys)
                    //{
                    //    var oam = Helpers.ToMgTransposed(keyList.Value.GetMatrix());
                    //    nodeAnim.rotationTime.Add(keyList.Time / anim.TicksPerSecond);  // / anim.TicksPerSecond if i want to turn it into seconds i probably do.
                    //    nodeAnim.rotation.Add(oam);
                    //}
                    foreach (var keyList in nodeAnimLists.RotationKeys)
                    {
                        var oaq = keyList.Value;
                        nodeAnim.qrotTime.Add(keyList.Time / anim.TicksPerSecond);
                        nodeAnim.qrot.Add(oaq.ToMg() ); // After i get this to work using assimp quaternions ill get it running with monogames they might need transposed which is the conjugate i think
                    }
                    foreach (var keyList in nodeAnimLists.PositionKeys)
                    {
                        var oap = keyList.Value.ToMg();
                        nodeAnim.positionTime.Add(keyList.Time / anim.TicksPerSecond);
                        nodeAnim.position.Add(oap);
                    }
                    foreach (var keyList in nodeAnimLists.ScalingKeys)
                    {
                        var oas = keyList.Value.ToMg();
                        nodeAnim.scaleTime.Add(keyList.Time / anim.TicksPerSecond);
                        nodeAnim.scale.Add(oas);
                    }
                    // Place this populated node into this model animation,  model.origAnim
                    mAnim.animatedNodes.Add(nodeAnim);
                }
                // Place the animation into the model.
                model.origAnim.Add(mAnim);
            }
            return model;
        }

        /*  well need this later on if we want these other standard types of animations
                Console.WriteLine($"  HasMeshAnimations: {anim.HasMeshAnimations} ");
                Console.WriteLine($"  Mesh Animation Channels: {anim.MeshAnimationChannelCount} ");
                foreach (var chan in anim.MeshAnimationChannels)
                {
                    Console.WriteLine($"  Channel MeshName {chan.MeshName}");        // the node name has to be used to tie this channel to the originally printed hierarchy.  BTW, node names must be unique.
                    Console.WriteLine($"    HasMeshKeys: {chan.HasMeshKeys}");       // access via chan.PositionKeys
                    Console.WriteLine($"    MeshKeyCount: {chan.MeshKeyCount}");       // 
                    //Console.WriteLine($"    Scaling  Keys: {chan.MeshKeys}");        // 
                }
                Console.WriteLine($"  Mesh Morph Channels: {anim.MeshMorphAnimationChannelCount} ");
                foreach (var chan in anim.MeshMorphAnimationChannels)
                {
                    Console.WriteLine($"  Channel {chan.Name}");
                    Console.WriteLine($"    HasMeshMorphKeys: {chan.HasMeshMorphKeys}");       // 
                    Console.WriteLine($"     MeshMorphKeyCount: {chan.MeshMorphKeyCount}");       // 
                    //Console.WriteLine($"    Scaling  Keys: {chan.MeshMorphKeys}");        // 
                }
         */


        /*
           ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
           ++++++++++++++++++++ Additional functions ++++++++++++++++++++
           ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        */

        /// <summary> Custom get file name.
        /// </summary>
        public string GetFileName(string s, bool useBothSeperators)
        {
            var tpathsplit = s.Split(new char[] { '.' });
            string f = tpathsplit[0];
            if (tpathsplit.Length > 1)
            {
                f = tpathsplit[tpathsplit.Length - 2];
            }
            if (useBothSeperators)
                tpathsplit = f.Split(new char[] { '/', '\\' });
            else
                tpathsplit = f.Split(new char[] { '/' });
            s = tpathsplit[tpathsplit.Length - 1];
            return s;
        }

        /// <summary>We Mark Parents Nessecary down the chain till we hit null this is because the parent node matrices are required for the transformation chain.
        /// </summary>
        public void MarkParentsNessecary(RiggedModel.RiggedModelNode b)
        {
            b.isThisNodeTransformNecessary = true;
            b.isThisNodeAlongTheBoneRoute = true;
            if (b.parent != null)
                MarkParentsNessecary(b.parent);
            else
                b.isTheRootNode = true;
        }

        /// <summary>We Mark Parents Un Nessecary down the chain till we hit null this is for when the parent nodes are not used before the root bone.
        /// </summary>
        public void MarkParentsUnNessecary(RiggedModel.RiggedModelNode b)
        {
            b.isThisNodeTransformNecessary = false;
            if (b.parent != null)
                MarkParentsUnNessecary(b.parent);
        }

        /// <summary>This also sets the global root and inverse transform from the assimp found first user defined bone.
        /// </summary>
        public void FindSetActualBoneInModel(RiggedModel model, Node node)
        {
            bool result = false;
            Point indexPair = SearchSceneMeshBonesForName(node.Name, scene);
            if (indexPair.Y > -1)
            {
                result = true;
                model.firstRealBoneInTree = SearchAssimpNodesForName(node.Name, model.rootNodeOfTree);
                model.firstRealBoneInTree.isTheFirstBone = true;
                model.globalPreTransformNode = model.firstRealBoneInTree.parent;
                model.globalPreTransformNode.isTheGlobalPreTransformNode = true;
            }
            else
            {
                foreach (var c in node.Children)
                    FindSetActualBoneInModel(model, c);
            }
        }

        /// <summary> Gets the index to the flat bone from its node name. 
        /// </summary>
        public int GetFlatBoneIndexInModel(RiggedModel model, Scene scene, string nameToFind)
        {
            int index = -1;
            for (int i = 0; i < model.flatListToBoneNodes.Count; i++)
            {
                var n = model.flatListToBoneNodes[i];
                if (n.name == nameToFind)
                {
                    index = i;
                    i = model.flatListToBoneNodes.Count; // break
                }
            }
            if (index == -1)
            {
                if (startupconsoleinfo)
                    Console.WriteLine("**** No Index found for the named bone (" + nameToFind + ") this is not good ");
            }
            return index;
        }


        /// <summary>Returns X as the mesh index and Y as the bone number if Y is negative it is not a bone.
        /// </summary>
        public Point SearchSceneMeshBonesForName(string name, Scene scene)
        {
            Point result = new Point(-1, -1);
            bool found = false;
            for (int i = 0; i < scene.Meshes.Count; i++)
            {
                for (int j = 0; j < scene.Meshes[i].Bones.Count; j++)
                {
                    if (scene.Meshes[i].Bones[j].Name == name)
                    {
                        result = new Point(i, j);
                        found = true;
                        j = scene.Meshes[i].Bones.Count;
                    }
                }
                if (found)
                    i = scene.Meshes.Count;
            }
            return result;
        }

        /// <summary>
        /// Returns X as the mesh index and Y as the bone number if Y is negative it is not a bone.
        /// </summary>
        public Matrix4x4 SearchSceneMeshBonesForNameGetOffsetMatrix(string name, Scene scene)
        {
            Matrix4x4 result = Matrix4x4.Identity;
            bool found = false;
            for (int i = 0; i < scene.Meshes.Count; i++)
            {
                for (int j = 0; j < scene.Meshes[i].Bones.Count; j++)
                {
                    if (scene.Meshes[i].Bones[j].Name == name)
                    {
                        result = scene.Meshes[i].Bones[j].OffsetMatrix;//MatrixConvertAssimpToMg(scene.Meshes[i].Bones[j].OffsetMatrix);
                        found = true;
                        j = scene.Meshes[i].Bones.Count;
                    }
                }
                if (found)
                    i = scene.Meshes.Count;
            }
            return result;
        }

        /// <summary>
        /// Finds the model node with the bone name.
        /// </summary>
        public RiggedModel.RiggedModelNode SearchAssimpNodesForName(string name, RiggedModel.RiggedModelNode node)
        {
            RiggedModel.RiggedModelNode result = null;
            if (node.name == name)
            {
                result = node;
            }
            if (result == null && node.children.Count > 0)
            {
                for (int i = 0; i < node.children.Count; i++)
                {
                    var res = SearchAssimpNodesForName(name, node.children[i]);
                    if (res != null)
                    {
                        result = res;
                        i = node.children.Count;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// </summary>
        public RiggedModel.RiggedModelNode SearchNodeTreeByNameGetRefToNode(string name, RiggedModel.RiggedModelNode rootNodeOfTree) // , OnlyAssimpBasedModel model );
        {
            return SearchIterateNodeTreeForNameGetRefToNode(name, rootNodeOfTree);
        }

        /// <summary>
        /// </summary>
        private RiggedModel.RiggedModelNode SearchIterateNodeTreeForNameGetRefToNode(string name, RiggedModel.RiggedModelNode node)
        {
            RiggedModel.RiggedModelNode result = null;
            if (node.name == name)
                result = node;
            if (result == null && node.children.Count > 0)
            {
                for (int i = 0; i < node.children.Count; i++)
                {
                    var res = SearchIterateNodeTreeForNameGetRefToNode(name, node.children[i]);
                    if (res != null)
                    {
                        // set result and break if the named node was found
                        result = res;
                        i = node.children.Count;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// </summary>
        public static RiggedModel.RiggedModelNode ModelGetRefToNode(string name, RiggedModel.RiggedModelNode rootNodeOfTree) // , OnlyAssimpBasedModel model );
            {
                return ModelSearchIterateNodeTreeForNameGetRefToNode(name, rootNodeOfTree);
            }
        
        /// <summary>
        /// </summary>
        private static RiggedModel.RiggedModelNode ModelSearchIterateNodeTreeForNameGetRefToNode(string name, RiggedModel.RiggedModelNode node)
            {
            RiggedModel.RiggedModelNode result = null;
                if (node.name == name)
                    result = node;
                if (result == null && node.children.Count > 0)
                {
                    for (int i = 0; i < node.children.Count; i++)
                    {
                        var res = ModelSearchIterateNodeTreeForNameGetRefToNode(name, node.children[i]);
                        if (res != null)
                        {
                            // set result and break if the named node was found
                            result = res;
                            i = node.children.Count;
                        }
                    }
                }
                return result;
            }

        /// <summary>
        /// </summary>
        public void PrintAnimData(Scene scene)
        {
            //int i;
            if (startupconsoleinfo)
            {
                string str = "\n\n AssimpSceneConsoleOutput ========= Animation Data========= \n\n";
                Console.WriteLine(str);
            }

            for (int i = 0; i < scene.Animations.Count; i++)
            {
                var anim = scene.Animations[i];
                if (startupconsoleinfo)
                {
                    Console.WriteLine($"__________________________");
                    Console.WriteLine($"Anim #[{i}] Name: {anim.Name}");
                    Console.WriteLine($"__________________________");
                    Console.WriteLine($"  Duration: {anim.DurationInTicks} / {anim.TicksPerSecond} sec.   total duration in seconds: {anim.DurationInTicks / anim.TicksPerSecond}");
                    Console.WriteLine($"  HasMeshAnimations: {anim.HasMeshAnimations} ");
                    Console.WriteLine($"  Mesh Animation Channels: {anim.MeshAnimationChannelCount} ");
                }
                foreach (var chan in anim.MeshAnimationChannels)
                {
                    if (startupconsoleinfo)
                    {
                            Console.WriteLine($"  Channel MeshName {chan.MeshName}");        // the node name has to be used to tie this channel to the originally printed hierarchy.  BTW, node names must be unique.
                            Console.WriteLine($"    HasMeshKeys: {chan.HasMeshKeys}");       // access via chan.PositionKeys
                            Console.WriteLine($"    MeshKeyCount: {chan.MeshKeyCount}");       // 
                                                                                               //Console.WriteLine($"    Scaling  Keys: {chan.MeshKeys}");        // 
                    }
                }
                if (startupconsoleinfo)
                    Console.WriteLine($"  Mesh Morph Channels: {anim.MeshMorphAnimationChannelCount} ");
                foreach (var chan in anim.MeshMorphAnimationChannels)
                {
                    if (startupconsoleinfo && (targetNodeConsoleName != "" || targetNodeConsoleName == chan.Name))
                    {
                        Console.WriteLine($"  Channel {chan.Name}");
                        Console.WriteLine($"    HasMeshMorphKeys: {chan.HasMeshMorphKeys}");       // 
                        Console.WriteLine($"     MeshMorphKeyCount: {chan.MeshMorphKeyCount}");       // 
                        //Console.WriteLine($"    Scaling  Keys: {chan.MeshMorphKeys}");        // 
                    }
                }
                if (startupconsoleinfo)
                {
                    if (startupconsoleinfo)
                    {
                        Console.WriteLine($"  HasNodeAnimations: {anim.HasNodeAnimations} ");
                        Console.WriteLine($"   Node Channels: {anim.NodeAnimationChannelCount}");
                    }
                }
                foreach (var chan in anim.NodeAnimationChannels)
                {
                    if (startupconsoleinfo && (targetNodeConsoleName == "" || targetNodeConsoleName == chan.NodeName))
                    {
                        Console.Write($"   Channel {chan.NodeName}".PadRight(35));        // the node name has to be used to tie this channel to the originally printed hierarchy.  BTW, node names must be unique.
                        Console.Write($"     Position Keys: {chan.PositionKeyCount}".PadRight(25));         // access via chan.PositionKeys
                        Console.Write($"     Rotation Keys: {chan.RotationKeyCount}".PadRight(25));      // 
                        Console.WriteLine($"     Scaling  Keys: {chan.ScalingKeyCount}".PadRight(25));        // 
                    }
                }
                if (startupconsoleinfo)
                {
                    Console.WriteLine("\n");
                    Console.WriteLine("\n Ok so this is all gonna go into our model class basically as is kinda. frownzers i needed it like this after all.");
                }
                foreach (var anode in anim.NodeAnimationChannels)
                {
                    if (startupconsoleinfo && (targetNodeConsoleName == "" || targetNodeConsoleName == anode.NodeName))
                    {
                        Console.WriteLine($"   Channel {anode.NodeName}\n   (time is in animation ticks it shouldn't exceed anim.DurationInTicks {anim.DurationInTicks} or total duration in seconds: {anim.DurationInTicks / anim.TicksPerSecond})");        // the node name has to be used to tie this channel to the originally printed hierarchy.  node names must be unique.
                        Console.WriteLine($"     Position Keys: {anode.PositionKeyCount}");       // access via chan.PositionKeys

                        for (int j = 0; j < anode.PositionKeys.Count; j++)
                        {
                            var key = anode.PositionKeys[j];
                            if (startupconsoleinfo)
                                Console.WriteLine("       index[" + (j + "]").PadRight(5) + " Time: " + key.Time.ToString().PadRight(17) + " secs: " + (key.Time / anim.TicksPerSecond).ToStringTrimed() + "  Position: {" + key.Value.ToStringTrimed() + "}");
                        }
                        if (startupconsoleinfo)
                            Console.WriteLine($"     Rotation Keys: {anode.RotationKeyCount}");       // 
                        for (int j = 0; j < anode.RotationKeys.Count; j++)
                        {
                            var key = anode.RotationKeys[j];
                            if (startupconsoleinfo)
                                Console.WriteLine("       index[" + (j + "]").PadRight(5) + " Time: " + key.Time.ToStringTrimed() + " secs: " + (key.Time / anim.TicksPerSecond).ToStringTrimed() + "  QRotation: {" + key.Value.ToStringTrimed() + "}");
                        }
                        if (startupconsoleinfo)
                            Console.WriteLine($"     Scaling  Keys: {anode.ScalingKeyCount}");        // 
                        for (int j = 0; j < anode.ScalingKeys.Count; j++)
                        {
                            var key = anode.ScalingKeys[j];
                            if (startupconsoleinfo)
                                Console.WriteLine("       index[" + (j + "]").PadRight(5) + " Time: " + key.Time.ToStringTrimed() + " secs: " + (key.Time / anim.TicksPerSecond).ToStringTrimed() + "  Scaling: {" + key.Value.ToStringTrimed() + "}");
                        }
                    }
                }
            }
        }



        /*
       */

        //=============================================================================
        /// <summary> Can be removed later or disregarded this is mainly for debuging. </summary>
        public void GetMaterialsInfoForNow(RiggedModel model, Scene scene)
        {
            for (int mloop = 0; mloop < scene.Meshes.Count; mloop++)
            {
                Mesh mesh = scene.Meshes[mloop];

                if (startupconsoleinfo)
                {
                    Console.WriteLine(
                    "\n" + "__________________________" +
                    "\n" + "Scene.Meshes[" + mloop + "] " +
                    "\n" + "Mesh.Name: " + mesh.Name +
                    "\n" + " FaceCount: " + mesh.FaceCount +
                    "\n" + " VertexCount: " + mesh.VertexCount +
                    "\n" + " Normals.Count: " + mesh.Normals.Count +
                    "\n" + " BoneCount: " + mesh.BoneCount +
                    "\n" + " MaterialIndex: " + mesh.MaterialIndex
                    );
                    Console.WriteLine("  mesh.UVComponentCount.Length: " + mesh.UVComponentCount.Length);
                }
                for (int i = 0; i < mesh.UVComponentCount.Length; i++)
                {
                    int val = mesh.UVComponentCount[i];
                    if (startupconsoleinfo)
                        Console.WriteLine("     mesh.UVComponentCount[" + i + "] : int value: " + val);
                }
                var tcc = mesh.TextureCoordinateChannelCount;
                var tc = mesh.TextureCoordinateChannels;
                if (startupconsoleinfo)
                {
                    Console.WriteLine("  mesh.HasMeshAnimationAttachments: " + mesh.HasMeshAnimationAttachments);
                    Console.WriteLine("  mesh.TextureCoordinateChannelCount: " + mesh.TextureCoordinateChannelCount);
                    Console.WriteLine("  mesh.TextureCoordinateChannels.Length:" + mesh.TextureCoordinateChannels.Length);
                }
                for (int i = 0; i < mesh.TextureCoordinateChannels.Length; i++)
                {
                    var channel = mesh.TextureCoordinateChannels[i];
                    if (startupconsoleinfo)
                        Console.WriteLine("     mesh.TextureCoordinateChannels[" + i + "]  count " + channel.Count);
                    for (int j = 0; j < channel.Count; j++)
                    {
                        // holds uvs and shit i think
                        //Console.Write(" channel[" + j + "].Count: " + channel.Count);
                    }
                }         
                if (startupconsoleinfo)
                    Console.WriteLine();

                //// Uv
                //Console.WriteLine("");
                //var uvchannels = mesh.TextureCoordinateChannels;
                //for (int k = 0; k < uvchannels.Length; k++)
                //{
                //    var f = uvchannels[k];
                //    int loopIndex = 0;
                //    for (int j = 0; j < f.Count; j++)
                //    {
                //        var uv = f[j];
                //        v[loopIndex].TextureCoordinate = new Microsoft.Xna.Framework.Vector2(uv.X, uv.Y);
                //        loopIndex++;
                //    }
                //}
            }


            if (scene.HasTextures)
            {
                var texturescount = scene.TextureCount;
                var textures = scene.Textures;
                if (startupconsoleinfo)
                    Console.WriteLine("\nTextures " + " Count " + texturescount + "\n");
                for (int i = 0; i < textures.Count; i++)
                {
                    var name = textures[i];
                    if (startupconsoleinfo)
                        Console.WriteLine("Textures[" + i + "] " + name);
                }
            }
            else
            {
                if (startupconsoleinfo)
                    Console.WriteLine("\nTextures " + " None ");
            }

            if (scene.HasMaterials)
            {
                if (startupconsoleinfo)
                    Console.WriteLine("\nMaterials scene.MaterialCount " + scene.MaterialCount + "\n");
                for (int i = 0; i < scene.Materials.Count; i++)
                {
                    if (startupconsoleinfo)
                    {
                        Console.WriteLine();
                        Console.WriteLine("Material[" + i + "] ");
                        Console.WriteLine("Material[" + i + "].Name " + scene.Materials[i].Name);
                    }
                    var m = scene.Materials[i];
                    if (m.HasName)
                    {
                        if (startupconsoleinfo)
                            Console.Write(" Name: " + m.Name);
                    }
                    var t = m.GetAllMaterialTextures();
                    if (startupconsoleinfo)
                    {
                        Console.WriteLine("  GetAllMaterialTextures Length " + t.Length);
                        Console.WriteLine();
                    }
                    for (int j = 0; j < t.Length; j++)
                    {
                        var tindex = t[j].TextureIndex;
                        var toperation = t[j].Operation;
                        var ttype = t[j].TextureType.ToString();
                        var tfilepath = t[j].FilePath;
                        // J matches up to the texture coordinate channel uv count it looks like.
                        if (startupconsoleinfo)
                        {
                            Console.WriteLine("   Texture[" + j + "] " + "   Index:" + tindex + "   Type: " + ttype + "   Filepath: " + tfilepath);
                        }
                    }
                    if (startupconsoleinfo)
                        Console.WriteLine();

                    // added info
                    if (startupconsoleinfo)
                    {
                        Console.WriteLine("   Material[" + i + "] " + "  HasBlendMode:" + m.HasBlendMode + "  HasBumpScaling: " + m.HasBumpScaling + "  HasOpacity: " + m.HasOpacity + "  HasShadingMode: " + m.HasShadingMode + "  HasTwoSided: " + m.HasTwoSided + "  IsTwoSided: " + m.IsTwoSided);
                        Console.WriteLine("   Material[" + i + "] " + "  HasBlendMode:" + m.HasShininess + "  HasTextureDisplacement:" + m.HasTextureDisplacement + "  HasTextureEmissive:" + m.HasTextureEmissive + "  HasTextureReflection:" + m.HasTextureReflection);
                        Console.WriteLine("   Material[" + i + "] " + "  HasTextureReflection " + scene.Materials[i].HasTextureReflection + "  HasTextureLightMap " + scene.Materials[i].HasTextureLightMap + "  Reflectivity " + scene.Materials[i].Reflectivity);
                        Console.WriteLine("   Material[" + i + "] " + "  ColorAmbient:" + m.ColorAmbient + "  ColorDiffuse: " + m.ColorDiffuse + "  ColorSpecular: " + m.ColorSpecular);
                        Console.WriteLine("   Material[" + i + "] " + "  ColorReflective:" + m.ColorReflective + "  ColorEmissive: " + m.ColorEmissive + "  ColorTransparent: " + m.ColorTransparent);
                    }
                }
                if (startupconsoleinfo)
                    Console.WriteLine();
            }
            else
            {
                if (startupconsoleinfo)
                    Console.WriteLine("\n   No Materials Present. \n");
            }
        }

    }

    public class TempWeightVert
    {
        public int countOfBoneEntrysForThisVertice = 0;
        public List<float> verticesFlatBoneId = new List<float>();
        public List<int> verticeIndexs = new List<int>();
        public List<float> verticeBoneWeights = new List<float>();
    }

    public static class OpenAssimpToMgHelpers
    {
        //++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        // +++++++++++++++  functional helpers +++++++++++++++++++++++
        //++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++


        public static float CheckVal(float n)
        {
            if (float.IsNaN(n) || n == float.NaN || float.IsInfinity(n))
                return 0.0f;
            else
                return n;
        }

        public static Microsoft.Xna.Framework.Quaternion ToMg(this Assimp.Quaternion aq)
        {
            //return new Microsoft.Xna.Framework.Quaternion(aq.X, aq.Y, aq.Z, aq.W);
            var m = aq.GetMatrix();
            var n = m.ToMgTransposed();
            var q = Microsoft.Xna.Framework.Quaternion.CreateFromRotationMatrix(n);  //MatrixToQuaternion();
            return q;
        }

        public static Matrix ToMg(this Assimp.Matrix4x4 ma)
        {
            Matrix m = Matrix.Identity;
            m.M11 = CheckVal(ma.A1); m.M12 = CheckVal(ma.A2); m.M13 = CheckVal(ma.A3); m.M14 = CheckVal(ma.A4);
            m.M21 = CheckVal(ma.B1); m.M22 = CheckVal(ma.B2); m.M23 = CheckVal(ma.B3); m.M24 = CheckVal(ma.B4);
            m.M31 = CheckVal(ma.C1); m.M32 = CheckVal(ma.C2); m.M33 = CheckVal(ma.C3); m.M34 = CheckVal(ma.C4);
            m.M41 = CheckVal(ma.D1); m.M42 = CheckVal(ma.D2); m.M43 = CheckVal(ma.D3); m.M44 = CheckVal(ma.D4);
            return m;
        }
        public static Matrix ToMgTransposed(this Assimp.Matrix4x4 ma)
        {
            Matrix m = Matrix.Identity;
            m.M11 = CheckVal(ma.A1); m.M12 = CheckVal(ma.A2); m.M13 = CheckVal(ma.A3); m.M14 = CheckVal(ma.A4);
            m.M21 = CheckVal(ma.B1); m.M22 = CheckVal(ma.B2); m.M23 = CheckVal(ma.B3); m.M24 = CheckVal(ma.B4);
            m.M31 = CheckVal(ma.C1); m.M32 = CheckVal(ma.C2); m.M33 = CheckVal(ma.C3); m.M34 = CheckVal(ma.C4);
            m.M41 = CheckVal(ma.D1); m.M42 = CheckVal(ma.D2); m.M43 = CheckVal(ma.D3); m.M44 = CheckVal(ma.D4);
            m = Matrix.Transpose(m);
            return m;
        }
        public static Matrix ToMgTransposed(this Assimp.Matrix3x3 ma)
        {
            Matrix m = Matrix.Identity;
            ma.Transpose();
            m.M11 = CheckVal(ma.A1); m.M12 = CheckVal(ma.A2); m.M13 = CheckVal(ma.A3); m.M14 = 0;
            m.M21 = CheckVal(ma.B1); m.M22 = CheckVal(ma.B2); m.M23 = CheckVal(ma.B3); m.M24 = 0;
            m.M31 = CheckVal(ma.C1); m.M32 = CheckVal(ma.C2); m.M33 = CheckVal(ma.C3); m.M34 = 0;
            m.M41 = 0; m.M42 = 0; m.M43 = 0; m.M44 = 1;
            return m;
        }

        public static Color ToColor(this Vector4 v)
        {
            return new Color(v.X, v.Y, v.Z, v.W);
        }
        public static Vector4 ToVector4(this Color v)
        {
            return new Vector4(1f/v.R, 1f/v.G, 1f/v.B, 1f/v.A);
        }

        public static Vector3 ToMg(this Assimp.Vector3D v)
        {
            return new Vector3(v.X, v.Y, v.Z);
        }

        public static string ToStringTrimed(this Assimp.Vector3D v)
        {
            string d = "+0.00#;-0.00#"; // "0.00";
            int pamt = 8;
            return (v.X.ToString(d).PadRight(pamt) + ", " + v.Y.ToString(d).PadRight(pamt) + ", " + v.Z.ToString(d).PadRight(pamt));
        }
        public static string ToStringTrimed(this Assimp.Quaternion q)
        {
            string d = "+0.00#;-0.00#"; // "0.00";
            int pamt = 8;
            return ("x: " + q.X.ToString(d).PadRight(pamt) + "y: " + q.Y.ToString(d).PadRight(pamt) + "z: " + q.Z.ToString(d).PadRight(pamt) + "w: " + q.W.ToString(d).PadRight(pamt));
        }
        public static string ToStringTrimed(this int v)
        {
            string d = "+0.00#;-0.00#"; // "0.00";
            int pamt = 8;
            return (v.ToString(d).PadRight(pamt));
        }
        public static string ToStringTrimed(this float v)
        {
            string d = "+0.00#;-0.00#"; // "0.00";
            int pamt = 8;
            return (v.ToString(d).PadRight(pamt));
        }
        public static string ToStringTrimed(this double v)
        {
            string d = "+0.00#;-0.00#"; // "0.00";
            int pamt = 8;
            return (v.ToString(d).PadRight(pamt));
        }
        public static string ToStringTrimed(this Vector3 v)
        {
            string d = "+0.00#;-0.00#"; // "0.00";
            int pamt = 8;
            return (v.X.ToString(d).PadRight(pamt) + ", " + v.Y.ToString(d).PadRight(pamt) + ", " + v.Z.ToString(d).PadRight(pamt));
        }
        public static string ToStringTrimed(this Microsoft.Xna.Framework.Quaternion q)
        {
            string d = "+0.00#;-0.00#"; // "0.00";
            int pamt = 8;
            return ("x: " + q.X.ToString(d).PadRight(pamt) + "y: " + q.Y.ToString(d).PadRight(pamt) + "z: " + q.Z.ToString(d).PadRight(pamt) + "w: " + q.W.ToString(d).PadRight(pamt));
        }

    }
}
