namespace CubeTest; 

public static class ResourceHelpers {
	/// <summary>
	///	Reads an embedded resource from the assembly and returns it as a byte array.
	/// </summary>
	/// <param name="name">The name of the resource to read.</param>
	/// <returns>The resource as a byte array.</returns>
	public static byte[] ReadResource(string name) {
		var assembly = typeof(ResourceHelpers).Assembly;
		var stream   = assembly.GetManifestResourceStream(assembly.GetName().Name + "." + name.Replace("/", "."));

		if (stream == null) {
			throw new Exception($"Unable to find resource {name}.");
		}

		var buffer = new byte[stream.Length];
		stream.Read(buffer, 0, buffer.Length);

		return buffer;
	}
}
