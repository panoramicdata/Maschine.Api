using MeltySynth;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using System.Text;

namespace Maschine.Demo;

/// <summary>
/// Soundfont acquisition for <see cref="DrumSoundfontPlayer"/>: locating the cached files,
/// downloading any that are missing, and the preset records describing them.
/// </summary>
internal sealed partial class DrumSoundfontPlayer
{
	private static async Task<IReadOnlyList<ResolvedSoundFontPreset>> EnsureSoundFontsAsync(ILogger logger, CancellationToken cancellationToken)
	{
		var cacheDirectory = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"Maschine.Api",
			"DemoAssets");

		Directory.CreateDirectory(cacheDirectory);
		var resolved = new List<ResolvedSoundFontPreset>(s_soundFontPresets.Length);
		var total = s_soundFontPresets.Length;

		for (var i = 0; i < s_soundFontPresets.Length; i++)
		{
			var preset = s_soundFontPresets[i];
			var localPath = Path.Combine(cacheDirectory, preset.FileName);

			if (File.Exists(localPath))
			{
				logger.LogInformation("[{Index}/{Total}] {Name,-10} {Bar} 100% (cached)", i + 1, total, preset.DisplayName, BuildProgressBar(1.0));
				var cached = new ResolvedSoundFontPreset(preset, localPath);
				resolved.Add(cached);
				continue;
			}

			try
			{
				await DownloadWithProgressAsync(preset, localPath, i + 1, total, logger, cancellationToken).ConfigureAwait(false);
				var downloaded = new ResolvedSoundFontPreset(preset, localPath);
				resolved.Add(downloaded);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				if (i == 0)
				{
					throw;
				}

				logger.LogWarning(ex, "Failed to download optional soundfont {Name}. One instrument variant will be unavailable.", preset.DisplayName);
			}
		}

		return resolved;
	}

	private static async Task DownloadWithProgressAsync(
		SoundFontPreset preset,
		string soundFontPath,
		int index,
		int total,
		ILogger logger,
		CancellationToken cancellationToken)
	{
		logger.LogInformation("[{Index}/{Total}] {Name,-10} {Bar}   0% (downloading)", index, total, preset.DisplayName, BuildProgressBar(0.0));

		var tempPath = soundFontPath + ".download";
		if (File.Exists(tempPath))
		{
			File.Delete(tempPath);
		}

		using var httpClient = new HttpClient();
		using var response = await httpClient.GetAsync(preset.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();

		var contentLength = response.Content.Headers.ContentLength;
		await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
		await using (var destination = File.Create(tempPath))
		{
			await CopyWithProgressAsync(source, destination, contentLength, progress
				=> logger.LogInformation(
					"[{Index}/{Total}] {Name,-10} {Bar} {Percent,3}%",
					index,
					total,
					preset.DisplayName,
					BuildProgressBar(progress),
					(int)Math.Round(progress * 100.0)),
				cancellationToken).ConfigureAwait(false);
		}

		if (File.Exists(soundFontPath))
		{
			File.Delete(soundFontPath);
		}

		File.Move(tempPath, soundFontPath);
		logger.LogInformation("[{Index}/{Total}] {Name,-10} {Bar} 100%", index, total, preset.DisplayName, BuildProgressBar(1.0));
	}

	/// <summary>
	/// Copies <paramref name="source"/> to <paramref name="destination"/>, invoking
	/// <paramref name="reportProgress"/> at each new multiple of 5%. Progress is only reported when
	/// the response declared a content length; otherwise the copy runs silently.
	/// </summary>
	private static async Task CopyWithProgressAsync(
		Stream source,
		Stream destination,
		long? contentLength,
		Action<double> reportProgress,
		CancellationToken cancellationToken)
	{
		var buffer = new byte[64 * 1024];
		long totalRead = 0;
		var lastPct = -1;

		while (true)
		{
			var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
			if (read == 0)
			{
				return;
			}

			await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
			totalRead += read;

			if (contentLength is null or <= 0)
			{
				continue;
			}

			var progress = Math.Clamp((double)totalRead / contentLength.Value, 0.0, 1.0);
			var pct = (int)Math.Round(progress * 100.0);
			if (pct != lastPct && pct % 5 == 0)
			{
				lastPct = pct;
				reportProgress(progress);
			}
		}
	}

	private static string BuildProgressBar(double progress)
	{
		const int width = 24;
		var clamped = Math.Clamp(progress, 0.0, 1.0);
		var filled = (int)Math.Round(clamped * width);
		var builder = new StringBuilder(width + 2);
		builder.Append('[');
		builder.Append('#', filled);
		builder.Append('.', width - filled);
		builder.Append(']');
		return builder.ToString();
	}

	private static Dictionary<InstrumentMode, IReadOnlyList<ResolvedSoundFontPreset>> BuildPresetsByMode(IReadOnlyList<ResolvedSoundFontPreset> presets)
	{
		return presets
			.GroupBy(p => p.Preset.Mode)
			.ToDictionary(
				g => g.Key,
				g => (IReadOnlyList<ResolvedSoundFontPreset>)g.OrderBy(p => p.Preset.Variant).ToArray());
	}

	private sealed record SoundFontPreset(
		string Id,
		InstrumentMode Mode,
		int Variant,
		string DisplayName,
		string FileName,
		string Url,
		string Attribution,
		int MidiChannel,
		int BaseNote,
		int ProgramNumber = -1);

	private sealed record ResolvedSoundFontPreset(SoundFontPreset Preset, string LocalPath);
}
