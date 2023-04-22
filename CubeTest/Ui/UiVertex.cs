using System.Numerics;

namespace CubeTest.Ui;

public struct UiVertex {
	public UiVertex(Vector2 position, Vector2 texCoord) {
		this.Position = position;
		this.TexCoord = texCoord;
	}

	public Vector2 Position;
	public Vector2 TexCoord;
}
