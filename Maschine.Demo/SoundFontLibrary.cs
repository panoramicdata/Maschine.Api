using Microsoft.Extensions.Logging;
using System.Text;

namespace Maschine.Demo;

/// <summary>
/// The soundfonts the demo plays: the catalogue, where they are cached on disk, and the
/// download that fetches any that are missing.
/// </summary>
internal static class SoundFontLibrary
{
	/// <summary>Every soundfont the demo can use, in presentation order.</summary>
	internal static readonly SoundFontPreset[] Catalogue =
	[
		new(
		"pad-brd",
		DrumSoundfontPlayer.InstrumentMode.PadMode,
		0,
		"Pad BRD Kit",
		"Processed_BRD_Kit.sf2",
		"https://musical-artifacts.com/artifacts/7365/Processed_BRD_Kit.sf2",
		"Processed BRD Kit (public domain) via Musical Artifacts",
		MidiChannel: 9,
		BaseNote: 36),
	new(
		"pad-zappa",
		DrumSoundfontPlayer.InstrumentMode.PadMode,
		1,
		"Pad Zappa Kit",
		"ZappaKit.sf2",
		"https://archive.org/download/ZappaKit.sf2/ZappaKit.sf2",
		"ZappaKit via Internet Archive",
		MidiChannel: 9,
		BaseNote: 36),
	new(
		"pad-retro",
		DrumSoundfontPlayer.InstrumentMode.PadMode,
		2,
		"Pad Retro",
		"Retro_Synth_PC.sf2",
		"https://archive.org/download/xmplayer.-7z/Retro_Synth_PC.sf2",
		"Retro Synth PC via Internet Archive",
		MidiChannel: 9,
		BaseNote: 36),
	new(
		"keys-space",
		DrumSoundfontPlayer.InstrumentMode.Keyboard,
		1,
		"Space Keys",
		"LX-Space.sf2",
		"https://archive.org/download/LXSpace/LX-Space.sf2",
		"LX-Space via Internet Archive",
		MidiChannel: 0,
		BaseNote: 60),
	new(
		"keys-piano",
		DrumSoundfontPlayer.InstrumentMode.Keyboard,
		0,
		"Stein Piano",
		"WST25FStein_00Sep22.sf2",
		"https://archive.org/download/WST25FStein_00Sep22.sf2/WST25FStein_00Sep22.sf2",
		"WST25FStein via Internet Archive",
		MidiChannel: 0,
		BaseNote: 62),
	new(
		"keys-retro",
		DrumSoundfontPlayer.InstrumentMode.Keyboard,
		2,
		"JV Harpsichord",
		"Roland JV-1080 GM.sf2",
		"https://archive.org/download/gabedudleyssf2collection/Roland%20JV-1080%20GM.sf2",
		"Roland JV-1080 GM via Internet Archive",
		MidiChannel: 0,
		BaseNote: 64,
		ProgramNumber: 6),
	new(
		"chords-space",
		DrumSoundfontPlayer.InstrumentMode.Chords,
		0,
		"Space Chords",
		"LX-Space.sf2",
		"https://archive.org/download/LXSpace/LX-Space.sf2",
		"LX-Space via Internet Archive",
		MidiChannel: 0,
		BaseNote: 48),
	new(
		"chords-zappa",
		DrumSoundfontPlayer.InstrumentMode.Chords,
		1,
		"Zappa Chords",
		"ZappaKit.sf2",
		"https://archive.org/download/ZappaKit.sf2/ZappaKit.sf2",
		"ZappaKit via Internet Archive",
		MidiChannel: 0,
		BaseNote: 52),
	new(
		"chords-retro",
		DrumSoundfontPlayer.InstrumentMode.Chords,
		2,
		"Retro Synth",
		"Retro_Synth_PC.sf2",
		"https://archive.org/download/xmplayer.-7z/Retro_Synth_PC.sf2",
		"Retro Synth PC via Internet Archive",
		MidiChannel: 0,
		BaseNote: 55),
];

	internal static async Task<IReadOnlyList<ResolvedSoundFontPreset>> EnsureSoundFontsAsync(ILogger logger, CancellationToken cancellationToken)
	{
		var cacheDirectory = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"Maschine.Api",
			"DemoAssets");

		Directory.CreateDirectory(cacheDirectory);
		var resolved = new List<ResolvedSoundFontPreset>(Catalogue.Length);
		var total = Catalogue.Length;

		for (var i = 0; i < Catalogue.Length; i++)
		{
			var preset = Catalogue[i];
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

	internal static Dictionary<DrumSoundfontPlayer.InstrumentMode, IReadOnlyList<ResolvedSoundFontPreset>> BuildPresetsByMode(IReadOnlyList<ResolvedSoundFontPreset> presets)
	{
		return presets
			.GroupBy(p => p.Preset.Mode)
			.ToDictionary(
				g => g.Key,
				g => (IReadOnlyList<ResolvedSoundFontPreset>)g.OrderBy(p => p.Preset.Variant).ToArray());
	}
}
