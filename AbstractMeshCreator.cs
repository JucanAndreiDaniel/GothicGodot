using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;
// using GothicGodot.GVR.Caches;
using Microsoft.VisualBasic;
// using GVR.Caches;
// using GVR.Extensions;
// using GVR.Globals;
// using UnityEngine;
// using UnityEngine.Rendering;
using ZenKit;
using Material = ZenKit.Material;
// using Material = UnityEngine.Material;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Mesh = Godot.Mesh;

// using Mesh = ZenKit.Mesh;

// using Mesh = UnityEngine.Mesh;
// using TextureFormat = UnityEngine.TextureFormat;

namespace GothicGodot
{
    public abstract class AbstractMeshCreator
    {
        // Decals work only on URP shaders. We therefore temporarily change everything to this
        // until we know how to change specifics to the cutout only. (e.g. bushes)
        protected const float DecalOpacity = 0.75f;

        protected Node3D Create(string objectName, IModelMesh mdm, IModelHierarchy mdh, Vector3 position,
            Quaternion rotation, Node3D parent = null, Node3D rootGo = null)
        {
            rootGo ??= new Node3D(); // Create new object if it is a null-parameter until now.
            rootGo.Name = $"{objectName} {position}";
            parent.AddChild(rootGo);
            rootGo.Owner = parent.Owner;
            SetPosAndRot(rootGo, position, rotation);
            var nodeObjects = new MeshInstance3D[mdh.Nodes.Count];

            // Create empty Nodes from hierarchy
            {
                for (var i = 0; i < mdh.Nodes.Count; i++)
                {
                    var node = mdh.Nodes[i];
                    // We attached some Components to root of bones. Therefore reusing it.
                    if (node.Name.Equals("BIP01"))
                    {
                        var bip01 = rootGo.FindChild("BIP01", true);
                        if (bip01 != null)
                            nodeObjects[i] = rootGo.FindChild("BIP01", true) as MeshInstance3D;
                        else
                            nodeObjects[i] = new MeshInstance3D()
                            {
                                Name = mdh.Nodes[i].Name
                            };
                    }
                    else
                    {
                        nodeObjects[i] = new MeshInstance3D()
                        {
                            Name = mdh.Nodes[i].Name
                        };
                    }
                }

                // Now set parents
                for (var i = 0; i < mdh.Nodes.Count; i++)
                {
                    var node = mdh.Nodes[i];
                    var nodeObj = nodeObjects[i];

                    SetPosAndRot(nodeObj, node.Transform);

                    if (node.ParentIndex == -1)
                    {
                        rootGo.AddChild(nodeObj);
                        nodeObj.Owner = rootGo.Owner;
                    }
                    else
                    {
                        nodeObjects[node.ParentIndex].AddChild(nodeObj);
                        nodeObj.Owner = nodeObjects[node.ParentIndex].Owner;
                    }
                }

                for (var i = 0; i < nodeObjects.Length; i++)
                {
                    if (mdh.Nodes[i].ParentIndex == -1)
                        nodeObjects[i].Position = mdh.RootTranslation.ToGodotVector();
                    else
                        SetPosAndRot(nodeObjects[i], mdh.Nodes[i].Transform);
                }
            }

            //// Fill Nodes with Meshes from "original" Mesh
            var meshCounter = 0;
            foreach (var softSkinMesh in mdm.Meshes)
            {
                var mesh = softSkinMesh.Mesh;

                var meshObj = new MeshInstance3D();
                meshObj.Name = $"ZM_{meshCounter++}";
                rootGo.AddChild(meshObj);
                meshObj.Owner = rootGo.Owner;

                var arrayMesh = PrepareMeshFilter(softSkinMesh);
                PrepareMeshRenderer(arrayMesh, mesh);

                meshObj.Mesh = arrayMesh;

                // CreateBonesData(rootGo, nodeObjects, meshRenderer, softSkinMesh);
            }

            var attachments = GetFilteredAttachments(mdm.Attachments);

            // Fill Nodes with Meshes from attachments
            foreach (var subMesh in attachments)
            {
                // var meshObj = new MeshInstance3D();
                // meshObj.Name = subMesh.Key;
                // rootGo.AddChild(meshObj);
                // meshObj.Owner = rootGo.Owner;
                //
                var meshObj = nodeObjects.First(bone => bone.Name == subMesh.Key);

                var arrayMesh = PrepareMeshFilter(subMesh.Value);
                PrepareMeshRenderer(arrayMesh, subMesh.Value);


                meshObj.Mesh = arrayMesh;
                // var meshFilter = meshObj.AddComponent<MeshFilter>();
                // var meshRenderer = meshObj.AddComponent<MeshRenderer>();
                //
                // PrepareMeshRenderer(meshRenderer, subMesh.Value);
                // PrepareMeshFilter(meshFilter, subMesh.Value, false);
                // PrepareMeshCollider(meshObj, meshFilter.mesh, subMesh.Value.Materials);
            }

            // SetPosAndRot(rootGo, position, rotation);

            // We need to set the root translation after we add children above. Otherwise the "additive" position/rotation will be broken.
            // We need to reset the rootBones position to zero. Otherwise Vobs won't be placed right.
            // Due to Unity's parent-child transformation magic, we need to do it at the end. ╰(*°▽°*)╯
            // nodeObjects[0].transform.localPosition = Vector3.zero;

            return rootGo;
        }

