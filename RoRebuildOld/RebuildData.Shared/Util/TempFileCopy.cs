using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RebuildData.Shared.Util
{
	public class TempFileCopy : IDisposable
	{
		public string Path;

		public TempFileCopy(string path)
		{
			var fname = System.IO.Path.GetFileNameWithoutExtension(path);
			var ext = System.IO.Path.GetExtension(path);
			var g = Guid.NewGuid();

			var newFileName = $"{fname}{g}{ext}";
			
			Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), newFileName);

			File.Copy(path, Path);
		}

		public void Dispose()
		{
			File.Delete(Path);
		}
	}
}
