using System.Buffers.Text;
using System.Numerics;
using System.Runtime.CompilerServices;
using CubeTest.World;
using Silk.NET.SDL;

namespace CubeTest.ModelLoader.WavefrontObj;

public class ObjModelLoader : ModelLoader {
	private class VertexDefinitions {
		public Vector3[] Positions;
		public Vector3[] Normals;
		public Vector2[] TexCoords;

		public uint[] PositionIndices;
		public uint[] NormalIndices;
		public uint[] TexCoordIndices;
	}

	public override Model LoadModel(byte[] data) {
		(int position, int normal, int texture) vertCount = this.CountVertices(data);

		int indicesCount = this.CountIndices(data);

		VertexDefinitions definitions = new VertexDefinitions {
			Positions       = new Vector3[vertCount.position],
			Normals         = new Vector3[vertCount.normal],
			TexCoords       = new Vector2[vertCount.texture],
			PositionIndices = new uint[indicesCount],
			NormalIndices   = new uint[indicesCount],
			TexCoordIndices = new uint[indicesCount]
		};

		int faceCount = this.ReadVerticesData(data, definitions);

		Model model = new Model {
			Vertices = new WorldVertex[faceCount * 3],
			Indices  = new uint[faceCount        * 3]
		};

		for (int i = 0; i < model.Indices.Length; i++)
			model.Indices[i] = (uint)i;

		for (int i = 0; i < faceCount; i++) {
			model.Vertices[i * 3 + 0].Position = definitions.Positions[definitions.PositionIndices[i * 3 + 0]];
			model.Vertices[i * 3 + 1].Position = definitions.Positions[definitions.PositionIndices[i * 3 + 1]];
			model.Vertices[i * 3 + 2].Position = definitions.Positions[definitions.PositionIndices[i * 3 + 2]];

			model.Vertices[i * 3 + 0].Normal = definitions.Normals[definitions.NormalIndices[i * 3 + 0]];
			model.Vertices[i * 3 + 1].Normal = definitions.Normals[definitions.NormalIndices[i * 3 + 1]];
			model.Vertices[i * 3 + 2].Normal = definitions.Normals[definitions.NormalIndices[i * 3 + 2]];

			model.Vertices[i * 3 + 0].TexCoord = definitions.TexCoords[definitions.TexCoordIndices[i * 3 + 0]] * new Vector2(1, -1);
			model.Vertices[i * 3 + 1].TexCoord = definitions.TexCoords[definitions.TexCoordIndices[i * 3 + 1]] * new Vector2(1, -1);
			model.Vertices[i * 3 + 2].TexCoord = definitions.TexCoords[definitions.TexCoordIndices[i * 3 + 2]] * new Vector2(1, -1);
		}

		return model;
	}

