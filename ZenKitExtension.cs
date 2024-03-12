using System;
using System.Collections.Generic;
using System.Numerics;
using Godot;
using ZenKit;
using ZenKit.Util;
using Quaternion = Godot.Quaternion;
using Vector2 = Godot.Vector2;
using Vector3 = Godot.Vector3;
using Vector4 = Godot.Vector4;

namespace GothicGodot
{
    public static class ZenKitExtension
    {
        // public static TextureFormat AsUnityTextureFormat(this ZenKit.TextureFormat format)
        // {
        //     return format switch
        //     {
        //         ZenKit.TextureFormat.Dxt1 => TextureFormat.DXT1,
        //         ZenKit.TextureFormat.Dxt5 => TextureFormat.DXT5,
        //         _ => TextureFormat.RGBA32 // Everything else we need to use uncompressed for Unity (e.g. DXT3).
        //     };
        // }

        /// <summary>
        /// According to this blog post, we can transform 3x3 into 4x4 matrix:
        /// @see https://forum.unity.com/threads/convert-3x3-rotation-matrix-to-euler-angles.1086392/#post-7002275
        /// Hint: m33 needs to be 1 to work properly
        /// </summary>
        public static Quaternion ToGodotQuaternion(this Matrix3x3 matrix)
        {
            var basis = new Basis()
            {
                X = new Vector3(matrix.M11, matrix.M12, matrix.M13),
                Y = new Vector3(matrix.M21, matrix.M22, matrix.M23),
                Z = new Vector3(matrix.M31, matrix.M32, matrix.M33),
            };
            
            var quat =new Quaternion(basis);

            return basis.Inverse().GetRotationQuaternion();
        }

        public static Vector3 ToGodotVector(this System.Numerics.Vector3 vector)
        {
            return new Vector3(vector.X, vector.Y, vector.Z) / 100;
        }

        public static Vector2 ToGodotVector(this System.Numerics.Vector2 vector)
        {
            return new Vector2(vector.X, vector.Y);
        }

        public static Projection ToGodotProjection(this System.Numerics.Matrix4x4 matrix)
        {
            return new Projection
            {
                X = new Vector4(matrix.M11, matrix.M12, matrix.M13, matrix.M14),
                Y = new Vector4(matrix.M21, matrix.M22, matrix.M23, matrix.M24),
                Z = new Vector4(matrix.M31, matrix.M32, matrix.M33, matrix.M34),
                W = new Vector4(matrix.M41, matrix.M42, matrix.M43, matrix.M44)
            };
        }

        // public static BoneWeight ToBoneWeight(this List<SoftSkinWeightEntry> weights, List<int> nodeMapping)
        // {
        //     if (weights == null)
        //         throw new ArgumentNullException("Weights are null.");
        //     if (weights.Count == 0 || weights.Count > 4)
        //         throw new ArgumentOutOfRangeException($"Only 1...4 weights are currently supported but >{weights.Count}< provided.");
        //
        //     var data = new BoneWeight();
        //
        //     for (var i = 0; i < weights.Count; i++)
        //     {
        //         var index = Array.IndexOf(nodeMapping.ToArray(), weights[i].NodeIndex);
        //         if (index == -1)
        //             throw new ArgumentException($"No matching node index found in nodeMapping for weights[{i}].nodeIndex.");
        //
        //         switch (i)
        //         {
        //             case 0:
        //                 data.boneIndex0 = index;
        //                 data.weight0 = weights[i].Weight;
        //                 break;
        //             case 1:
        //                 data.boneIndex1 = index;
        //                 data.weight1 = weights[i].Weight;
        //                 break;
        //             case 2:
        //                 data.boneIndex2 = index;
        //                 data.weight2 = weights[i].Weight;
        //                 break;
        //             case 3:
        //                 data.boneIndex3 = index;
        //                 data.weight3 = weights[i].Weight;
        //                 break;
        //         }
        //     }
        //
        //     return data;
        // }
    }
}