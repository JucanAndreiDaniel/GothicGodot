using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// using GVR.Creator.Sounds;
// using GVR.Data;
// using GVR.Extensions;
// using GVR.Globals;
// using JetBrains.Annotations;
// using UnityEngine;
// using UnityEngine.Rendering;
// using UnityEngine.Tilemaps;
using ZenKit;
using ZenKit.Daedalus;
using Font = ZenKit.Font;
using Mesh = ZenKit.Mesh;

// using Object = UnityEngine.Object;

namespace GothicGodot
{
    public static class AssetCache
    {
        // public static Dictionary<TextureArrayTypes, UnityEngine.Texture> TextureArrays { get; private set; } = new();
        public static int ReferenceTextureSize = 256;

        private static readonly Dictionary<string, ImageTexture> TextureCache = new();
        private static readonly Dictionary<string, ZenKit.Texture> TextureDataCache = new();
        private static readonly Dictionary<string, IMesh> MshCache = new();
        private static readonly Dictionary<string, IModelScript> MdsCache = new();
        private static readonly Dictionary<string, IModelAnimation> AnimCache = new();
        private static readonly Dictionary<string, IModelHierarchy> MdhCache = new();
        private static readonly Dictionary<string, IModel> MdlCache = new();
        private static readonly Dictionary<string, IModelMesh> MdmCache = new();
        private static readonly Dictionary<string, IMultiResolutionMesh> MrmCache = new();
        private static readonly Dictionary<string, IMorphMesh> MmbCache = new();
        private static readonly Dictionary<string, ItemInstance> ItemDataCache = new();
        private static readonly Dictionary<string, MusicThemeInstance> MusiThemeCache = new();
        private static readonly Dictionary<string, SoundEffectInstance> SfxDataCache = new();

        private static readonly Dictionary<string, ParticleEffectInstance> PfxDataCache = new();

        // private static readonly Dictionary<string, SoundData> SoundCache = new();
        private static readonly Dictionary<string, IFont> FontCache = new();

        private static Dictionary<TextureArrayTypes, List<(string PreparedKey, ZenKit.Texture Texture)>>
            _arrayTexturesList = new();

        public enum TextureArrayTypes
        {
            Opaque,
            Transparent,
            Water
        }

        private static readonly string[] MisplacedMdmArmors =
        {
            "Hum_GrdS_Armor",
            "Hum_GrdM_Armor",
            "Hum_GrdL_Armor",
            "Hum_NovM_Armor",
            "Hum_TplL_Armor",
            "Hum_Body_Cooksmith",
            "Hum_VlkL_Armor",
            "Hum_VlkM_Armor",
            "Hum_KdfS_Armor"
        };


        public static ImageTexture TryGetTexture(string key)
        {
            string preparedKey = GetPreparedKey(key);
            if (TextureCache.ContainsKey(preparedKey) && TextureCache[preparedKey] != null)
            {
                return TextureCache[preparedKey];
            }
            ImageTexture imageTexture = null;
            try
            {
                imageTexture = ImportZenTexture(preparedKey);
                TextureCache.Add(preparedKey, imageTexture);
            }
            catch (Exception e)
            {
                GD.PrintErr(e);
                return null;
            }

            return imageTexture;
        }

        public static ZenKit.Texture GetZenTextureData(string key)
        {
            if (TextureDataCache.ContainsKey(key))
            {
                return TextureDataCache[key];
            }

            try
            {
                string preparedKey = GetPreparedKey(key);
                TextureDataCache.Add(key, new ZenKit.Texture(GameData.Vfs, $"{preparedKey}-C.TEX"));
                return TextureDataCache[key];
            }
            catch (Exception)
            {
                GD.PrintErr($"Texture {key} couldn't be found.");
                return null;
            }
        }

        private static ImageTexture ImportZenTexture(string path)
        {
            var texture = GetZenTextureData(path);
            if (texture == null)
            {
                return null;
            }
            var image = new Image();

            image.SetData(texture.Width, texture.Height, false, Image.Format.Rgba8,
                texture.GetMipmapRgba(0));
            image.GenerateMipmaps();


            var imageTexture = new ImageTexture();
            imageTexture = ImageTexture.CreateFromImage(image);
            return imageTexture;
        }

