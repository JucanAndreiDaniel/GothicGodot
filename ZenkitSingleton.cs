using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using ZenKit;
using ZenKit.Vobs;
using Material = Godot.Material;
using Mesh = Godot.Mesh;
using Texture = ZenKit.Texture;

[Tool]
public partial class ZenkitSingleton : Node
{
    ~ZenkitSingleton()
    {
        Dispose();
    }

    private ZenKit.Vfs Vfs { get; set; }

    public void MountVfs(string g1Dir)
    {
        Vfs = new Vfs();

        var fullPath = Path.GetFullPath(Path.Join(g1Dir, "Data"));

        var vfsPaths = Directory.GetFiles(fullPath, "*.VDF", SearchOption.AllDirectories);

        foreach (var path in vfsPaths)
        {
            Vfs.MountDisk(path, VfsOverwriteBehavior.Older);
        }
    }

#if TOOLS
    public void ExtendInspectorBegin(ExtendableInspector inspector)
    {
        Button button = new Button();
        button.Text = "Load World";
        button.Pressed += LoadWorld;
        inspector.AddCustomControl(button);
    }
#endif

    protected void LoadWorld()
    {
        // FIXME: dont use hardcoded path
        MountVfs("D:\\Games\\Gothic");
        var zkWorld = new ZenKit.World(Vfs, "world.zen");


        var subMeshes = CreateSubMeshesForUnity(zkWorld.Mesh, zkWorld.BspTree);

        //create a root node 

        var rootNode = GetNode("WorldRoot");

        foreach (var subMesh in subMeshes.Values)
        {
            if (subMesh.Vertices.Count == 0)
                continue;

            var gdMesh = new ArrayMesh();

            var array = new Godot.Collections.Array();
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();

            array.Resize((int)Mesh.ArrayType.Max);

            foreach (var vertex in subMesh.Vertices)
            {
                vertices.Add(ToGodotVector(vertex));
            }

            foreach (var normal in subMesh.Normals)
            {
                normals.Add(ToGodotVector(normal));
            }

            foreach (var uv in subMesh.Uvs)
            {
                uvs.Add(ToGodotVector(uv));
            }

            array[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
            array[(int)Mesh.ArrayType.Normal] = normals.ToArray();
            // array[(int)Mesh.ArrayType.Color] = colors.ToArray();
            array[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();

            gdMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, array);


            var mi = new MeshInstance3D();
            mi.Name = subMesh.Material.Name;
            var material = new StandardMaterial3D();
            material.AlbedoTexture = ToGodotImageTexture(subMesh.Material.Texture);
            material.SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled;
            material.MetallicSpecular = 0;
            // material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            if (subMesh.Material.Group == MaterialGroup.Water || material.AlbedoTexture.HasAlpha())
                material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;

            material.Uv1Scale = 100 * Vector3.One;
            mi.MaterialOverride = material;

            mi.Mesh = gdMesh;

            rootNode.AddChild(mi);
            mi.Owner = GetTree().EditedSceneRoot;
        }
    }

    private ZenKit.Texture LoadZenkitTexture(string path)
    {
        var lowerKey = path.ToLower();
        var extension = Path.GetExtension(lowerKey);

        var preparedKey = lowerKey;

        if (extension != string.Empty)
            preparedKey = preparedKey.Replace(extension, "");
        if (preparedKey == "")
            preparedKey = "default";
        return new Texture(Vfs, $"{preparedKey}-C.TEX");
    }

    private ImageTexture ToGodotImageTexture(string path)
    {
        var texture = LoadZenkitTexture(path);
        var image = new Image();

        switch (texture.Format)
        {
            case TextureFormat.Dxt1:
                image.SetData(texture.Width, texture.Height, false, Image.Format.Dxt1,
                    texture.GetMipmapRaw(0));
                break;
            case TextureFormat.Dxt3:
                image.SetData(texture.Width, texture.Height, false, Image.Format.Dxt3,
                    texture.GetMipmapRaw(0));
                break;
            default:
                image.SetData(texture.Width, texture.Height, false, Image.Format.Rgba8,
                    texture.GetMipmapRgba(0));
                image.GenerateMipmaps();
                break;
        }

        var imageTexture = new ImageTexture();
        imageTexture = ImageTexture.CreateFromImage(image);
        return imageTexture;
    }

    private static Dictionary<int, WorldData.SubMeshData> CreateSubMeshesForUnity(IMesh zkMesh, IBspTree zkBspTree)
    {
        var zkMaterials = zkMesh.Materials;
        var zkPolygons = zkMesh.Polygons;
        var zkPositions = zkMesh.Positions;
        var zkFeatures = zkMesh.Features;
        var zkLightmaps = zkMesh.LightMap;

        // As we know the exact size of SubMeshes (aka size of Materials), we will prefill them now.
        Dictionary<int, WorldData.SubMeshData> subMeshes = new(zkMaterials.Count);
        for (var materialIndex = 0; materialIndex < zkMaterials.Count; materialIndex++)
        {
            subMeshes.Add(materialIndex, new()
            {
                Material = zkMaterials[materialIndex],
            });
        }

        // LeafPolygonIndices aren't distinct. We therefore need to rearrange them this way.
        // Alternatively we could also loop through all Nodes and fetch where Front==Back==-1 (aka Leaf)
        foreach (var leafPolygonIndex in zkBspTree.LeafPolygonIndices.Distinct())
        {
            var polygon = zkPolygons[leafPolygonIndex];
            var currentSubMesh = subMeshes[polygon.MaterialIndex];
            //
            // if (polygon.IsPortal)
            //     continue;

            // As we always use element 0 and i+1, we skip it in the loop.
            for (var i = 1; i < polygon.PositionIndices.Count - 1; i++)
            {
                // Triangle Fan - We need to add element 0 (A) before every triangle 2 elements.
                AddEntry(zkPositions, zkFeatures, zkLightmaps, polygon, currentSubMesh, 0);
                AddEntry(zkPositions, zkFeatures, zkLightmaps, polygon, currentSubMesh, i);
                AddEntry(zkPositions, zkFeatures, zkLightmaps, polygon, currentSubMesh, i + 1);
            }
        }

        // To have easier to read code above, we reverse the arrays now at the end.
        // foreach (var subMesh in subMeshes)
        // {
        //     subMesh.Value.Vertices.Reverse();
        //     subMesh.Value.Uvs.Reverse();
        //     subMesh.Value.Normals.Reverse();
        // }

        return subMeshes;
    }

    private static void AddEntry(List<System.Numerics.Vector3> zkPositions, List<Vertex> features,
        List<ILightMap> lightMaps, IPolygon polygon, WorldData.SubMeshData currentSubMesh, int index)
    {
        // For every vertexIndex we store a new vertex. (i.e. no reuse of Vector3-vertices for later texture/uv attachment)
        var positionIndex = polygon.PositionIndices[index];
        currentSubMesh.Vertices.Add(zkPositions[(int)positionIndex]);

        // This triangle (index where Vector 3 lies inside vertices, points to the newly added vertex (Vector3) as we don't reuse vertices.
        currentSubMesh.Triangles.Add(currentSubMesh.Vertices.Count - 1);

        var featureIndex = polygon.FeatureIndices[index];
        var feature = features[(int)featureIndex];
        currentSubMesh.Uvs.Add(feature.Texture);
        currentSubMesh.Normals.Add(feature.Normal);

        if (polygon.LightMapIndex != -1)
        {
            currentSubMesh.LightMap = lightMaps[(int)polygon.LightMapIndex];
        }
    }


    private Vector3 ToGodotVector(System.Numerics.Vector3 vector3)
    {
        return new(vector3.X / 100, vector3.Y / 100, vector3.Z / 100);
    }

    private Vector2 ToGodotVector(System.Numerics.Vector2 vector3)
    {
        return new(vector3.X / 100, vector3.Y / 100);
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
    }
}

public class WorldData
{
    // We need to store it as we need the pointer to it for load+save of un-cached vobs.
    // ReSharper disable once NotAccessedField.Global
    public global::ZenKit.World World;
    public List<IVirtualObject> Vobs;
    public CachedWayNet WayNet;

    public Dictionary<int, SubMeshData> SubMeshes;

    public class SubMeshData
    {
        public IMaterial Material;

        public ILightMap
            LightMap =
                new CachedLightMap(); //we give it a default value so it doesnt crash at runtime when creating the world

        public readonly List<System.Numerics.Vector3> Vertices = new();
        public readonly List<int> Triangles = new();
        public readonly List<System.Numerics.Vector2> Uvs = new();
        public readonly List<System.Numerics.Vector3> Normals = new();
    }
}