        /// <summary>
        /// There are some objects (e.g. NPCs) where we want to skip specific attachments. This method can be overridden for this feature.
        /// </summary>
        protected virtual Dictionary<string, IMultiResolutionMesh> GetFilteredAttachments(
            Dictionary<string, IMultiResolutionMesh> attachments)
        {
            return attachments;
        }

        protected Node3D Create(string objectName, IMultiResolutionMesh mrm, Vector3 position, Quaternion rotation,
            bool withCollider, Node3D parent = null, MeshInstance3D rootGo = null)
        {
            if (mrm == null)
            {
                GD.Print("No mesh data was found for: " + objectName);
                return null;
            }

            // If there is no texture for any of the meshes, just skip this item.
            // G1: Some skull decorations are without texture.
            if (mrm.Materials.All(m => m.Texture == null))
                return null;

            rootGo ??= new MeshInstance3D();
            rootGo.Name = objectName;
            parent.AddChild(rootGo);
            rootGo.Owner = parent.Owner;
            SetPosAndRot(rootGo, position, rotation);

            var arrayMesh = PrepareMeshFilter(mrm, false);
            PrepareMeshRenderer(arrayMesh, mrm);

            rootGo.Mesh = arrayMesh;

            return rootGo;
        }

        protected void SetPosAndRot(Node3D obj, Vector3 position, Quaternion rotation)
        {
            SetPosAndRot(obj, new Transform3D(new Basis(rotation), position));
        }

        protected void SetPosAndRot(Node3D obj, Matrix4x4 matrix)
        {
            SetPosAndRot(obj, matrix.ToGodotProjection());
        }

        protected void SetPosAndRot(Node3D obj, Projection matrix)
        {
            var transform = new Transform3D(matrix);
            SetPosAndRot(obj, transform);
        }

        protected void SetPosAndRot(Node3D obj, Transform3D transform)
        {
            obj.Transform = transform;
        }

        protected void PrepareMeshRenderer(ArrayMesh mesh, IMultiResolutionMesh mrmData)
        {
            if (null == mrmData)
            {
                // GD.Print("No mesh data could be added to renderer: " +);
                return;
            }

            for (var index = 0; index < mrmData.SubMeshes.Count; index++)
            {
                var subMesh = mrmData.SubMeshes[index];
                var materialData = subMesh.Material;

                if (materialData.Texture.Length == 0)
                    continue;

                var texture = GetTexture(materialData.Texture);
                if (null == texture)
                {
                    if (materialData.Texture.ToUpperInvariant().EndsWith(".TGA"))
                    {
                        GD.Print("This is supposed to be a decal: " + materialData.Texture);
                    }
                    else
                    {
                        GD.Print("Couldn't get texture from name: " + materialData.Texture);
                    }
                }

                var material = new StandardMaterial3D();
                material.AlbedoTexture = texture;
                material.SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled;
                material.MetallicSpecular = 0;
                material.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
                material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;

                mesh.SurfaceSetMaterial(index, material);
            }

            // rend.SetMaterials(finalMaterials);
        }