        public static IModelScript TryGetMds(string key)
        {
            var preparedKey = GetPreparedKey(key);
            if (MdsCache.TryGetValue(preparedKey, out var data))
                return data;

            var newData = new ModelScript(GameData.Vfs, $"{preparedKey}.mds").Cache();
            MdsCache[preparedKey] = newData;

            return newData;
        }

        public static IModelAnimation TryGetAnimation(string mdsKey, string animKey)
        {
            var preparedMdsKey = GetPreparedKey(mdsKey);
            var preparedAnimKey = GetPreparedKey(animKey);
            var preparedKey = $"{preparedMdsKey}-{preparedAnimKey}";
            if (AnimCache.TryGetValue(preparedKey, out var data))
                return data;

            IModelAnimation newData = null;
            try
            {
                newData = new ModelAnimation(GameData.Vfs, $"{preparedKey}.man").Cache();
            }
            catch (Exception)
            {
                // ignored
            }

            AnimCache[preparedKey] = newData;

            return newData;
        }

        public static IMesh TryGetMsh(string key)
        {
            var preparedKey = GetPreparedKey(key);
            if (MshCache.TryGetValue(preparedKey, out var data))
                return data;

            IMesh newData = null;
            try
            {
                newData = new Mesh(GameData.Vfs, $"{preparedKey}.msh").Cache();
            }
            catch (Exception)
            {
                // ignored
            }

            MshCache[preparedKey] = newData;

            return newData;
        }


        public static IModelHierarchy TryGetMdh(string key)
        {
            var preparedKey = GetPreparedKey(key);
            if (MdhCache.TryGetValue(preparedKey, out var data))
                return data;

            IModelHierarchy newData = null;
            try
            {
                newData = new ModelHierarchy(GameData.Vfs, $"{preparedKey}.mdh").Cache();
            }
            catch (Exception)
            {
                // ignored
            }

            MdhCache[preparedKey] = newData;

            return newData;
        }


        public static IModel TryGetMdl(string key)
        {
            var preparedKey = GetPreparedKey(key);
            if (MdlCache.TryGetValue(preparedKey, out var data))
                return data;

            IModel newData = null;
            try
            {
                newData = new Model(GameData.Vfs, $"{preparedKey}.mdl").Cache();
            }
            catch (Exception)
            {
                // ignored
            }

            MdlCache[preparedKey] = newData;

            return newData;
        }


        public static IModelMesh TryGetMdm(string key)
        {
            var preparedKey = GetPreparedKey(key);
            if (MdmCache.TryGetValue(preparedKey, out var data))
                return data;

            IModelMesh newData = null;
            try
            {
                newData = new ModelMesh(GameData.Vfs, $"{preparedKey}.mdm").Cache();
            }
            catch (Exception)
            {
                // ignored
            }

            MdmCache[preparedKey] = newData;

            // FixArmorTriangles(preparedKey, newData);

            return newData;
        }

        /// <summary>
        /// Some armor mdm's have wrong triangles. This function corrects them hard coded until we find a proper solution.
        /// </summary>
        private static void FixArmorTriangles(string key, IModelMesh mdm)
        {
            if (!MisplacedMdmArmors.Contains(key, StringComparer.OrdinalIgnoreCase))
                return;

            foreach (var mesh in mdm.Meshes)
            {
                for (var i = 0; i < mesh.Mesh.Positions.Count; i++)
                {
                    var curPos = mesh.Mesh.Positions[i];
                    mesh.Mesh.Positions[i] = new(curPos.X + 0.5f, curPos.Y - 0.5f, curPos.Z + 13f);
                }
            }
        }

        public static IMultiResolutionMesh TryGetMrm(string key)
        {
            var preparedKey = GetPreparedKey(key);
            if (MrmCache.TryGetValue(preparedKey, out var data))
                return data;

            IMultiResolutionMesh newData = null;
            try
            {
                newData = new MultiResolutionMesh(GameData.Vfs, $"{preparedKey}.mrm").Cache();
            }
            catch (Exception e)
            {
                // ignored
            }

            MrmCache[preparedKey] = newData;


            return newData;
        }

        /// <summary>
        /// MMS == MorphMesh
        /// e.g. face animations during dialogs.
        /// </summary>
        public static IMorphMesh TryGetMmb(string key)
        {
            var preparedKey = GetPreparedKey(key);
            if (MmbCache.TryGetValue(preparedKey, out var data))
                return data;

            var newData = new MorphMesh(GameData.Vfs, $"{preparedKey}.mmb").Cache();
            MmbCache[preparedKey] = newData;

            return newData;
        }

