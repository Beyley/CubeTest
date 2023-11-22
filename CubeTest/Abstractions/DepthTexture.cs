using Silk.NET.Maths;
using Silk.NET.WebGPU;

namespace CubeTest.Abstractions;

public unsafe class DepthTexture {
	public const TextureFormat DepthFormat = TextureFormat.Depth16Unorm;

	public Silk.NET.WebGPU.Texture* RawTexture;
	public TextureView*             RawTextureView;

	public Vector2D<int> Size;

	public DepthTexture(uint width, uint height) {
		TextureFormat viewFormat = DepthFormat;

		this.Size.X = (int)width;
		this.Size.Y = (int)height;

		TextureDescriptor descriptor = new TextureDescriptor {
			Size            = new Extent3D(width, height, 1),
			Format          = viewFormat,
			Usage           = TextureUsage.RenderAttachment | TextureUsage.TextureBinding,
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
			Aspect          = TextureAspect.DepthOnly,
			MipLevelCount   = 1,
			ArrayLayerCount = 1,
			BaseArrayLayer  = 0,
			BaseMipLevel    = 0
		};

		this.RawTextureView = Graphics.WebGPU.TextureCreateView(this.RawTexture, viewDescriptor);

		Console.WriteLine($"Created depth texture of size {this.Size}");
	}

	private void ReleaseUnmanagedResources() {
		Console.WriteLine($"Deleting texture of size {this.Size}");
		Graphics.WebGPU.TextureViewRelease(this.RawTextureView);
		Graphics.WebGPU.TextureRelease(this.RawTexture);
	}

	public void Dispose() {
		this.ReleaseUnmanagedResources();
		GC.SuppressFinalize(this);
	}

	~DepthTexture() {
		this.ReleaseUnmanagedResources();
	}
}
