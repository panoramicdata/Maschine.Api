namespace Maschine.Demo;

internal sealed record SoundFontPreset(
string Id,
DrumSoundfontPlayer.InstrumentMode Mode,
int Variant,
string DisplayName,
string FileName,
string Url,
string Attribution,
int MidiChannel,
int BaseNote,
int ProgramNumber = -1);

internal sealed record ResolvedSoundFontPreset(SoundFontPreset Preset, string LocalPath);