        public static MusicThemeInstance TryGetMusic(string key)
        {
            var preparedKey = GetPreparedKey(key);
            if (MusiThemeCache.TryGetValue(preparedKey, out var data))
                return data;

            MusicThemeInstance newData = null;
            try
            {
                newData = GameData.MusicVm.InitInstance<MusicThemeInstance>(preparedKey);
            }
            catch (Exception)
            {
                // ignored
            }

            MusiThemeCache[preparedKey] = newData;

            return newData;
        }

        /// <summary>
        /// Hint: Instances only need to be initialized once in ZenKit.
        /// There are two ways of getting Item data. Via INSTANCE name or symbolIndex inside VM.
        /// </summary>
        public static ItemInstance TryGetItemData(int instanceId)
        {
            var symbol = GameData.GothicVm.GetSymbolByIndex(instanceId);

            if (symbol == null)
                return null;

            return TryGetItemData(symbol.Name);
        }

        /// <summary>
        /// Hint: Instances only need to be initialized once in ZenKit.
        /// There are two ways of getting Item data. Via INSTANCE name or symbolIndex inside VM.
        /// </summary>
        public static ItemInstance TryGetItemData(string key)
        {
            var preparedKey = GetPreparedKey(key);
            if (ItemDataCache.TryGetValue(preparedKey, out var data))
                return data;

            ItemInstance newData = null;
            try
            {
                newData = GameData.GothicVm.InitInstance<ItemInstance>(preparedKey);
            }
            catch (Exception)
            {
                // ignored
            }

            ItemDataCache[preparedKey] = newData;

            return newData;
        }

        /// <summary>
        /// Hint: Instances only need to be initialized once in ZenKit and don't need to be deleted during runtime.
        /// </summary>
        public static SoundEffectInstance TryGetSfxData(string key)
        {
            var preparedKey = GetPreparedKey(key);
            if (SfxDataCache.TryGetValue(preparedKey, out var data))
                return data;

            SoundEffectInstance newData = null;
            try
            {
                newData = GameData.SfxVm.InitInstance<SoundEffectInstance>(preparedKey);
            }
            catch (Exception)
            {
                // ignored
            }

            SfxDataCache[preparedKey] = newData;

            return newData;
        }

        /// <summary>
        /// Hint: Instances only need to be initialized once in ZenKit and don't need to be deleted during runtime.
        /// </summary>
        public static ParticleEffectInstance TryGetPfxData(string key)
        {
            var preparedKey = GetPreparedKey(key);
            if (PfxDataCache.TryGetValue(preparedKey, out var data))
                return data;

            ParticleEffectInstance newData = null;
            try
            {
                newData = GameData.PfxVm.InitInstance<ParticleEffectInstance>(preparedKey);
            }
            catch (Exception)
            {
                // ignored
            }

            PfxDataCache[preparedKey] = newData;

            return newData;
        }

        // public static SoundData TryGetSound(string key)
        // {
        //     var preparedKey = GetPreparedKey(key);
        //     if (SoundCache.TryGetValue(preparedKey, out var data))
        //         return data;
        //
        //     var newData = SoundCreator.GetSoundArrayFromVfs($"{preparedKey}.wav");
        //     SoundCache[preparedKey] = newData;
        //
        //     return newData;
        // }

        public static IFont TryGetFont(string key)
        {
            var preparedKey = GetPreparedKey(key);
            if (FontCache.TryGetValue(preparedKey, out var data))
                return data;

            var fontData = new Font(GameData.Vfs, $"{preparedKey}.fnt").Cache();
            FontCache[preparedKey] = fontData;

            return fontData;
        }

        private static string GetPreparedKey(string key)
        {
            var lowerKey = key.ToLower();
            var extension = Path.GetExtension(lowerKey);

            if (extension == string.Empty)
                return lowerKey;
            else
                return lowerKey.Replace(extension, "");
        }

        public static void Dispose()
        {
            TextureCache.Clear();
            TextureDataCache.Clear();
            MdsCache.Clear();
            AnimCache.Clear();
            MdhCache.Clear();
            MdlCache.Clear();
            MdmCache.Clear();
            MrmCache.Clear();
            MmbCache.Clear();
            ItemDataCache.Clear();
            MusiThemeCache.Clear();
            SfxDataCache.Clear();
            PfxDataCache.Clear();
            // SoundCache.Clear();
            FontCache.Clear();
        }
    }
}