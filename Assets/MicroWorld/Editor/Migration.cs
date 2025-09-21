#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MicroWorldNS
{
    public class Migration : ScriptableObject
    {
        public List<Replace> Shaders = new List<Replace>();
        public List<ReplaceMat> Materials = new List<ReplaceMat>();

        public static void Migrate(Dir direction)
        {
            var instance = Resources.Load<Migration>(nameof(Migration));
            if (instance == null)
                return;

            var myDir = Directory.EnumerateDirectories(Application.dataPath, "MicroWorld", SearchOption.AllDirectories).FirstOrDefault();
            if (myDir == null)
            {
                Debug.LogError("MicroWorld folder is not found!");
                return;
            }

            var replaces = new Dictionary<Shader, Shader>();
            foreach (var r in instance.Shaders)
                switch (direction)
                {
                    case Dir.To_URP: replaces[r.BuiltIn] = r.URP; break;
                    case Dir.To_HDRP: replaces[r.BuiltIn] = r.HDRP; break;
                    case Dir.To_BuiltIn: replaces[r.URP] = r.BuiltIn; break;
                }

            if (direction == Dir.To_URP)
            {
                // replace standard shaders
                ReplaceByName("Standard", "Universal Render Pipeline/Lit");
                ReplaceByName("Standard (Specular setup)", "Universal Render Pipeline/Lit");

                // terrain shader
                ReplaceByName("Nature/Terrain/Diffuse", "Universal Render Pipeline/Terrain/Lit");
            }

            if (direction == Dir.To_HDRP)
            {
                // replace standard shaders
                ReplaceByName("Standard", "HDRP/Lit");
                ReplaceByName("Standard (Specular setup)", "HDRP/Lit");

                // terrain shader
                ReplaceByName("Nature/Terrain/Diffuse", "HDRP/TerrainLit");
            }

            var replacesMat = new Dictionary<Material, Material>();
            foreach (var r in instance.Materials)
                switch (direction)
                {
                    case Dir.To_URP: replacesMat[r.BuiltIn] = r.URP; break;
                    case Dir.To_HDRP: replacesMat[r.BuiltIn] = r.HDRP; break;
                    case Dir.To_BuiltIn: replacesMat[r.URP] = r.BuiltIn; break;
                }

            foreach (var file in Directory.EnumerateFiles(myDir, "*.mat", System.IO.SearchOption.AllDirectories))
            {
                var localFilePath = file.Replace(Application.dataPath, "Assets");
                foreach (var mat in AssetDatabase.LoadAllAssetsAtPath(localFilePath).OfType<Material>())
                {
                    var shader = mat.shader;
                    if (shader == null) continue;
                    if (replaces.TryGetValue(shader, out var newShader))
                    {
                        try
                        {
                            if (shader.name.StartsWith("Standard"))
                            {
                                if (direction == Dir.To_URP)
                                    StMatToUrp(mat, newShader);
                                if (direction == Dir.To_HDRP)
                                    StMatToHdrp(mat, newShader);
                            }
                            else
                            {
                                mat.shader = newShader;
                                if (direction == Dir.To_HDRP)
                                    AdjustHDRP(mat);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
                        }
                    }
                }
                ;
            }

            foreach (var file in System.IO.Directory.EnumerateFiles(myDir, "*.prefab", System.IO.SearchOption.AllDirectories))
            {
                var localFilePath = file.Replace(Application.dataPath, "Assets");
                foreach (var go in AssetDatabase.LoadAllAssetsAtPath(localFilePath).OfType<GameObject>())
                {
                    foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var mats = mr.sharedMaterials;
                        var found = false;
                        for (int i = 0; i < mats.Length; i++)
                        {
                            var mat = mr.sharedMaterials[i];
                            if (mat == null) continue;
                            if (replacesMat.TryGetValue(mat, out var newMat))
                            {
                                mats[i] = newMat;
                                found = true;
                            }
                        }
                        if (found)
                            mr.sharedMaterials = mats;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            void ReplaceByName(string builtIn, string srp)
            {
                var builtInShader = Shader.Find(builtIn);
                var srpShader = Shader.Find(srp);
                if (builtInShader != null && srpShader != null)
                    switch (direction)
                    {
                        case Dir.To_HDRP:
                        case Dir.To_URP:
                            replaces[builtInShader] = srpShader;
                            break;
                        case Dir.To_BuiltIn:
                            replaces[srpShader] = builtInShader;
                            break;
                    }
            }
        }

        private static void StMatToUrp(Material mat, Shader newShader)
        {
            var mainTex = mat.GetTexture("_MainTex");
            var color = mat.GetColor("_Color");
            var gloss = mat.GetFloat("_Glossiness");
            var detTex = mat.GetTexture("_DetailAlbedoMap");
            var alphaClip = mat.GetTag("RenderType", true, "") == "TransparentCutout";

            if (detTex != null && PlayerSettings.colorSpace == ColorSpace.Linear)
                MakeTextureSRGB(detTex, false);
            mat.shader = newShader;
            mat.SetTexture("_BaseMap", mainTex);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", gloss);
            mat.SetFloat("_AlphaClip", alphaClip ? 1 : 0);
        }

        private static void StMatToHdrp(Material mat, Shader newShader)
        {
            var mainTex = mat.GetTexture("_MainTex");
            var color = mat.GetColor("_Color");
            var gloss = mat.GetFloat("_Glossiness");
            var alphaClip = mat.GetTag("RenderType", true, "") == "TransparentCutout";

            mat.shader = newShader;

            mat.SetTexture("_BaseColorMap", mainTex);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", gloss);
            mat.SetFloat("_AlphaCutoffEnable", alphaClip ? 1 : 0);

            if (alphaClip)
                mat.SetFloat("_AlphaCutoff", 0.5f);

            mat.SetFloat("_SurfaceType", 0);
            mat.SetFloat("_BlendMode", 0);
            mat.SetFloat("_ZWrite", 1);
        }

        static Material hdrpSampleMat;

        private static void AdjustHDRP(Material mat)
        {
            if (hdrpSampleMat == null)
                hdrpSampleMat = Resources.Load<Material>("GrassHDRP");

            mat.SetFloat("_DiffusionProfileHash", hdrpSampleMat.GetFloat("_DiffusionProfileHash"));
            mat.SetFloat("_DoubleSidedNormalMode", hdrpSampleMat.GetFloat("_DoubleSidedNormalMode"));
        }

        public static void MakeTextureSRGB(Texture tex, bool sRGB)
        {
            string path = AssetDatabase.GetAssetPath(tex);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.sRGBTexture != sRGB)
            {
                importer.sRGBTexture = sRGB;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        [Serializable]
        public class Replace
        {
            public Shader BuiltIn;
            public Shader URP;
            public Shader HDRP;
        }

        [Serializable]
        public class ReplaceMat
        {
            public Material BuiltIn;
            public Material URP;
            public Material HDRP;
        }

        public enum Dir
        {
            To_URP, To_BuiltIn, To_HDRP
        }
    }
}
#endif