        //
        protected ArrayMesh PrepareMeshFilter(IMultiResolutionMesh mrmData, bool isMorphMesh = false,
            string morphMeshName = "")
        {
            /*
             * Ok, brace yourself:
             * There are three parameters of interest when it comes to creating meshes for items (etc.).
             * 1. positions - Unity: vertices (=Vector3)
             * 2. triangles - contains 3 indices to wedges.
             * 3. wedges - contains indices (Unity: triangles) to the positions (Unity: vertices) and textures (Unity: uvs (=Vector2)).
             *
             * Data example:
             *  positions: 0=>[x1,x2,x3], 0=>[x2,y2,z2], 0=>[x3,y3,z3]
             *  submesh:
             *    triangles: [0, 2, 1], [1, 2, 3]
             *    wedges: 0=>[index=0, texture=...], 1=>[index=2, texture=...], 2=>[index=2, texture=...]
             *
             *  If we now take first triangle and prepare it for Unity, we would get the following:
             *  vertices = 0[x0,...], 2[x2,...], 1[x1,...] --> as triangles point to a wedge and wedge index points to the position-index itself.
             *  triangles = 0, 2, 3 --> (indices for position items); ATTENTION: index 3 would normally be index 2, but! we can never reuse positions. We always need to create new ones. (Reason: uvs demand the same size as vertices.)
             *  uvs = [wedge[0].texture], [wedge[2].texture], [wedge[1].texture]
             */
            var mesh = new ArrayMesh();

            var array = new Godot.Collections.Array();

            array.Resize((int)Mesh.ArrayType.Max);

            // if (isMorphMesh)
            // {
            //     // MorphMeshes will change the vertices. This call optimizes performance.
            //     mesh.MarkDynamic();
            //     MorphMeshCache.AddVertexMapping(morphMeshName, mrmData.PositionCount);
            //     morphMeshName =
            //         MorphMeshCache
            //             .GetPreparedKey(morphMeshName); // So we don't need to recalculate every Add() call later.
            // }

            // meshFilter.mesh = mesh;
            if (null == mrmData)
            {
                // GD.Print("No mesh data could be added to filter: " + meshFilter.transform.parent.name);
                return null;
            }

            // mesh.subMeshCount = mrmData.SubMeshes.Count;


            var vertices = mrmData.Positions;

            foreach (var subMesh in mrmData.SubMeshes)
            {
                var verticesAndUvSize = mrmData.SubMeshes.Sum(i => i.Triangles.Count) * 3;
                var preparedVertices = new List<Vector3>(verticesAndUvSize);
                var preparedUVs = new List<Vector2>(verticesAndUvSize);

                // 2-dimensional arrays (as there are segregated by submeshes)
                var preparedTriangles = new List<List<int>>(mrmData.SubMeshes.Count);

                var triangles = subMesh.Triangles;
                var wedges = subMesh.Wedges;

                // every triangle is attached to a new vertex.
                // Therefore new submesh triangles start referencing their vertices with an offset from previous runs.
                var verticesIndexOffset = preparedVertices.Count;

                var subMeshTriangles = new List<int>(triangles.Count * 3);
                for (var i = 0; i < triangles.Count; i++)
                {
                    // One triangle is made of 3 elements for Unity. We therefore need to prepare 3 elements within one loop.
                    var preparedIndex = i * 3 + verticesIndexOffset;

                    var index1 = wedges[triangles[i].Wedge0];
                    var index2 = wedges[triangles[i].Wedge1];
                    var index3 = wedges[triangles[i].Wedge2];

                    preparedVertices.Add(vertices[index1.Index].ToGodotVector());
                    preparedVertices.Add(vertices[index2.Index].ToGodotVector());
                    preparedVertices.Add(vertices[index3.Index].ToGodotVector());

                    subMeshTriangles.Add(preparedIndex);
                    subMeshTriangles.Add(preparedIndex + 1);
                    subMeshTriangles.Add(preparedIndex + 2);

                    // // We add mapping data to later reuse for IMorphAnimation samples
                    // if (isMorphMesh)
                    // {
                    //     MorphMeshCache.AddVertexMappingEntry(morphMeshName, index1.Index, preparedVertices.Count - 3);
                    //     MorphMeshCache.AddVertexMappingEntry(morphMeshName, index2.Index, preparedVertices.Count - 2);
                    //     MorphMeshCache.AddVertexMappingEntry(morphMeshName, index3.Index, preparedVertices.Count - 1);
                    // }


                    preparedUVs.Add(index1.Texture.ToGodotVector());
                    preparedUVs.Add(index2.Texture.ToGodotVector());
                    preparedUVs.Add(index3.Texture.ToGodotVector());
                }

                preparedTriangles.Add(subMeshTriangles);

                // preparedVertices.Reverse();
                // preparedUVs.Reverse();
                // preparedTriangles.SelectMany(i => i).Reverse();

                array[(int)Mesh.ArrayType.Index] = preparedTriangles.SelectMany(i => i).ToArray();
                array[(int)Mesh.ArrayType.Vertex] = preparedVertices.ToArray();
                array[(int)Mesh.ArrayType.TexUV] = preparedUVs.ToArray();

                mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, array);
            }

