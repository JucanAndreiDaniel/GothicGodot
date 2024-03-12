using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using GothicGodot;
using ZenKit;
using ZenKit.Vobs;
using Mesh = Godot.Mesh;
using Texture = ZenKit.Texture;

[Tool]
public partial class ZenkitSingleton : Node
{
    [Export] private string G1Dir { get; set; }
    [Export] private string worldName { get; set; }
    [Export] private bool SkipPortals { get; set; }

    private Dictionary<string, ImageTexture> textureCache = new();

    private bool isLoaded = false;

    public void MountVfs(string g1Dir)
    {
        GameData.Vfs = new Vfs();

        var fullPath = Path.GetFullPath(Path.Join(g1Dir, "Data"));

        var vfsVDFPaths = Directory.GetFiles(fullPath, "*.VDF", SearchOption.AllDirectories);
        var vfsMODPaths = Directory.GetFiles(fullPath, "*.MOD", SearchOption.AllDirectories);

        foreach (var path in vfsVDFPaths)
        {
            GameData.Vfs.MountDisk(path, VfsOverwriteBehavior.Older);
        }

        foreach (var path in vfsMODPaths)
        {
            GameData.Vfs.MountDisk(path, VfsOverwriteBehavior.Older);
        }
    }

#if TOOLS
    public void ExtendInspectorBegin(ExtendableInspector inspector)
    {
        Button button = new Button();
        button.Text = "Load World";
        button.Pressed += ProcessWorld;
        inspector.AddCustomControl(button);

        Button button2 = new Button();
        button2.Text = "Unload World";
        button2.Pressed += ResetWorld;
        inspector.AddCustomControl(button2);
    }
#endif

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;

        if (G1Dir == "")
            G1Dir = "D:\\games\\gothic";
        if (worldName == "")
            worldName = "world";
        ProcessWorld();
    }

    private void ResetWorld()
    {
        var rootNode = GetNode("WorldRoot");

        foreach (var child in rootNode.GetChildren())
        {
            rootNode.RemoveChild(child);
            child.QueueFree();
        }
    }

    private async void ProcessWorld()
    {
        GD.Print("Loading World");
        MountVfs(G1Dir);
        GameData.World = LoadWorld();

        var rootNode = GetNode("WorldRoot");

        var teleportRootNode = new Node3D()
        {
            Name = "TeleportRoot"
        };
        rootNode.AddChild(teleportRootNode);
        var nonTeleportRootNode = new Node3D()
        {
            Name = "NonTeleportRoot"
        };
        rootNode.AddChild(nonTeleportRootNode);

        teleportRootNode.Owner = GetTree().EditedSceneRoot;
        nonTeleportRootNode.Owner = GetTree().EditedSceneRoot;

        await WorldMeshCreator.CreateAsync(GameData.World, teleportRootNode, 100);
        await VobCreator.CreateAsync(teleportRootNode, nonTeleportRootNode, GameData.World, 100);
    }

    private WorldData LoadWorld()
    {
        var worldToLoad = worldName.ToUpperInvariant().Contains(".zen") ? worldName : $"{worldName}.zen";
        var zkWorld = new World(GameData.Vfs, worldToLoad);
        var zkMesh = zkWorld.Mesh.Cache();
        var zkBspTree = zkWorld.BspTree.Cache();
        var zkWayNet = zkWorld.WayNet.Cache();

        var subMeshes = CreateSubMeshesForGodot(zkMesh, zkBspTree);

        return new WorldData
        {
            World = zkWorld,
            Vobs = zkWorld.RootObjects,
            WayNet = (CachedWayNet)zkWayNet,
            SubMeshes = subMeshes
        };
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
        return new Texture(GameData.Vfs, $"{preparedKey}-C.TEX");
    }


    private Dictionary<int, WorldData.SubMeshData> CreateSubMeshesForGodot(IMesh zkMesh, IBspTree zkBspTree)
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

            if (polygon.IsPortal && SkipPortals)
                continue;

            // As we always use element 0 and i+1, we skip it in the loop.
            for (var i = 1; i < polygon.PositionIndices.Count - 1; i++)
            {
                // Triangle Fan - We need to add element 0 (A) before every triangle 2 elements.
                AddEntry(zkPositions, zkFeatures, zkLightmaps, polygon, currentSubMesh, 0);
                AddEntry(zkPositions, zkFeatures, zkLightmaps, polygon, currentSubMesh, i + 1);
                AddEntry(zkPositions, zkFeatures, zkLightmaps, polygon, currentSubMesh, i);
            }

        }

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