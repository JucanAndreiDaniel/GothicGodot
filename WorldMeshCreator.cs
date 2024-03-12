using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Godot;
// using GVR.Caches;
// using GVR.Extensions;
// using GVR.Manager;
// using GVR.World;
// using UnityEditor;
// using UnityEngine;
using ZenKit;
// using Material = ZenKit.Material;
using Mesh = Godot.Mesh;

// using Material = UnityEngine.Material;
// using Mesh = UnityEngine.Mesh;
// using TextureFormat = UnityEngine.TextureFormat;

namespace GothicGodot
{
    public class WorldMeshCreator : AbstractMeshCreator
    {
        // As we subclass the main Mesh Creator, we need to have a parent-child inheritance instance.
        // Needed e.g. for overwriting PrepareMeshRenderer() to change specific behaviour.
        private static readonly WorldMeshCreator Self = new();

        public static async Task CreateAsync(WorldData world, Node3D parent, int meshesPerFrame)
        {
            var meshObj = new Node3D()
            {
                Name = "Mesh",
            };
            parent.AddChild(meshObj);
            meshObj.Owner = parent.Owner;

            // Track the progress of each sub-mesh creation separately
            int numSubMeshes = world.SubMeshes.Values.Count;
            int meshesCreated = 0;

            foreach (var subMesh in world.SubMeshes.Values)
            {
                // No texture to add.
                // For G1 this is: material.name == [KEINE, KEINETEXTUREN, DEFAULT, BRETT2, BRETT1, SUMPFWAASER, S:PSIT01_ABODEN]
                // Removing these removes tiny slices of walls on the ground. If anyone finds them, I owe them a beer. ;-)
                if (subMesh.Material.Texture.Length == 0 || subMesh.Triangles.Count == 0)
                    continue;

                // var arrayMesh = Self.PrepareMeshFilter(subMesh);
                // Self.PrepareMeshRenderer(arrayMesh, subMesh);

                var subMeshObj = new MeshInstance3D()
                {
                    Name = subMesh.Material.Name,
                };

                meshObj.AddChild(subMeshObj);
                subMeshObj.Owner = meshObj.Owner;
                
                var gdMesh = new ArrayMesh();
                
                var array = new Godot.Collections.Array();
                
                array.Resize((int)Mesh.ArrayType.Max);
                
                array[(int)Mesh.ArrayType.Vertex] = subMesh.Vertices.Select(x => x.ToGodotVector()).ToArray();
                array[(int)Mesh.ArrayType.Normal] = subMesh.Normals.Select(x => x.ToGodotVector()).ToArray();
                array[(int)Mesh.ArrayType.TexUV] = subMesh.Uvs.Select(x => x.ToGodotVector()).ToArray();
                
                gdMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, array);
                
                var material = new StandardMaterial3D();
                material.AlbedoTexture = AssetCache.TryGetTexture(subMesh.Material.Texture);
                material.SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled;
                material.MetallicSpecular = 0;
                
                // material.Uv1Scale = 100 * Vector3.One;
                subMeshObj.MaterialOverride = material;
                //
                subMeshObj.Mesh = gdMesh;

                if (++meshesCreated % meshesPerFrame == 0)
                    await Task.Yield(); // Yield to allow other operations to run in the frame
            }
        }

        protected void PrepareMeshRenderer(ArrayMesh mesh, WorldData.SubMeshData subMesh)
        {
            var materialData = subMesh.Material;

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
            // material.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;

            mesh.SurfaceSetMaterial(0, material);
        }

        private ArrayMesh PrepareMeshFilter(WorldData.SubMeshData subMesh)
        {
            var mesh = new ArrayMesh();

            var array = new Godot.Collections.Array();
            array.Resize((int)Mesh.ArrayType.Max);

            array[(int)Mesh.ArrayType.Index] = subMesh.Triangles.ToArray();
            array[(int)Mesh.ArrayType.Vertex] = subMesh.Vertices.Select(x => x.ToGodotVector()).ToArray();
            array[(int)Mesh.ArrayType.TexUV] = subMesh.Uvs.Select(x => x.ToGodotVector()).ToArray();

            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, array);

            return mesh;
        }
    }
}