            // Unity 1/ handles vertices on mesh level, but triangles (aka vertex-indices) on submesh level.
            // and 2/ demands vertices to be stored before triangles/uvs.
            // Therefore we prepare the full data once and assign it afterwards.
            // @see: https://answers.unity.com/questions/531968/submesh-vertices.html


            return mesh;

            // if (isMorphMesh)
            // MorphMeshCache.SetUnityVerticesForVertexMapping(morphMeshName, preparedVertices.ToArray());
        }

        //
        //
        protected ArrayMesh PrepareMeshFilter(ISoftSkinMesh soft)
        {
            /*
             * Ok, brace yourself:
             * There are three parameters of interest when it comes to creating meshes for items (etc.).
             * 1. positions - Unity: vertices (=Vector3)
             * 2. triangles - contains 3 indices to wedges.
             * 3. wedges - contains indices (Unity: triangles) to the positions (Unity: vertices) and textures (Unity: uvs (=Vector2)).
             *
             * Data example:
             *  positions: 0=>[x1,x2,x3], 0=>[x2,y2,z2], 0=>[x3,y3,z3]
             *  submesh:
             *    triangles: [0, 2, 1], [1, 2, 3]
             *    wedges: 0=>[index=0, texture=...], 1=>[index=2, texture=...], 2=>[index=2, texture=...]
             *
             *  If we now take first triangle and prepare it for Unity, we would get the following:
             *  vertices = 0[x0,...], 2[x2,...], 1[x1,...] --> as triangles point to a wedge and wedge index points to the position-index itself.
             *  triangles = 0, 2, 3 --> (indices for position items); ATTENTION: index 3 would normally be index 2, but! we can never reuse positions. We always need to create new ones. (Reason: uvs demand the same size as vertices.)
             *  uvs = [wedge[0].texture], [wedge[2].texture], [wedge[1].texture]
             */
            var mesh = new ArrayMesh();

            var array = new Godot.Collections.Array();
            array.Resize((int)Mesh.ArrayType.Max);

            var zkMesh = soft.Mesh;
            var weights = soft.Weights;

            var verticesAndUvSize = zkMesh.SubMeshes.Sum(i => i.Triangles!.Count) * 3;
            var preparedVertices = new List<Vector3>(verticesAndUvSize);
            var preparedUVs = new List<Vector2>(verticesAndUvSize);
            var boneIndices = new List<int>(verticesAndUvSize);
            var boneWeights = new List<float>(verticesAndUvSize);
            var preparedBoneWeights = new Tuple<List<int>, List<float>>(boneIndices, boneWeights);

            // 2-dimensional arrays (as there are segregated by submeshes)
            var preparedTriangles = new List<List<int>>(zkMesh.SubMeshes.Count);

            foreach (var subMesh in zkMesh.SubMeshes)
            {
                var vertices = zkMesh.Positions;
                var triangles = subMesh.Triangles;
                var wedges = subMesh.Wedges;

                // every triangle is attached to a new vertex.
                // Therefore new submesh triangles start referencing their vertices with an offset from previous runs.
                var verticesIndexOffset = preparedVertices.Count;

                var subMeshTriangles = new List<int>(triangles.Count * 3);
                for (var i = 0; i < triangles.Count; i++)
                {
                    // One triangle is made of 3 elements for Unity. We therefore need to prepare 3 elements within one loop.
                    var preparedIndex = i * 3 + verticesIndexOffset;

                    var index1 = wedges![triangles[i].Wedge0];
                    var index2 = wedges[triangles[i].Wedge1];
                    var index3 = wedges[triangles[i].Wedge2];

                    preparedVertices.Add(vertices![index1.Index].ToGodotVector());
                    preparedVertices.Add(vertices[index2.Index].ToGodotVector());
                    preparedVertices.Add(vertices[index3.Index].ToGodotVector());

                    subMeshTriangles.Add(preparedIndex);
                    subMeshTriangles.Add(preparedIndex + 1);
                    subMeshTriangles.Add(preparedIndex + 2);

                    preparedUVs.Add(index1.Texture.ToGodotVector());
                    preparedUVs.Add(index2.Texture.ToGodotVector());
                    preparedUVs.Add(index3.Texture.ToGodotVector());

                    var weight1 = weights[index1.Index].ToBoneWeight(soft.Nodes);
                    var weight2 = weights[index2.Index].ToBoneWeight(soft.Nodes);
                    var weight3 = weights[index3.Index].ToBoneWeight(soft.Nodes);


                    preparedBoneWeights.Item1.AddRange(weight1.Item1);
                    preparedBoneWeights.Item2.AddRange(weight1.Item2);

                    preparedBoneWeights.Item1.AddRange(weight2.Item1);
                    preparedBoneWeights.Item2.AddRange(weight2.Item2);

                    preparedBoneWeights.Item1.AddRange(weight3.Item1);
                    preparedBoneWeights.Item2.AddRange(weight3.Item2);
                }

                preparedTriangles.Add(subMeshTriangles);


                // preparedVertices.Reverse();
                // preparedUVs.Reverse();
                // preparedTriangles.SelectMany(i => i).Reverse();

                array[(int)Mesh.ArrayType.Index] = preparedTriangles.SelectMany(i => i).ToArray();

                array[(int)Mesh.ArrayType.Vertex] = preparedVertices.ToArray();
                array[(int)Mesh.ArrayType.TexUV] = preparedUVs.ToArray();
                array[(int)Mesh.ArrayType.Bones] = preparedBoneWeights.Item1.ToArray();
                array[(int)Mesh.ArrayType.Weights] = preparedBoneWeights.Item2.ToArray();
                mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, array);
            }

