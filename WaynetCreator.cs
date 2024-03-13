using Godot;
namespace GothicGodot;

using System.Collections.Generic;
using System.Linq;
using ZenKit;
using WayPoint = Vob.WayNet.WayPoint;

public static class WaynetCreator
{
    public static void Create(Node3D root, WorldData world)
    {
        var waynetObj = new Node3D()
        {
            Name = "WayNet"
        };
        root.AddChild(waynetObj);
        waynetObj.Owner = root.Owner;

        SetWayPointCache(world.WayNet);
        CreateWaypoints(waynetObj, world);
        CreateDijkstraWaypoints(world.WayNet);
        // CreateWaypointEdges(waynetObj, world);
    }

    private static void SetWayPointCache(IWayNet wayNet)
    {
        GameData.WayPoints.Clear();

        foreach (var wp in wayNet.Points)
        {
            GameData.WayPoints.Add(wp.Name, new WayPoint()
            {
                Name = wp.Name,
                Position = wp.Position.ToGodotVector(),
                Direction = wp.Direction.ToGodotVector()
            });
        }
    }

    private static void CreateDijkstraWaypoints(IWayNet wayNet)
    {
        CreateDijkstraWaypointEntries(wayNet);
        AttachWaypointPositionToDijkstraEntries();
        CalculateDijkstraNeighbourDistances();
    }

    private static void CreateDijkstraWaypointEntries(IWayNet wayNet)
    {
        Dictionary<string, DijkstraWaypoint> dijkstraWaypoints = new();
        var wayEdges = wayNet.Edges;
        var wayPoints = wayNet.Points;

        // Using LINQ to transform wayEdges into DijkstraWaypoints.
        dijkstraWaypoints = wayEdges.SelectMany(edge => new[]
            {
                // For each edge, create two entries: one for each direction of the edge.
                // 'a' is the source waypoint, 'b' is the destination waypoint.
                new { a = wayPoints[edge.A], b = wayPoints[edge.B] },
                new { a = wayPoints[edge.B], b = wayPoints[edge.A] }
            })
            .GroupBy(x => x.a.Name) // Group the entries by the name of the source waypoint.
            .ToDictionary(g => g.Key, g => new DijkstraWaypoint(g.Key) // Transform each group into a DijkstraWaypoint.
            {
                // The neighbors of the DijkstraWaypoint are the names of the destination waypoints in the group.
                Neighbors = g.Select(x => x.b.Name).ToList()
            });

        GameData.DijkstraWaypoints = dijkstraWaypoints;
    }

    private static void AttachWaypointPositionToDijkstraEntries()
    {
        foreach (var waypoint in GameData.DijkstraWaypoints)
        {
            var result = GameData.WayPoints.First(i => i.Key == waypoint.Key).Value.Position;
            waypoint.Value.Position = result;
        }
    }

    /// <summary>
    /// Needed for future calculations.
    /// </summary>
    private static void CalculateDijkstraNeighbourDistances()
    {
        foreach (var waypoint in GameData.DijkstraWaypoints.Values)
        {
            foreach (var neighbour in waypoint.Neighbors.Where(neighbour =>
                         !waypoint.DistanceToNeighbors.ContainsKey(neighbour)))
            {
                waypoint.DistanceToNeighbors.Add(neighbour,
                    waypoint.Position.DistanceTo(GameData.DijkstraWaypoints[neighbour].Position));
            }
        }
    }

    private static void CreateWaypoints(Node3D parent, WorldData world)
    {
        var waypointsObj = new Node3D()
        {
            Name = "Waypoints"
        };
        parent.AddChild(waypointsObj);
        waypointsObj.Owner = parent.Owner;

        foreach (var waypoint in world.WayNet.Points)
        {
            var wpObject = new Node3D();

            wpObject.Name = waypoint.Name;
            wpObject.Position = waypoint.Position.ToGodotVector();

            waypointsObj.AddChild(wpObject);
            wpObject.Owner = waypointsObj.Owner;
        }
    }

    // private static void CreateWaypointEdges(Node3D parent, WorldData world)
    // {
    //     // if (!FeatureFlags.I.drawWaypointEdges)
    //     // return;
    //
    //     var waypointEdgesObj = new Node3D(string.Format("Edges"));
    //     waypointEdgesObj.SetParent(parent);
    //
    //     for (var i = 0; i < world.WayNet.Edges.Count; i++)
    //     {
    //         var edge = world.WayNet.Edges[i];
    //         var startPos = world.WayNet.Points[(int)edge.A].Position.ToGodotVector();
    //         var endPos = world.WayNet.Points[(int)edge.B].Position.ToGodotVector();
    //         var lineObj = new Node3D();
    //
    //         lineObj.AddComponent<LineRenderer>();
    //         var lr = lineObj.GetComponent<LineRenderer>();
    //         lr.material = new Material(Constants.ShaderStandard);
    //         lr.startWidth = 0.1f;
    //         lr.endWidth = 0.1f;
    //         lr.SetPosition(0, startPos);
    //         lr.SetPosition(1, endPos);
    //
    //         lineObj.name = $"{edge.A}->{edge.B}";
    //         lineObj.transform.position = startPos;
    //         lineObj.transform.parent = waypointEdgesObj.transform;
    //     }
    // }
}