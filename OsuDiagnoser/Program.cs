using System;
using System.IO;
using System.Linq;
using System.Net;

namespace OsuDiagnoser
{
	internal static class Program
	{
		private static void Main()
		{
			Console.WriteLine(@"osu! Diagnoser: a tool to find missing required files in your osu! maps folder.
This tool is not designed to work with osu! lazer versions.
If your osu! version ends in -lazer, use lazer2stable to export your files first.
");
			
			Console.Write("Please locate your osu! \"Songs\" folder >> ");
			var raw = new DirectoryInfo(Console.ReadLine()!);
			
			DirectoryInfo osuFolder;
			
			if (!raw.Exists)
			{
				Console.WriteLine("That directory does not exist.");
				return;
			}

			switch (raw.Name)
			{
				// songs folder found!
				case "Songs" when raw.Parent is { Name: "osu" }:
					osuFolder = raw;
					break;
				// osu root folder instead
				case "osu" when raw.EnumerateDirectories().Any(d => d.Name   == "Songs"):
					osuFolder = raw.EnumerateDirectories().First(d => d.Name == "Songs");
					break;
				default:
					Console.WriteLine("That directory is not valid.");
					return;
			}

			var (maps, sbs)       = OsuMapTools.ScanForMaps(osuFolder);
			var (mapDeps, sbDeps) = OsuMapTools.GetDeps(maps, sbs);

			var flattenedDeps = mapDeps.Concat(sbDeps)
									   .SelectMany(set => set.SelectMany(m => m.audioFile == null
																				  ? m.sbFiles
																				  : m.sbFiles.Append(m.audioFile!)))
									   .ToArray();
			
			var dependedOnFiles = flattenedDeps.Distinct().ToArray();
			
			Console.WriteLine($"Total of {flattenedDeps.Length} dependencies on {dependedOnFiles.Length} unique files");
			
			Console.Write("Checking that all files exist... ");
			var filesChecked      = dependedOnFiles.Select(file => (file, exists: File.Exists(file))).ToArray();
			var missingFiles      = filesChecked.Where(f => !f.exists).ToArray();
			var existingFileCount = filesChecked.Length - missingFiles.Length;
			Console.WriteLine($"{existingFileCount} / {filesChecked.Length} exist - {missingFiles.Length} missing");
			
			File.WriteAllLines("missing.txt", missingFiles.Select(f => f.file));
			Console.WriteLine("Saved missing file list to missing.txt");
		}
	}
}