            // Unity 1/ handles vertices on mesh level, but triangles (aka vertex-indices) on submesh level.
            // and 2/ demands vertices to be stored before triangles/uvs.
            // Therefore we prepare the full data once and assign it afterwards.
            // @see: https://answers.unity.com/questions/531968/submesh-vertices.html


            return mesh;
        }
        //
        // protected Collider PrepareMeshCollider(Node3D obj, Mesh mesh)
        // {
        //     var meshCollider = obj.AddComponent<MeshCollider>();
        //     meshCollider.sharedMesh = mesh;
        //     return meshCollider;
        // }
        //
        // /// <summary>
        // /// Check if Collider needs to be added.
        // /// </summary>
        // protected Collider PrepareMeshCollider(Node3D obj, Mesh mesh, IMaterial materialData)
        // {
        //     if (materialData.DisableCollision ||
        //         materialData.Group == MaterialGroup.Water)
        //     {
        //         // Do not add colliders
        //         return null;
        //     }
        //     else
        //     {
        //         return PrepareMeshCollider(obj, mesh);
        //     }
        // }
        //
        // /// <summary>
        // /// Check if Collider needs to be added.
        // /// </summary>
        // protected void PrepareMeshCollider(Node3D obj, Mesh mesh, List<IMaterial> materialDatas)
        // {
        //     var anythingDisableCollission = materialDatas.Any(i => i.DisableCollision);
        //     var anythingWater = materialDatas.Any(i => i.Group == MaterialGroup.Water);
        //
        //     if (anythingDisableCollission || anythingWater)
        //     {
        //         // Do not add colliders
        //     }
        //     else
        //     {
        //         PrepareMeshCollider(obj, mesh);
        //     }
        // }

        // /// <summary>
        // /// We basically only set the values from official Unity documentation. No added sugar for the bingPoses.
        // /// @see https://docs.unity3d.com/ScriptReference/Mesh-bindposes.html
        // /// @see https://forum.unity.com/threads/some-explanations-on-bindposes.86185/
        // /// </summary>
        // private void CreateBonesData(Node3D rootObj, Node3D[] nodeObjects, SkinnedMeshRenderer renderer, ISoftSkinMesh mesh)
        // {
        //     var meshBones = new Transform[mesh.Nodes.Count];
        //     var bindPoses = new UnityEngine.Matrix4x4[mesh.Nodes.Count];
        //
        //     for (var i = 0; i < mesh.Nodes.Count; i++)
        //     {
        //         var nodeIndex = mesh.Nodes[i];
        //
        //         meshBones[i] = nodeObjects[nodeIndex].transform;
        //         bindPoses[i] = meshBones[i].worldToLocalMatrix * rootObj.transform.localToWorldMatrix;
        //     }
        //
        //     renderer.sharedMesh.bindposes = bindPoses;
        //     renderer.bones = meshBones;
        // }

        protected virtual ImageTexture GetTexture(string name)
        {
            return AssetCache.TryGetTexture(name);
        }

        // protected Material GetDefaultMaterial(IMaterial zkMaterial, bool isAlphaTest)
        // {
        //     Shader shader;
        //     Material material;
        //     if (isAlphaTest)
        //     {
        //         shader = Constants.ShaderUnlitAlphaToCoverage;
        //         material = new Material(shader);
        //
        //         // Manually correct the render queue for alpha test, as Unity doesn't want to do it from the shader's render queue tag.
        //         material.renderQueue = (int)RenderQueue.AlphaTest;
        //
        //         return material;
        //     }
        //
        //     if (zkMaterial.Group == MaterialGroup.Water)
        //     {
        //         return GetWaterMaterial(zkMaterial);
        //     }
        //
        //     shader = Constants.ShaderUnlit;
        //     material = new Material(shader);
        //     switch (zkMaterial.AlphaFunction)
        //     {
        //         case AlphaFunction.Blend:
        //             material.ToTransparentMode();
        //             break;
        //         case AlphaFunction.Multiply:
        //             material.ToMulMode();
        //             break;
        //         case AlphaFunction.MultiplyAlt:
        //             material.ToMul2Mode();
        //             break;
        //         case AlphaFunction.Add:
        //         case AlphaFunction.Subtract:
        //             material.ToAdditiveMode();
        //             break;
        //         case AlphaFunction.Default:
        //         case AlphaFunction.None:
        //         default:
        //             material.ToOpaqueMode();
        //             break;
        //     }
        //
        //     return material;
        // }
        //
        // protected Material GetWaterMaterial(IMaterial materialData)
        // {
        //     var shader = Constants.ShaderWater;
        //     var material = new Material(shader);
        //
        //     if (materialData.TextureAnimationMapping != AnimationMapping.None)
        //     {
        //         material.SetVector("_Scroll", materialData.TextureAnimationMappingDirection.ToUnityVector() * 1000);
        //     }
        //
        //     material.ToTransparentMode();
        //
        //     return material;
        // }
        //
        // protected static bool IsTransparentShader(Shader shader)
        // {
        //     if (shader == null)
        //     {
        //         return false;
        //     }
        //
        //     return shader == Constants.ShaderUnlitAlphaToCoverage || shader == Constants.ShaderWater;
        // }
    }
}