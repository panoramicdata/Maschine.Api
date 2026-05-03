using System.ComponentModel.DataAnnotations;

namespace Maschine.Api.Models;

/// <summary>
/// Named physical buttons for the Maschine Mikro MK3.
/// Values map directly to hardware button indices from input reports.
/// </summary>
#pragma warning disable CS1591 // enum member docs are intentionally represented via DisplayAttribute labels
public enum MikroMk3Button
{
	[Display(Name = "MACHINE Logo")]
	MachineLogo = 0,

	[Display(Name = "Star")]
	Star = 1,

	[Display(Name = "Search")]
	Search = 2,

	[Display(Name = "VOLUME\n[Velocity]")]
	VolumeVelocity = 3,

	[Display(Name = "SWING\n[Position]")]
	Swing = 4,

	[Display(Name = "TEMPO\n[Tune]")]
	TempoTune = 5,

	[Display(Name = "PLUG-IN\nMacro")]
	PlugInMicrok8s = 6,

	[Display(Name = "SAMPLING")]
	Sampling = 7,

	[Display(Name = "\u2190")]  // Left arrow
	LeftArrow = 8,

	[Display(Name = "\u2192")]  // Right arrow
	RightArrow = 9,

	[Display(Name = "PITCH")]
	Pitch = 10,

	[Display(Name = "MOD")]
	Mod = 11,

	[Display(Name = "PERFORM\nFX Select")]
	PerformFxSelect = 12,

	[Display(Name = "NOTES")]
	Notes = 13,

	[Display(Name = "GROUP")]
	Group = 14,

	[Display(Name = "AUTO")]
	Auto = 15,

	[Display(Name = "LOCK")]
	Lock = 16,

	[Display(Name = "NOTE REPEAT\nArp")]
	NoteRepeatArp = 17,

	[Display(Name = "RESTART\nLoop")]
	RestartLoop = 18,

	[Display(Name = "ERASE\nReplace")]
	ArraysReplace = 19,

	[Display(Name = "TAP\nMetro")]
	Tapro = 20,

	[Display(Name = "FOLLOW\nGrid")]
	FollowGrid = 21,

	[Display(Name = "\u2192 PLAY")]
	Play = 22,

	[Display(Name = "\u25CF REC\nCount-In")]
	RecCountIn = 23,

// Stop symbol (square) as unicode
	[Display(Name = "\u25A0 STOP")]
	Stop = 24,

	[Display(Name = "SHIFT")]
	Shift = 25,

	[Display(Name = "FIXED VEL\n16 Vel")]
	FixedVel16Vel = 26,

	[Display(Name = "PAD MODE")]
	PadMode = 27,

	[Display(Name = "KEYBOARD")]
	Keyboard = 28,

	[Display(Name = "CHORDS")]
	Chords = 29,

	[Display(Name = "STEP")]
	Step = 30,

	[Display(Name = "SCENE\nSelection")]
	SceneSelection = 31,

	[Display(Name = "PATTERN")]
	Pattern = 32,

	[Display(Name = "EVENTS")]
	Events = 33,

	[Display(Name = "VARIATION\nNavigate")]
	VariationNavigate = 34,

	[Display(Name = "DUPLICATE\nDouble")]
	DuplicateDouble = 35,

	[Display(Name = "SELECT")]
	Select = 36,

	[Display(Name = "SOLO")]
	Solo = 37,

	[Display(Name = "MUTE\nChoke")]
	MuteChoke = 38,

	[Display(Name = "Knob Press")]
	KnobPress = 39,
}
#pragma warning restore CS1591
