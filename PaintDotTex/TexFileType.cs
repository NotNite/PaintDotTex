using System;
using System.IO;
using System.Runtime.InteropServices;
using Lumina.Data;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Tex.Buffers;
using PaintDotNet;
using PaintDotNet.Rendering;
using SurfaceExtensions = PaintDotNet.Rendering.SurfaceExtensions;

namespace PaintDotTex;

public sealed class TexFileType : FileType, IFileTypeFactory {
    public TexFileType()
        : base("FFXIV Texture",
            new FileTypeOptions {
                LoadExtensions = new[] { ".tex" },
                SaveExtensions = new [] { ".tex" }
            }) { }

    public static T ByteToType<T>(BinaryReader reader) {
        byte[] bytes = reader.ReadBytes(Marshal.SizeOf(typeof(T)));

        GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        T theStructure = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
        handle.Free();

        return theStructure;
    }

    public static byte[] TypeToByte<T>(T structure) {
        int size = Marshal.SizeOf(structure);
        byte[] arr = new byte[size];

        IntPtr ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(structure, ptr, true);
        Marshal.Copy(ptr, arr, 0, size);
        Marshal.FreeHGlobal(ptr);

        return arr;
    }

    protected override Document OnLoad(Stream input) {
        var br = new BinaryReader(input);
        var header = ByteToType<TexFile.TexHeader>(br);

        var lbr = new LuminaBinaryReader(input);
        var tex = TextureBuffer.FromStream(header, lbr);
        var texData = tex.Filter(mip: 0, z: 0, format: TexFile.TextureFormat.B8G8R8A8).RawData;

        var doc = new Document(header.Width, header.Height);
        var surface = new Surface(header.Width, header.Height);

        for (var y = 0; y < header.Height; y++) {
            for (var x = 0; x < header.Width; x++) {
                var color = ColorBgra.FromBgra(texData[(y * header.Width + x) * 4 + 0],
                    texData[(y * header.Width + x) * 4 + 1], texData[(y * header.Width + x) * 4 + 2],
                    texData[(y * header.Width + x) * 4 + 3]);
                surface[x, y] = color;
            }
        }

        doc.Layers.Add(Layer.CreateBackgroundLayer(surface, takeOwnership: true));
        return doc;
    }

    protected override void OnSave(Document input, Stream output, SaveConfigToken token, Surface scratchSurface,
        ProgressEventHandler callback) {
        scratchSurface.Clear();
        input.CreateRenderer().Render(SurfaceExtensions.AsRegionPtr(scratchSurface), Point2Int32.Zero);

        unsafe {
            var texHeader = new TexFile.TexHeader();
            texHeader.Width = (ushort)scratchSurface.Width;
            texHeader.Height = (ushort)scratchSurface.Height;
            texHeader.Format = TexFile.TextureFormat.B8G8R8A8;
            texHeader.Depth = 1;
            texHeader.MipLevels = 1;

            *(int*)texHeader.LodOffset = 0;
            *(int*)(texHeader.LodOffset + 1) = 1;
            *(int*)(texHeader.LodOffset + 2) = 1;

            *(int*)texHeader.OffsetToSurface = 80;

            var texData = new byte[scratchSurface.Width * scratchSurface.Height * 4 + sizeof(TexFile.TexHeader)];
            var bw = new BinaryWriter(new MemoryStream(texData));

            var texHeaderBytes = TypeToByte(texHeader);
            bw.Write(texHeaderBytes);


            for (var y = 0; y < scratchSurface.Height; y++) {
                for (var x = 0; x < scratchSurface.Width; x++) {
                    var pixel = scratchSurface[x, y];
                    bw.Write(pixel.B);
                    bw.Write(pixel.G);
                    bw.Write(pixel.R);
                    bw.Write(pixel.A);
                }
            }

            output.Write(texData);
        }
    }

    public FileType[] GetFileTypeInstances() {
        return new FileType[] { new TexFileType() };
    }
}
