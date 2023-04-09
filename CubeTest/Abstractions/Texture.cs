using Silk.NET.Maths;
using Silk.NET.WebGPU;

namespace CubeTest.Abstractions;

public unsafe class Texture : IDisposable {
	public Silk.NET.WebGPU.Texture* RawTexture;
	public TextureView*             RawTextureView;

	public Vector2D<int> Size;
	
	public Texture(byte[] data) {
		using Image<Rgba32> image = Image.Load<Rgba32>(data);

		this.Load(image);
	}

	private void Load(Image<Rgba32> image) {
		this.Size = new Vector2D<int>(image.Width, image.Height);
		
		TextureFormat viewFormat = TextureFormat.Rgba8Unorm;

		TextureDescriptor descriptor = new TextureDescriptor {
			Size            = new Extent3D((uint)image.Width, (uint)image.Height, 1),
			Format          = TextureFormat.Rgba8Unorm,
			Usage           = TextureUsage.CopyDst | TextureUsage.TextureBinding,
			MipLevelCount   = 1,
			SampleCount     = 1,
			Dimension       = TextureDimension.TextureDimension2D,
			ViewFormats     = &viewFormat,
			ViewFormatCount = 1
		};

		this.RawTexture = Graphics.WebGPU.DeviceCreateTexture(Graphics.Device, descriptor);

		TextureViewDescriptor viewDescriptor = new TextureViewDescriptor {
			Format          = viewFormat,
			Dimension       = TextureViewDimension.TextureViewDimension2D,
			Aspect          = TextureAspect.All,
			MipLevelCount   = 1,
			ArrayLayerCount = 1,
			BaseArrayLayer  = 0,
			BaseMipLevel    = 0
		};

		this.RawTextureView = Graphics.WebGPU.TextureCreateView(this.RawTexture, viewDescriptor);

		image.ProcessPixelRows(pixels => {
			for (int y = 0; y < pixels.Height; y++) {
				Span<Rgba32> row = pixels.GetRowSpan(y);

				ImageCopyTexture imageCopyTexture = new ImageCopyTexture {
					Texture  = this.RawTexture,
					Aspect   = TextureAspect.All,
					MipLevel = 0,
					Origin   = new Origin3D(0, (uint)y, 0)
				};

				TextureDataLayout layout = new TextureDataLayout {
					BytesPerRow  = (uint)(pixels.Width * sizeof(Rgba32)),
					RowsPerImage = (uint)pixels.Height
				};

				Extent3D extent = new Extent3D {
					Width              = (uint)pixels.Width,
					Height             = 1,
					DepthOrArrayLayers = 1
				};

				fixed (void* dataPtr = row)
					Graphics.WebGPU.QueueWriteTexture(Graphics.Queue, imageCopyTexture, dataPtr, (nuint)(sizeof(Rgba32) * row.Length), layout, extent);
			}
		});
		Console.WriteLine($"Created texture of size {this.Size}");
	}

	private void ReleaseUnmanagedResources() {
		Console.WriteLine($"Deleting texture of size {this.Size}");
		Graphics.Disposal.Dispose(this.RawTextureView);
		Graphics.Disposal.Dispose(this.RawTexture);
	}

	public void Dispose() {
		this.ReleaseUnmanagedResources();
		GC.SuppressFinalize(this);
	}

	~Texture() {
		this.ReleaseUnmanagedResources();
	}
}
