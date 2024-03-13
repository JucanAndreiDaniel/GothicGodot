// using GVR.Creator.Meshes;
// using GVR.Vm;
// using UnityEngine;

using Godot;
using ZenKit;
using ZenKit.Daedalus;
using ZenKit.Vobs;

namespace GothicGodot
{
    /// <summary>
    /// We leverage Facade pattern to ensure:
    ///   1. A common interface for Mesh creations
    ///   2. Instance handling of the Builder itself. (Using static instances to have function override capabilities)
    /// 
    /// @see Builder Pattern reference: https://refactoring.guru/design-patterns/facade
    /// </summary>
    public static class MeshCreatorFacade
    {
        private static readonly VobMeshCreator VobMeshCreator = new();

        // public static void CreateNpc(string npcName, string mdmName, string mdhName,
        //     VmGothicExternals.ExtSetVisualBodyData bodyData, Node3D root)
        // {
        //     NpcMeshCreator.CreateNpc(npcName, mdmName, mdhName, bodyData, root);
        // }
        //
        // public static void EquipNpcWeapon(Node3D npcGo, ItemInstance itemData, VmGothicEnums.ItemFlags mainFlag,
        //     VmGothicEnums.ItemFlags flags)
        // {
        //     NpcMeshCreator.CreateNpcWeapon(npcGo, itemData, mainFlag, flags);
        // }

        public static Node3D CreateVob(string objectName, IMultiResolutionMesh mrm, Vector3 position,
            Quaternion rotation, bool withCollider, Node3D parent = null, MeshInstance3D rootGo = null)
        {
            return VobMeshCreator.CreateVob(objectName, mrm, position, rotation, withCollider, parent, rootGo);
        }

        public static Node3D CreateVob(string objectName, IModel mdl, Vector3 position, Quaternion rotation,
            Node3D parent = null, Node3D rootGo = null)
        {
            return VobMeshCreator.CreateVob(objectName, mdl, position, rotation, parent, rootGo);
        }

        public static Node3D CreateVob(string objectName, IModelMesh mdm, IModelHierarchy mdh,
            Vector3 position, Quaternion rotation, Node3D parent = null, Node3D rootGo = null)
        {
            return VobMeshCreator.CreateVob(objectName, mdm, mdh, position, rotation, parent, rootGo);
        }

        public static Node3D CreateVobDecal(IVirtualObject vob, VisualDecal decal, Node3D parent)
        {
            return VobMeshCreator.CreateVobDecal(vob, decal, parent);
        }
        
        // public static Node3D CreateBarrier(string objectName, IMesh mesh)
        // {
        //     return MeshCreator.CreateBarrier(objectName, mesh);
        // }

        // public static void CreatePolyStrip(Node3D go, int numberOfSegments, Vector3 startPoint, Vector3 endPoint)
        // {
        //     PolyStripMeshCreator.CreatePolyStrip(go, numberOfSegments, startPoint, endPoint);
        // }
    }
}