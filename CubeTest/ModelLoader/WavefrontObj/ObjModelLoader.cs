using System.Buffers.Text;
using System.Runtime.CompilerServices;

namespace CubeTest.ModelLoader.WavefrontObj; 

public class ObjModelLoader : global::CubeTest.ModelLoader.ModelLoader {
	public override Model LoadModel(byte[] data) {
		int vertexCount = this.CountVertices(data);
		WorldVertex[] vertices = new WorldVertex[vertexCount];
		this.ReadVertices(data, vertices);

		return new Model {
			Vertices = vertices
		};
	}
	
	private void ReadVertices(byte[] data, WorldVertex[] vertices) {
		int  start       = 0;
		int  end         = 0;

		int positionVertexIndex = 0;
		
		using Stream @out = Console.OpenStandardOutput();
		void ParseLine(ReadOnlySpan<byte> line) {
			// Console.Write("Parsing line \"");
			// @out.Write(line); @out.Flush();
			// Console.WriteLine("\"");
			
			if (line.Length == 0) {
				return;
			}

			//trigger for v and not for vt
			if (line[0] == 'v' && line[1] == ' ') {
				//get a span of the line without the first character and space
				ReadOnlySpan<byte> lineWithoutFirstChar = line[2..];
				
				float ReadFloat(ref int startIndex, ReadOnlySpan<byte> span) {
					int endIndex = 0;
					for (int i = startIndex; i < span.Length; i++) {
						if (span[i] != ' ')
							continue;
						
						endIndex = i;
						break;
					}
					if (endIndex == 0) {
						endIndex = span.Length;
					}

					ReadOnlySpan<byte> floatSpan = span[startIndex..endIndex];

					// Console.Write("Parsing float \"");
					// @out.Write(floatSpan); @out.Flush();
					// Console.WriteLine("\"");
					
					if (floatSpan.Length == 0) {
						throw new Exception("Failed to parse float");
					}
					
					if (!Utf8Parser.TryParse(floatSpan, out float result, out int bytesConsumed)) {
						throw new Exception("Failed to parse float");
					}
					
					startIndex = endIndex + 1;
					return result;
				}
				
				int startIndex = 0;
				vertices[positionVertexIndex].Position.X = ReadFloat(ref startIndex, lineWithoutFirstChar);
				vertices[positionVertexIndex].Position.Y = ReadFloat(ref startIndex, lineWithoutFirstChar);
				vertices[positionVertexIndex].Position.Z = ReadFloat(ref startIndex, lineWithoutFirstChar);

				positionVertexIndex++;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		bool IsSomeNewline(byte b) => b == '\r' || b == '\n';
		
		byte last = 0;
		for (int i = 0; i < data.Length; i++) {
			byte curr = data[i];
			
			if (IsSomeNewline(curr) && !IsSomeNewline(last)) {
				end = i;
				ParseLine(data.AsSpan(start, end - start));
				start = i + 1;
			} 
			
			last = curr;
		}
	}

	private int CountVertices(byte[] data) {
		//we set to true here so that it gets picked up on the first line, if that line is a vertex
		bool wasLastCharNewline     = true;
		bool lastWasLastCharNewline = false;

		int vertices = 0;

		byte last = 0;
		for (int i = 0; i < data.Length; i++) {
			byte curr = data[i];

			if (curr == 'v' && wasLastCharNewline) {
				vertices++;
			}

			if (last == 'v' && curr != ' ' && lastWasLastCharNewline) {
				vertices--;
			}

			lastWasLastCharNewline = wasLastCharNewline;
			if (curr == '\r' || curr == '\n') {
				wasLastCharNewline = true;
			} else {
				wasLastCharNewline = false;
			}

			last = curr;
		}

		return vertices;
	}
}
