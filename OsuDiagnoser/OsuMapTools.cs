using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace OsuDiagnoser
{
	public static class OsuMapTools
	{
		private static readonly Regex SbFileRegex    = new(".*,\"(.*)\",.*", RegexOptions.Compiled);
		private static readonly Regex AudioFileRegex = new("AudioFilename: (.*)", RegexOptions.Compiled);
		
		public static (FileInfo[][] maps, FileInfo[][] sbs) ScanForMaps(DirectoryInfo osuFolder)
		{
			Console.Write("\nScanning for maps... ");
			var sw   = Stopwatch.StartNew();
			var sets = osuFolder.GetDirectories();
			var maps = sets.Select(s => s.EnumerateFiles().Where(f => f.Extension == ".osu").ToArray())
						   .ToArray();
			var sbs = sets.Select(s => s.EnumerateFiles().Where(f => f.Extension == ".osb").ToArray())
						  .Where(set => set.Any())
						  .ToArray();
			sw.Stop();
			Console.WriteLine($@"Found:
    {maps.SelectMany(s => s).Count()} maps
    {sbs.SelectMany(s => s).Count()} storyboards
    in {sets.Length} sets.
    (Took {sw.ElapsedMilliseconds}ms)
");
			return (maps, sbs);
		}

		public static (string? audioFile, string[] sbFiles) GetMapDependencies(FileInfo map)
		{
			using var sr = new StreamReader(map.OpenRead());
			var mapRawText = sr.ReadToEnd()
							   .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

			var filePrefix = map.DirectoryName!;
			
			string? audioFile = null;
			var     files     = new List<string>();
			var     hitEvents = false;
			
			foreach (var line in mapRawText)
			{
				// is this line the map music?
				if (AudioFileRegex.IsMatch(line))
					audioFile = Path.Combine(filePrefix, AudioFileRegex.Replace(line, "$1").Replace('\\', '/'));
				
				// only bother parsing in the right bit of the map file
				if (line == "[Events]")
				{
					hitEvents = true;
					continue;
				}
				if (!hitEvents) continue;
				if (line.StartsWith('[')) break;

				if (!SbFileRegex.IsMatch(line)) continue;
				
				var fileName = SbFileRegex.Replace(line, "$1");
				if (!files.Contains(fileName))
					files.Add(Path.Combine(filePrefix, fileName.Replace('\\', '/')));
			}

			return (audioFile, files.ToArray());
		}

		public static ((string? audioFile, string[] sbFiles)[][] mapDeps, (string? audioFile, string[] sbFiles)[][] sbDeps)
			GetDeps(FileInfo[][] maps, FileInfo[][] sbs)
		{
			Console.Write("Reading files required by maps and storyboards... ");
			var sw      = Stopwatch.StartNew();
			var mapDeps = MultiThreadTool.BatchTask(maps, set => set.Select(OsuMapTools.GetMapDependencies).ToArray(), 8);
			//var mapDeps = maps.Select(set => set.Select(GetMapDependencies).ToArray()).ToArray();
			var sbDeps = MultiThreadTool.BatchTask(sbs, set => set.Select(OsuMapTools.GetMapDependencies).ToArray(), 8);
			//var sbDeps  = sbs.Select(set => set.Select(GetMapDependencies).ToArray()).ToArray();
			sw.Stop();
			Console.WriteLine($@"Found:
    {mapDeps.SelectMany(set => set).Select(p => p.sbFiles.Length + (p.audioFile != null ? 1 : 0)).Aggregate(0, (i, i1) => i + i1)} dependencies
        from {maps.SelectMany(s => s).Count()} maps
    {sbDeps.SelectMany(set => set).Select(p => p.sbFiles.Length + (p.audioFile != null ? 1 : 0)).Aggregate(0, (i, i1) => i + i1)} dependencies
        from {sbs.SelectMany(s => s).Count()} storyboards
    (Took {sw.ElapsedMilliseconds / 1000}s)
");
			return (mapDeps, sbDeps);
		}
	}
}