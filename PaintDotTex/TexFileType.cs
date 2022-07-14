using System.IO;
using System.Runtime.InteropServices;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Tex.Buffers;
using PaintDotNet;
using PaintDotNet.Rendering;

namespace PaintDotTex;

public sealed class TexFileType : FileType, IFileTypeFactory {
    public TexFileType()
        : base("FFXIV Texture",
            new FileTypeOptions {
                LoadExtensions = new[] { ".tex" },
                SaveExtensions = new string[] { }
            }) { }

    public static T ByteToType<T>(BinaryReader reader) {
        byte[] bytes = reader.ReadBytes(Marshal.SizeOf(typeof(T)));

        GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        T theStructure = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
        handle.Free();

        return theStructure;
    }

    protected override Document OnLoad(Stream input) {
        var br = new BinaryReader(input);
        var header = ByteToType<TexFile.TexHeader>(br);

        var tex = TextureBuffer.FromStream(header, br);
        var texData = tex.Filter(mip: 0, z: 0, format: TexFile.TextureFormat.B8G8R8A8).RawData;

        var doc = new Document(header.Width, header.Height);
        var surface = new Surface(header.Width, header.Height);

        for (var y = 0; y < header.Height; y++) {
            for (var x = 0; x < header.Width; x++) {
                var color = ColorBgra.FromBgra(texData[(y * header.Width + x) * 4 + 0],
                    texData[(y * header.Width + x) * 4 + 1], texData[(y * header.Width + x) * 4 + 2],
                    texData[(y * header.Width + x) * 4 + 3]);
                surface.SetPoint(x, y, color);
            }
        }

        doc.Layers.Add(Layer.CreateBackgroundLayer(surface, takeOwnership: true));

        return doc;
    }

    public FileType[] GetFileTypeInstances() {
        return new FileType[] { new TexFileType() };
    }
}