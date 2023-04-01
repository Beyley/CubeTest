using System.Numerics;

namespace CubeTest; 

public struct UiVertex
{
	public UiVertex(Vector2 position, Vector2 texCoord)
	{
		Position = position;
		TexCoord = texCoord;
	}

	public Vector2 Position;
	public Vector2 TexCoord;
}