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
                                        | PostProcessSteps.OptimizeMeshes
                                        | PostProcessSteps.OptimizeGraph // normal
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
            //Console.WriteLine("\n@@@GetMaterialsInfoForNow");
            //GetMaterialsInfoForNow(model, scene);

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

                // tangents
                for (int k = 0; k < mesh.Tangents.Count; k++)
                {
                    var f = mesh.Tangents[k];
                    v[k].Tangent = new Vector3(f.X, f.Y, f.Z);
                }

                //// TODO need to add this to the vertex declaration or calculate it on the shader.
                //// bi tangents  
                //for (int k = 0; k < mesh.BiTangents.Count; k++)
                //{
                //    var f = mesh.BiTangents[k];
                //    v[k].BiTangent = ConvertAssimpMg.Vector3ConvertAssimpToMg(f.X, f.Y, f.Z);
                //}

                // A mesh may contain 0 to AI_MAX_NUMBER_OF_COLOR_SETS vertex colors per vertex. NULL if not present. Each array is mNumVertices in size if present. 
                // http://sir-kimmi.de/assimp/lib_html/structai_mesh.html#aa2807c7ba172115203ed16047ad65f9e

                ////// TODO colors dunno why there are lists of lists for colors 
                ////// maybe its multi colored or something ill have to read up on this before i load them.
                //////  This will have to be made from scratch need v4 to mg and other stuff
                //////
                ////for (int k = 0; k < mesh.VertexColorChannels[0].Count; k++)
                ////{
                ////    var f = mesh.VertexColorChannels[k];
                ////    v[k].Color = ConvertAssimpMg.Vector3ConvertAssimpToMg(f.X, f.Y, f.Z);
                ////}
                ///


                // example from some dudes thing ok so i might have to actually get the texture id from within the uv ? does that make sense ? no !
                //// Add textures
                //if (assimpMesh.TextureCoordsChannelCount > 0)
                //{
                //    for (int localIndex = 0, i = 0; i < assimpMesh.TextureCoordsChannelCount; i++)
                //    {
                //        if (assimpMesh.HasTextureCoords(i))
                //        {
                //            var uvCount = assimpMesh.GetUVComponentCount(i);

                //            if (uvCount == 2)
                //            {
                //                layout.Add(VertexElement.TextureCoordinate(localIndex, Format.R32G32_Float, vertexBufferElementSize));
                //                vertexBufferElementSize += Utilities.SizeOf<SharpDX.Vector2>();
                //            }
                //            else if (uvCount == 3)
                //            {
                //                layout.Add(VertexElement.TextureCoordinate(localIndex, Format.R32G32B32_Float, vertexBufferElementSize));
                //                vertexBufferElementSize += Utilities.SizeOf<SharpDX.Vector3>();
                //            }
                //            else
                //            {
                //                throw new InvalidOperationException("Unexpected uv count");
                //            }

                //            localIndex++;
                //        }
                //    }
                //}


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

        ////=============================================================================
        ///// <summary> Can be removed later or disregarded this is mainly for debuging. </summary>
        //public void GetMaterialsInfoForNow(AssimpBasedModel model, Scene scene)
        //{
        //    for (int mloop = 0; mloop < scene.Meshes.Count; mloop++)
        //    {
        //        Mesh mesh = scene.Meshes[mloop];

        //        if (startupconsoleinfo)
        //        {
        //            Console.WriteLine(
        //            "\n" + "__________________________" +
        //            "\n" + "Scene.Meshes[" + mloop + "] " +
        //            "\n" + "Mesh.Name: " + mesh.Name +
        //            "\n" + " FaceCount: " + mesh.FaceCount +
        //            "\n" + " VertexCount: " + mesh.VertexCount +
        //            "\n" + " Normals.Count: " + mesh.Normals.Count +
        //            "\n" + " BoneCount: " + mesh.BoneCount +
        //            "\n" + " MaterialIndex: " + mesh.MaterialIndex
        //            );
        //            Console.WriteLine("  mesh.UVComponentCount.Length: " + mesh.UVComponentCount.Length);
        //        }
        //        for (int i = 0; i < mesh.UVComponentCount.Length; i++)
        //        {
        //            int val = mesh.UVComponentCount[i];
        //            if (startupconsoleinfo)
        //                Console.WriteLine("     mesh.UVComponentCount[" + i + "] : int value: " + val);
        //        }
        //        var tcc = mesh.TextureCoordinateChannelCount;
        //        var tc = mesh.TextureCoordinateChannels;
        //        if (startupconsoleinfo)
        //        {
        //            Console.WriteLine("  mesh.TextureCoordinateChannelCount: " + mesh.TextureCoordinateChannelCount);
        //            Console.WriteLine("  mesh.TextureCoordinateChannels.Length:" + mesh.TextureCoordinateChannels.Length);
        //        }
        //        for (int i = 0; i < mesh.TextureCoordinateChannels.Length; i++)
        //        {
        //            var channel = mesh.TextureCoordinateChannels[i];
        //            if (startupconsoleinfo)
        //                Console.WriteLine("     mesh.TextureCoordinateChannels[" + i + "]  count " + channel.Count);
        //            for (int j = 0; j < channel.Count; j++)
        //            {
        //                // holds uvs and shit i think
        //                //Console.Write(" channel[" + j + "].Count: " + channel.Count);
        //            }
        //        }
        //        if (startupconsoleinfo)
        //            Console.WriteLine();
        //    }

        //    //// Uv
        //    //Console.WriteLine("");
        //    //var uvchannels = mesh.TextureCoordinateChannels;
        //    //for (int k = 0; k < uvchannels.Length; k++)
        //    //{
        //    //    var f = uvchannels[k];
        //    //    int loopIndex = 0;
        //    //    for (int j = 0; j < f.Count; j++)
        //    //    {
        //    //        var uv = f[j];
        //    //        v[loopIndex].TextureCoordinate = new Microsoft.Xna.Framework.Vector2(uv.X, uv.Y);
        //    //        loopIndex++;
        //    //    }
        //    //}

        //    if (scene.HasTextures)
        //    {
        //        var texturescount = scene.TextureCount;
        //        var textures = scene.Textures;
        //        if (startupconsoleinfo)
        //            Console.WriteLine("\nTextures " + " Count " + texturescount + "\n");
        //        for (int i = 0; i < textures.Count; i++)
        //        {
        //            var name = textures[i];
        //            if (startupconsoleinfo)
        //                Console.WriteLine("Textures[" + i + "] " + name);
        //        }
        //    }
        //    else
        //    {
        //        if (startupconsoleinfo)
        //            Console.WriteLine("\nTextures " + " None ");
        //    }

        //    if (startupconsoleinfo)
        //        Console.WriteLine("\nMaterials scene.MaterialCount " + scene.MaterialCount + "\n");
        //    for (int i = 0; i < scene.Materials.Count; i++)
        //    {
        //        if (startupconsoleinfo)
        //        {
        //            Console.WriteLine();
        //            Console.WriteLine("Material[" + i + "] ");
        //        }
        //        var m = scene.Materials[i];
        //        if (m.HasName)
        //        {
        //            if (startupconsoleinfo)
        //                Console.Write(" Name: " + m.Name);
        //        }
        //        var t = m.GetAllMaterialTextures();
        //        if (startupconsoleinfo)
        //        {
        //            Console.WriteLine("  GetAllMaterialTextures Length " + t.Length);
        //            Console.WriteLine();
        //        }
        //        for (int j = 0; j < t.Length; j++)
        //        {
        //            var tindex = t[j].TextureIndex;
        //            var toperation = t[j].Operation;
        //            var ttype = t[j].TextureType.ToString();
        //            var tfilepath = t[j].FilePath;
        //            // j matches up to the texture coordinate channel uv count it looks like 
        //            if (startupconsoleinfo)
        //                Console.WriteLine("   Texture[" + j + "] " + "   Index:" + tindex + "   Type: " + ttype + "   Filepath: " + tfilepath);
        //        }
        //        if (startupconsoleinfo)
        //            Console.WriteLine();
        //    }
        //    if (startupconsoleinfo)
        //        Console.WriteLine();
        //}
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
