﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Assimp;                               // note: install AssimpNET 4.1 via nuget


//  TODO 
// Fix the amimations there is a small discrepancy between my animations and visual studios model viewer which also isn't perfect.
// link the textures to the shader in the model 
//  b) add in the normal mapping to the shader.
//  c) add in heightmapping humm i never actually got around to writing one yet so that will need a seperate test project.
// Improve the draw method so it draws itself better.
// Maybe stick the effect into the model.


/// <summary>
/// 
/// </summary>
namespace AssimpLoaderExample
{
    /// <summary>
    /// The rigged model
    /// </summary>
    public class RiggedModel
    {
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

        #region Region animation stuff

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
            //currentFrame = 0;
            animationRunning = true;
        }

        public void StopAnimation()
        {
            //currentFrame = 0;
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
    /// The meshes of the model.
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
        /// Defines the minimum vertices in each direction x y z in system coordinates.
        /// </summary>
        public Vector3 Min { get; set; }
        /// <summary>
        /// Defines the mximum vertices in each direction x y z in system coordinates.
        /// </summary>
        public Vector3 Max { get; set; }
        /// <summary>
        /// Defines the center mass point or average of all the vertices.
        /// </summary>
        public Vector3 Centroid { get; set; }
    }

    /// <summary>
    /// A node of the models skeleton a rigged model has nodes some are bones some aren't.
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
    /// Animations
    /// </summary>
    public class RiggedAnimation
    {
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
                    var frameTime = j * SecondsPerFrame + .0001d; 
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
            var nodeAnim = n;
            // 
            Microsoft.Xna.Framework.Quaternion q1 = nodeAnim.qrot[0];
            Vector3 p1 = nodeAnim.position[0];
            Vector3 s1 = nodeAnim.scale[0];
            // 
            double tq1 = nodeAnim.qrotTime[0]; // =0;
            double tp1 = nodeAnim.positionTime[0]; ; // =0;
            double ts1 = nodeAnim.scaleTime[0]; //=0;
            // 
            int qindex1 = 0; int qindex2 = 0; // for output to console only
            int pindex1 = 0; int pindex2 = 0; // for output to console only
            // 
            int i2 = 0;
            if (nodeAnim.qrot.Count > 1)
            {
                i2 = 1;
            }
            Microsoft.Xna.Framework.Quaternion q2 = nodeAnim.qrot[i2];
            double tq2 = nodeAnim.qrotTime[i2];

            i2 = 0;
            if (nodeAnim.position.Count > 1)
            {
                i2 = 1;
            }
            Vector3 p2 = nodeAnim.position[i2];
            double tp2 = nodeAnim.positionTime[i2];

            i2 = 0;
            if (nodeAnim.scale.Count > 1)
            {
                i2 = 1;
            }
            Vector3 s2 = nodeAnim.scale[i2];
            double ts2 = nodeAnim.scaleTime[i2];

            //
            for (int frame = 0; frame < nodeAnim.qrot.Count; frame++)
            {
                var t = nodeAnim.qrotTime[frame];
                if (t <= animTime)
                {
                    //1___
                    q1 = nodeAnim.qrot[frame];
                    tq1 = t;
                    qindex1 = frame; // for output to console only
                    //2___
                    var frame2 = frame;
                    if (nodeAnim.qrot.Count > 1)
                    {
                        frame2 += 1;
                    }
                    if (frame2 >= nodeAnim.qrot.Count)
                    {
                        frame2 = 0;
                        q2 = nodeAnim.qrot[frame2];
                        tq2 = nodeAnim.qrotTime[frame2] + DurationInSeconds;
                        qindex2 = frame2; // for output to console only
                    }
                    else
                    {
                        q2 = nodeAnim.qrot[frame2];
                        tq2 = nodeAnim.qrotTime[frame2];
                        qindex2 = frame2; // for output to console only
                    }
                }
            }
            //
            for (int frame = 0; frame < nodeAnim.position.Count; frame++)
            {
                var t = nodeAnim.positionTime[frame];
                if (t <= animTime)
                {
                    //1___
                    p1 = nodeAnim.position[frame];
                    tp1 = t;
                    pindex1 = frame; // for output to console only
                    //2___
                    var frame2 = frame;
                    if (nodeAnim.position.Count > 1)
                    {
                        frame2 += 1;
                    }
                    if (frame2 >= nodeAnim.position.Count)
                    {
                        frame2 = 0;
                        p2 = nodeAnim.position[frame2];
                        tp2 = nodeAnim.positionTime[frame2] + DurationInSeconds;
                        pindex2 = frame2; // for output to console only
                    }
                    else
                    {
                        p2 = nodeAnim.position[frame2];
                        tp2 = nodeAnim.positionTime[frame2];
                        pindex2 = frame2; // for output to console only
                    }
                }
            }

