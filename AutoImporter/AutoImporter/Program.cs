using System;
using System.Collections.Generic;
using System.IO;
using Assimp;
using Stride.Assets.Materials;
using Stride.Assets.Models;
using Stride.Assets.Textures;
using Stride.Core.Assets;
using Stride.Core.Assets.Analysis;
using Stride.Core.Assets.Serializers;
using Stride.Core.IO;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;
using Stride.Graphics;
using Stride.Graphics.Data;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using Stride.TextureConverter;

namespace AutoImporter
{
    class Program
    {
        static bool overwrite = false;
        static AssimpContext ctx = new AssimpContext();

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine($"{typeof(Program).Assembly.Location} <file/or/folder/to/import> <folder/to/export/to> [overwrite=false]");
                return;
            }
            if (!Directory.Exists(args[1]))
            {
                Console.WriteLine($"{args[1]} is an invalid folder.");
                return;
            }
            if (args.Length >= 3 && bool.TrueString.ToLower().Equals(args[2].ToLower()))
            {
                Console.WriteLine("Overwrite flag set to true.");
                if (!bool.TrueString.ToUpper().Equals(args[2]))
                {
                    Console.WriteLine("Overwrite existing [true/false]? Set overwrite flag to \"TRUE\" (case sensitive) to skip this message.");
                    string input = Console.ReadLine();
                    if (bool.TrueString.ToLower().Equals(input.ToLower()))
                    {
                        overwrite = true;
                    }
                }
                else
                    overwrite = true;
            }
            Console.WriteLine($"Importing assets... ({args[0]})");
            if (Directory.Exists(args[0]))
            {
                foreach (string file in Directory.GetFiles(args[0], "*.*", SearchOption.AllDirectories))
                {
                    ImportFile(file, Path.GetRelativePath(args[1], file), args[1]);
                }
            }
            if (File.Exists(args[0]))
            {
                ImportFile(args[0], Path.GetRelativePath(args[1], args[0]), args[1]);
            }
            Console.WriteLine("Done!");
        }

        static TextureAsset ImportTexture(string relativePath, TextureType type)
        {
            switch (type)
            {
                case TextureType.Diffuse:
                    TextureAsset diffuse = ColorTextureFactory.Create();
                    diffuse.Source = new UFile(relativePath);
                    return diffuse;
                case TextureType.Normals:
                    TextureAsset normals = ColorTextureFactory.Create();
                    normals.Source = new UFile(relativePath);
                    return normals;
                default:
                    return null;
            }
        }

        static Texture GetTexture(TextureAsset asset)
        {
            return AttachedReferenceManager.CreateProxyObject<Texture>(asset.Id, asset.Source.FullPath);
        }

        static void SetTexture(MaterialAsset mat, Texture diffuse, TextureType type)
        {
            //MaterialAsset mat = DiffuseMaterialFactory.Create();
            switch (type)
            {
                case TextureType.Diffuse:
                    ((ComputeTextureColor)((MaterialDiffuseMapFeature)mat.Attributes.Diffuse).DiffuseMap).Texture = diffuse;
                    break;
            }
            //((ComputeTextureColor)((MaterialDiffuseMapFeature)mat.Attributes.Diffuse).DiffuseMap).Texture = diffuse;
            //return mat;
        }


        static Stride.Rendering.Material GetMaterial(MaterialAsset materialAsset, string name)
        {
            return AttachedReferenceManager.CreateProxyObject<Stride.Rendering.Material>(materialAsset.Id, name);
        }

        static void ImportFile(string path, string relativePath, string output)
        {
            string[] arr = relativePath.Split('.');
            string name = Path.GetFileNameWithoutExtension(relativePath);
            switch (arr[arr.Length - 1])
            {
                case "jpg":
                case "jpeg":
                case "dds":
                case "tiff":
                case "tif":
                case "psd":
                case "tga":
                case "bmp":
                case "gif":
                case "png":
                    break;
                    if ((name.ToLower().EndsWith("bump") && !name.ToLower().StartsWith("bump")) || (name.ToLower().EndsWith("normal") && !name.ToLower().StartsWith("normal")))
                    {
                        TextureAsset tex = NormalMapTextureFactory.Create();
                        tex.Source = new UFile(relativePath);
                        if (!overwrite && File.Exists($"{output}/{name}{TextureAsset.FileExtension}"))
                        {
                            Console.WriteLine($"{output}/{name}{TextureAsset.FileExtension} exists - not writing.");
                            break;
                        }
                        else
                            AssetFileSerializer.Save($"{output}/{name}{TextureAsset.FileExtension}", tex, null);
                    }
                    else
                    {
                        TextureAsset tex = ColorTextureFactory.Create();
                        tex.Source = new UFile(relativePath);
                        if (!overwrite && File.Exists($"{output}/{name}{TextureAsset.FileExtension}"))
                        {
                            Console.WriteLine($"{output}/{name}{TextureAsset.FileExtension} exists - not writing.");
                            break;
                        }
                        AssetFileSerializer.Save($"{output}/{name}{TextureAsset.FileExtension}", tex, null);
                        MaterialAsset mat = DiffuseMaterialFactory.Create();
                        ((ComputeTextureColor)((MaterialDiffuseMapFeature)mat.Attributes.Diffuse).DiffuseMap).Texture = GetTexture(tex);
                        AssetFileSerializer.Save($"{output}/{name}{MaterialAsset.FileExtension}", mat, null);
                    }
                    break;
                case "dae":
                case "3ds":
                case "obj":
                case "blend":
                case "x":
                case "md2":
                case "md3":
                case "dxf":
                case "fbx":
                    if (!overwrite && File.Exists($"{output}/{name}{ModelAsset.FileExtension.Split(';')[0]}"))
                    {
                        Console.WriteLine($"{output}/{name}{ModelAsset.FileExtension.Split(';')[0]} exists - not writing.");
                        break;
                    }
                    ModelAsset model = DefaultAssetFactory<ModelAsset>.Create();
                    model.Source = new UFile(relativePath);
                    Scene scene = ctx.ImportFile(path, PostProcessSteps.None);
                    Dictionary<string, Stride.Rendering.Material> materials = new Dictionary<string, Stride.Rendering.Material>();
                    for (int i = 0; i < scene.MaterialCount; i++)
                    {
                        if (materials.ContainsKey(scene.Materials[i].Name))
                        {
                            model.Materials.Add(new ModelMaterial()
                            {
                                Name = scene.Materials[i].Name,
                                MaterialInstance = new MaterialInstance(materials[scene.Materials[i].Name])
                            });
                            continue;
                        }

                        if (!overwrite && File.Exists($"{output}/{scene.Materials[i].Name}{MaterialAsset.FileExtension}"))
                        {
                            Console.WriteLine($"{output}/{scene.Materials[i].Name}{MaterialAsset.FileExtension} exists - not writing parent mesh.");
                            break;
                        }
                        MaterialAsset materialAsset = DiffuseMaterialFactory.Create();

                        // set diffuse (if possible)
                        if (scene.Materials[i].HasTextureDiffuse)
                        {
                            string diffPath = Path.GetRelativePath(output, Path.Combine(Path.GetDirectoryName(path), scene.Materials[i].TextureDiffuse.FilePath));
                            TextureAsset asset = ImportTexture(diffPath, TextureType.Diffuse);
                            if (!overwrite && File.Exists($"{output}/{asset.Source.GetFileNameWithoutExtension()}{TextureAsset.FileExtension}"))
                            {
                                Console.WriteLine($"{output}/{asset.Source.GetFileNameWithoutExtension()}{TextureAsset.FileExtension} exists - not writing parent mesh.");
                                break;
                            }
                            AssetFileSerializer.Save($"{output}/{asset.Source.GetFileNameWithoutExtension()}{TextureAsset.FileExtension}", asset, null);
                            Texture tex = GetTexture(asset);
                            SetTexture(materialAsset, tex, TextureType.Diffuse);
                        }

                        // normals
                        if (scene.Materials[i].HasTextureNormal)
                        {
                            string normPath = Path.GetRelativePath(output, Path.Combine(Path.GetDirectoryName(path), scene.Materials[i].TextureNormal.FilePath));
                            TextureAsset asset = ImportTexture(normPath, TextureType.Normals);
                            if (!overwrite && File.Exists($"{output}/{asset.Source.GetFileNameWithoutExtension()}{TextureAsset.FileExtension}"))
                            {
                                Console.WriteLine($"{output}/{asset.Source.GetFileNameWithoutExtension()}{TextureAsset.FileExtension} exists - not writing parent mesh.");
                                break;
                            }
                            AssetFileSerializer.Save($"{output}/{asset.Source.GetFileNameWithoutExtension()}{TextureAsset.FileExtension}", asset, null);
                            Texture tex = GetTexture(asset);
                            SetTexture(materialAsset, tex, TextureType.Normals);
                        }


                        AssetFileSerializer.Save($"{output}/{scene.Materials[i].Name}{MaterialAsset.FileExtension}", materialAsset, null);
                        Stride.Rendering.Material material = GetMaterial(materialAsset, scene.Materials[i].Name);
                        model.Materials.Add(new ModelMaterial()
                        {
                            Name = scene.Materials[i].Name,
                            MaterialInstance = new MaterialInstance(material)
                        });
                        materials.Add(scene.Materials[i].Name, material);
                    }
                    AssetFileSerializer.Save($"{output}/{name}{ModelAsset.FileExtension.Split(';')[0]}", model, null);
                    break;
                default:
                    Console.WriteLine($"The file extension \".{arr[arr.Length - 1]}\" is not supported. ({path})");
                    break;
            }
        }
    }
}
