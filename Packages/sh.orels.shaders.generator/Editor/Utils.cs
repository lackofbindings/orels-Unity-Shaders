﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ORL.ShaderGenerator
{
    public static class Utils
    {
        public static TextAsset Locator = Resources.Load<TextAsset>("ORLLocator");
        
        public static string GetORLSourceFolder()
        {
            if (Locator == null)
            {
                AssetDatabase.Refresh();
                Locator = Resources.Load<TextAsset>("ORLLocator");
                if (Locator == null)
                {
                    AssetDatabase.ImportAsset("Packages/sh.orels.shaders.generator/Runtime/Resources/ORLLocator.txt");
                    Locator = Resources.Load<TextAsset>("ORLLocator");
                }
            }
            var locatorPath = AssetDatabase.GetAssetPath(Locator);
            var sourceFolder = locatorPath.Substring(0, locatorPath.LastIndexOf('/'));
            sourceFolder = sourceFolder.Replace("/Resources", "/Sources");
            return sourceFolder;
        }

        public static string GetFullPath(string assetPath)
        {
            return assetPath;
        }

        public static string ResolveORLAsset(string path, bool bundled, string basePath = null)
        {
            if (bundled)
            {
                return ResolveBundledAsset(path);
            }

            var freeAsset = ResolveFreeAsset(path, basePath);
            if (freeAsset == null)
            {
                Debug.LogWarning($"Unable to find asset {path}. Make sure it exists in {basePath}");
            }

            return freeAsset;
        }

        private static string ResolveBundledAsset(string path)
        {
            var cleaned = path.Replace("@/", "");
            var sourcesFolder = GetORLSourceFolder();
            // this package is split off but we still want to have nice shorthands into it
            var shaderSourcesFolder = "/Packages/sh.orels.shaders/Runtime";

            var builtInAsset = ResolveFreeAsset(cleaned, sourcesFolder);
            if (!string.IsNullOrWhiteSpace(builtInAsset)) return builtInAsset;

            var shaderPackageAsset = ResolveFreeAsset(cleaned, shaderSourcesFolder);
            
            if (builtInAsset == null && shaderPackageAsset == null)
            {
                Debug.LogWarning($"Unable to find bundled asset {path}. Make sure it exists in {sourcesFolder} or {shaderSourcesFolder}");
                return null;
            }

            return shaderPackageAsset;
        }

        private static string ResolveFreeAsset(string path, string basePath)
        {
            var fullPath = basePath + "/" + path;
            // Resolve absolute paths
            var isAbsoluteImport = path.StartsWith("/");
            if (isAbsoluteImport) {
                fullPath = GetFullPath(path.Substring(1));
            }
            // Resolve relative paths
            if (path.StartsWith(".."))
            {
                var parts = path.Split('/').ToList();
                var fileName = parts[parts.Count - 1];
                parts.RemoveAt(parts.Count - 1);
                foreach (var part in parts)
                {
                    if (part == "..")
                    {
                        basePath = basePath.Substring(0, basePath.LastIndexOf('/'));
                    }
                    else
                    {
                        basePath += "/" + part;
                    }
                }
                fullPath = GetFullPath(basePath + "/" + fileName);
            }

            if (fullPath.StartsWith("/"))
            {
                fullPath = fullPath.Substring(1);
            }
            var directExists = File.Exists(fullPath);
            var orlSourceExists = File.Exists($"{fullPath}.orlsource");
            var orlShaderExists = File.Exists($"{fullPath}.orlshader");
            var orlTemplateExists = File.Exists($"{fullPath}.orltemplate");
            if (!directExists && !orlSourceExists && !orlShaderExists && !orlTemplateExists)
            {
                return null;
            }

            if (directExists)
            {
                return isAbsoluteImport ? path.Substring(1) : fullPath;
            }

            if (orlSourceExists)
            {
                return (isAbsoluteImport ? path.Substring(1) : fullPath) + ".orlsource";
            }
            
            if (orlShaderExists)
            {
                return (isAbsoluteImport ? path.Substring(1) : fullPath) + ".orlshader";
            }
            
            if (orlTemplateExists)
            {
                return (isAbsoluteImport ? path.Substring(1) : fullPath) + ".orltemplate";
            }

            return fullPath;
        }

        public static string ResolveORLAsset(string path)
        {
            return ResolveORLAsset(path, true);
        }
        
        public static string[] GetORLTemplate(string path)
        {
            var cleaned = path.Replace("@", "");
            var sourcesFolder = GetORLSourceFolder();
            var fullPath = GetFullPath(sourcesFolder + cleaned + ".orltemplate");
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"Unable to find built-in asset {cleaned}. Make sure it exists in {sourcesFolder}");
                return null;
            }

            return File.ReadAllLines(fullPath);
        }
        
        public static string[] GetORLSource(string path)
        {
            var cleaned = path.Replace("@", "");
            var sourcesFolder = GetORLSourceFolder();
            var fullPath = GetFullPath(sourcesFolder + cleaned + ".orlsource");
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"Unable to find built-in asset {cleaned}. Make sure it exists in {sourcesFolder}");
                return null;
            }

            return File.ReadAllLines(fullPath);
        }

        public static string[] GetAssetSource(string path, string basePath)
        {
            return File.ReadAllLines(GetFullPath(ResolveORLAsset(path, path.StartsWith("@/"), basePath)));
        }

        public static Texture2D GetNonModifiableTexture(Shader shader, string name)
        {
            var so = new SerializedObject(shader);
            var texList = so.FindProperty("m_NonModifiableTextures");
            if (texList.arraySize == 0) return null;
            
            for (var i = 0; i < texList.arraySize; i++)
            {
                var tex = texList.GetArrayElementAtIndex(i);
                var texName = tex.FindPropertyRelative("first").stringValue;
                if (texName != name) continue;
                var texValue = tex.FindPropertyRelative("second");
                return texValue.objectReferenceValue as Texture2D;
            }

            return null;
        }

        public static void RecursivelyCollectDependencies(List<string> sourceList, ref List<string> dependencies, string basePath)
        {
            var parser = new Parser();
            foreach (var source in sourceList)
            {
                var blocks = parser.Parse(GetAssetSource(source, basePath));
                var includesBlockIndex = blocks.FindIndex(b => b.Name == "%Includes");
                if (includesBlockIndex == -1)
                {
                    dependencies.Add(source);
                    continue;
                }
                var cleanDepPaths = blocks[includesBlockIndex].Contents
                    .Select(l => l.Replace("\"", "").Replace(",", "").Trim()).ToList();
                foreach (var depPath in cleanDepPaths)
                {
                    if (depPath == "self")
                    {
                        if (!dependencies.Contains(source))
                        {
                            dependencies.Add(source);
                        }
                        continue;
                    }
                    if (!dependencies.Contains(depPath))
                    {
                        var deepDeps = new List<string>();
                        RecursivelyCollectDependencies(new List<string> {depPath}, ref deepDeps, basePath);
                        dependencies.AddRange(deepDeps);
                    }
                }
            }
        }
    }
}