            Microsoft.Xna.Framework.Quaternion q;
            if (tq1 != tq2)
                q = Microsoft.Xna.Framework.Quaternion.Slerp(q1, q2, (float)GetInterpolationTimeRatio(tq1, tq2, animTime));
            else
                q = q1;

            Vector3 p;
            if (tp1 != tp2)
                p = Vector3.Lerp(p1, p2, (float)GetInterpolationTimeRatio(tp1, tp2, animTime));
            else
                p = p1;

            Console.WriteLine("" + " animTime: " + animTime);
            Console.WriteLine(" q : " + " index1: " + qindex1 + " index2: " + qindex2 + " time1: " + tq1 + "  time2: " + tq2 + "  quaternion: " + q.ToString());
            Console.WriteLine(" p : " + " index1: " + pindex1 + " index2: " + pindex2 + " time1: " + tp1 + "  time2: " + tp2 + "  position: " + p.ToString());

            var r = Matrix.CreateFromQuaternion(q);
            r.Translation = p;

            return r; // * Matrix.CreateTranslation(p); //  * Matrix.CreateScale(s);
        }

        public double GetInterpolationTimeRatio(double s, double e, double val)
        {
            if (e < s)
                s = s + e;
            return (val - s) / (e - s);
        }
    }

    /// <summary>
    /// Each node contains lists for Animation frame orientations.
    /// </summary>
    public class RiggedAnimationNodes
    {
        public RiggedModelNode nodeRef;
        public string nodeName = "";
        // in model tick time
        public List<double> positionTime = new List<double>();
        public List<double> scaleTime = new List<double>();
        public List<double> qrotTime = new List<double>();
        public List<Vector3> position = new List<Vector3>();
        public List<Vector3> scale = new List<Vector3>();
        public List<Microsoft.Xna.Framework.Quaternion> qrot = new List<Microsoft.Xna.Framework.Quaternion>();

        // the actual calculated interpolation orientation matrice based on time.
        public double[] frameOrientationTimes;
        public Matrix[] frameOrientations;
    }

    }
    // vertex structure.

    /// <summary>
    /// basically a wide spectrum vertice structure.
    /// </summary>
    public struct VertexPositionTextureNormalTangentWeights : IVertexType
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 TextureCoordinate;
        public Vector3 Tangent;
        public Vector4 BlendIndices;
        public Vector4 BlendWeights;

        public static VertexDeclaration VertexDeclaration = new VertexDeclaration
        (
              new VertexElement(VertexElementByteOffset.PositionStartOffset(), VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
              new VertexElement(VertexElementByteOffset.OffsetVector3(), VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
              new VertexElement(VertexElementByteOffset.OffsetVector2(), VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
              new VertexElement(VertexElementByteOffset.OffsetVector3(), VertexElementFormat.Vector3, VertexElementUsage.Normal, 1),
              new VertexElement(VertexElementByteOffset.OffsetVector4(), VertexElementFormat.Vector4, VertexElementUsage.BlendIndices, 0),
              new VertexElement(VertexElementByteOffset.OffsetVector4(), VertexElementFormat.Vector4, VertexElementUsage.BlendWeight, 0)
        );
        VertexDeclaration IVertexType.VertexDeclaration { get { return VertexDeclaration; } }
    }
    /// <summary>
    /// This is a helper struct for tallying byte offsets
    /// </summary>
    public struct VertexElementByteOffset
    {
        public static int currentByteSize = 0;
        //[STAThread]
        public static int PositionStartOffset() { currentByteSize = 0; var s = sizeof(float) * 3; currentByteSize += s; return currentByteSize - s; }
        public static int Offset(int n) { var s = sizeof(int); currentByteSize += s; return currentByteSize - s; }
        public static int Offset(float n) { var s = sizeof(float); currentByteSize += s; return currentByteSize - s; }
        public static int Offset(Vector2 n) { var s = sizeof(float) * 2; currentByteSize += s; return currentByteSize - s; }
        public static int Offset(Color n) { var s = sizeof(int); currentByteSize += s; return currentByteSize - s; }
        public static int Offset(Vector3 n) { var s = sizeof(float) * 3; currentByteSize += s; return currentByteSize - s; }
        public static int Offset(Vector4 n) { var s = sizeof(float) * 4; currentByteSize += s; return currentByteSize - s; }

        public static int OffsetInt() { var s = sizeof(int); currentByteSize += s; return currentByteSize - s; }
        public static int OffsetFloat() { var s = sizeof(float); currentByteSize += s; return currentByteSize - s; }
        public static int OffsetColor() { var s = sizeof(int); currentByteSize += s; return currentByteSize - s; }
        public static int OffsetVector2() { var s = sizeof(float) * 2; currentByteSize += s; return currentByteSize - s; }
        public static int OffsetVector3() { var s = sizeof(float) * 3; currentByteSize += s; return currentByteSize - s; }
        public static int OffsetVector4() { var s = sizeof(float) * 4; currentByteSize += s; return currentByteSize - s; }
    }

}

