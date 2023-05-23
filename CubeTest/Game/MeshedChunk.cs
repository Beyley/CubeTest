using Buffer = Silk.NET.WebGPU.Buffer;

namespace CubeTest.Game;

public unsafe struct MeshedChunk
{
    public Buffer* CountsBuffer;
    public Buffer* VertexBuffer;
    public Buffer* IndexBuffer;
}