	private int ReadVerticesData(byte[] data, VertexDefinitions definition) {
		int start = 0;
		int end   = 0;

		int positionVertexIndex = 0;
		int normalVertexIndex   = 0;
		int textureVertexIndex  = 0;

		int positionIndexIndex = 0;
		int normalIndexIndex   = 0;
		int textureIndexIndex  = 0;

		int faces = 0;

#if DEBUG
		using Stream @out = Console.OpenStandardOutput();
#endif

		float ReadFloat(ref int startIndex, ReadOnlySpan<byte> span) {
			int endIndex = 0;
			for (int i = startIndex; i < span.Length; i++) {
				if (span[i] != ' ')
					continue;

				endIndex = i;
				break;
			}
			if (endIndex == 0)
				endIndex = span.Length;

			ReadOnlySpan<byte> floatSpan = span[startIndex..endIndex];

#if DEBUG
			// Console.Write("Parsing float \"");
			// @out.Write(floatSpan);
			// @out.Flush();
			// Console.WriteLine("\"");
#endif

			if (floatSpan.Length == 0)
				throw new Exception("Failed to parse float");

			if (!Utf8Parser.TryParse(floatSpan, out float result, out int bytesConsumed))
				throw new Exception("Failed to parse float");

			startIndex = endIndex + 1;
			return result;
		}

		(uint p, uint t, uint n) ReadIndex(ref int startIndex, ReadOnlySpan<byte> span) {
			int endIndex = 0;
			for (int i = startIndex; i < span.Length; i++) {
				if (span[i] != ' ')
					continue;

				endIndex = i;
				break;
			}
			if (endIndex == 0)
				endIndex = span.Length;

			ReadOnlySpan<byte> intSpan = span[startIndex..endIndex];

#if DEBUG
			// Console.Write("Parsing int \"");
			// @out.Write(intSpan);
			// @out.Flush();
			// Console.WriteLine("\"");
#endif

			if (intSpan.Length == 0)
				throw new Exception("Failed to parse float");

			int parsed = 0;

			if (!Utf8Parser.TryParse(intSpan, out uint resultP, out int bytesConsumed))
				throw new Exception("Failed to parse float");
			parsed += bytesConsumed + 1;

			uint resultT = 0;
			if (parsed < intSpan.Length && !Utf8Parser.TryParse(intSpan[parsed..], out resultT, out bytesConsumed))
				throw new Exception("Failed to parse float");
			parsed += bytesConsumed + 1;

			uint resultN = 0;
			if (parsed < intSpan.Length && !Utf8Parser.TryParse(intSpan[parsed..], out resultN, out bytesConsumed))
				throw new Exception("Failed to parse float");
			parsed += bytesConsumed + 1;

			startIndex = endIndex + 1;
			//obj files are 1-indexed, so lets subtract 1
			return (resultP - 1, resultT - 1, resultN - 1);
		}

		void ParseLine(ReadOnlySpan<byte> line) {
#if DEBUG
			// Console.Write("Parsing line \"");
			// @out.Write(line);
			// @out.Flush();
			// Console.WriteLine("\"");
#endif

			if (line.Length == 0)
				return;

			//trigger for v and not for vt
			if (line[0] == 'v' && line[1] == ' ') {
				//get a span of the line without the first character and space
				ReadOnlySpan<byte> lineWithoutFirstChar = line[2..];

				int startIndex = 0;
				definition.Positions[positionVertexIndex].X = ReadFloat(ref startIndex, lineWithoutFirstChar);
				definition.Positions[positionVertexIndex].Y = ReadFloat(ref startIndex, lineWithoutFirstChar);
				definition.Positions[positionVertexIndex].Z = ReadFloat(ref startIndex, lineWithoutFirstChar);

				positionVertexIndex++;
			}
			//trigger for vt
			if (line[0] == 'v' && line[1] == 't') {
				//get a span of the line without the first 2 characters
				ReadOnlySpan<byte> lineWithoutFirstChar = line[3..];

				int startIndex = 0;
				definition.TexCoords[textureVertexIndex].X = ReadFloat(ref startIndex, lineWithoutFirstChar);
				definition.TexCoords[textureVertexIndex].Y = ReadFloat(ref startIndex, lineWithoutFirstChar);

				textureVertexIndex++;
			}
			//trigger for vn
			if (line[0] == 'v' && line[1] == 'n') {
				//get a span of the line without the first 2 characters
				ReadOnlySpan<byte> lineWithoutFirstChar = line[3..];

				int startIndex = 0;
				definition.Normals[normalVertexIndex].X = ReadFloat(ref startIndex, lineWithoutFirstChar);
				definition.Normals[normalVertexIndex].Y = ReadFloat(ref startIndex, lineWithoutFirstChar);
				definition.Normals[normalVertexIndex].Z = ReadFloat(ref startIndex, lineWithoutFirstChar);

				normalVertexIndex++;
			}
			//Trigger for `f` and not `fx`
			else if (line[0] == 'f' && line[1] == ' ') {
				//we found a face, so lets increment the face count
				faces++;

				//get a span of the line without the first character and space
				ReadOnlySpan<byte> lineWithoutFirstChar = line[2..];

				int startIndex = 0;

				//Read the first index sex
				(uint p, uint t, uint n) index = ReadIndex(ref startIndex, lineWithoutFirstChar);

				definition.PositionIndices[positionIndexIndex++] = index.p;
				definition.TexCoordIndices[textureIndexIndex++]  = index.t;
				definition.NormalIndices[normalIndexIndex++]     = index.n;

				//Read the second index set
				index = ReadIndex(ref startIndex, lineWithoutFirstChar);

				definition.PositionIndices[positionIndexIndex++] = index.p;
				definition.TexCoordIndices[textureIndexIndex++]  = index.t;
				definition.NormalIndices[normalIndexIndex++]     = index.n;

				//Read the third index set
				index = ReadIndex(ref startIndex, lineWithoutFirstChar);

				definition.PositionIndices[positionIndexIndex++] = index.p;
				definition.TexCoordIndices[textureIndexIndex++]  = index.t;
				definition.NormalIndices[normalIndexIndex++]     = index.n;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		bool IsSomeNewline(byte b) {
			return b == '\r' || b == '\n';
		}

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

		return faces;
	}

	private (int position, int normal, int texture) CountVertices(byte[] data) {
		//we set to true here so that it gets picked up on the first line, if that line is a vertex
		bool wasLastCharNewline     = true;
		bool lastWasLastCharNewline = false;

		int verticesP = 0;
		int verticesN = 0;
		int verticesT = 0;

		byte last = 0;
		for (int i = 0; i < data.Length; i++) {
			byte curr = data[i];

			if (curr == 'v' && wasLastCharNewline)
				verticesP++;

			if (last == 'v' && curr != ' ' && lastWasLastCharNewline)
				verticesP--;

			if (last == 'v' && curr == 'n' && lastWasLastCharNewline)
				verticesN++;

			if (last == 'v' && curr == 't' && lastWasLastCharNewline)
				verticesT++;

			lastWasLastCharNewline = wasLastCharNewline;
			if (curr == '\r' || curr == '\n')
				wasLastCharNewline = true;
			else
				wasLastCharNewline = false;

			last = curr;
		}

		return (verticesP, verticesN, verticesT);
	}

	private int CountIndices(byte[] data) {
		//we set to true here so that it gets picked up on the first line, if that line is a vertex
		bool wasLastCharNewline     = true;
		bool lastWasLastCharNewline = false;

		int vertices = 0;

		byte last = 0;
		for (int i = 0; i < data.Length; i++) {
			byte curr = data[i];

			if (curr == 'f' && wasLastCharNewline)
				//TODO: count the number of actual vertices in the face, right now lets just assume a triangle
				vertices += 3;

			if (last == 'f' && curr != ' ' && lastWasLastCharNewline)
				vertices -= 3;

			lastWasLastCharNewline = wasLastCharNewline;
			if (curr == '\r' || curr == '\n')
				wasLastCharNewline = true;
			else
				wasLastCharNewline = false;

			last = curr;
		}

		return vertices;
	}
}
