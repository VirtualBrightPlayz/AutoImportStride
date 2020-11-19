using System;
using System.IO;
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
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using Stride.TextureConverter;

namespace AutoImporter
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine($"{typeof(Program).Assembly.Location} <file/or/folder/to/import> <folder/to/export/to>");
                return;
            }
            if (!Directory.Exists(args[1]))
            {
                Console.WriteLine($"{args[1]} is an invalid folder.");
                return;
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
                    if ((name.ToLower().EndsWith("bump") && !name.ToLower().StartsWith("bump")) || (name.ToLower().EndsWith("normal") && !name.ToLower().StartsWith("normal")))
                    {
                        TextureAsset tex = NormalMapTextureFactory.Create();
                        tex.Source = new UFile(relativePath);
                        AssetFileSerializer.Save($"{output}/{name}{TextureAsset.FileExtension}", tex, null);
                    }
                    else
                    {
                        TextureAsset tex = ColorTextureFactory.Create();
                        tex.Source = new UFile(relativePath);
                        AssetFileSerializer.Save($"{output}/{name}{TextureAsset.FileExtension}", tex, null);
                        MaterialAsset mat = DiffuseMaterialFactory.Create();
                        Texture tex2 = AttachedReferenceManager.CreateProxyObject<Texture>(tex.Id, tex.Source.FullPath);
                        ((ComputeTextureColor)((MaterialDiffuseMapFeature)mat.Attributes.Diffuse).DiffuseMap).Texture = (Texture)tex2;
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
                    ModelAsset model = DefaultAssetFactory<ModelAsset>.Create();
                    model.Source = new UFile(relativePath);
                    //model.Materials.Add();
                    AssetFileSerializer.Save($"{output}/{name}{ModelAsset.FileExtension.Split(';')[0]}", model, null);
                    break;
                default:
                    Console.WriteLine($"The file extension \".{arr[arr.Length - 1]}\" is not supported. ({path})");
                    break;
            }

        }
    }
}
