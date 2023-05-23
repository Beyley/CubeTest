namespace CubeTest.Game;

public unsafe struct Chunk {
	public const int CHUNK_SIZE    = 16;
	public const int CHUNK_SIZE_SQ = CHUNK_SIZE * CHUNK_SIZE;
	public const int CHUNK_SIZE_CU = CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE;

	public int ChunkX; 
	public int ChunkY; 
	public fixed uint Blocks[CHUNK_SIZE_CU];

	/// <summary>
	/// Gets an index into the blocks array from the position
	/// </summary>
	/// <param name="x">X pos</param>
	/// <param name="y">Y pos</param>
	/// <param name="z">Z pos</param>
	/// <returns>The index</returns>
	public int IndexFromPos(int x, int y, int z) {
		return CHUNK_SIZE_SQ * y + CHUNK_SIZE * z + x;
	}
}
