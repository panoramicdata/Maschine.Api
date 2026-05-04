namespace Maschine.Api.Test;

public sealed class MikroMk3ProtocolTests
{
	// ── Constants ────────────────────────────────────────────────────────────

	[Fact]
	public void PadLedReportId_Is0x80() => MikroMk3Protocol.PadLedReportId.Should().Be(0x80);

	[Fact]
	public void PadPressureReportId_Is0x02() => MikroMk3Protocol.PadPressureReportId.Should().Be(0x02);

	[Fact]
	public void ButtonReportId_Is0x01() => MikroMk3Protocol.ButtonReportId.Should().Be(0x01);

	[Fact]
	public void PadPressureReportLength_Is64() => MikroMk3Protocol.PadPressureReportLength.Should().Be(64);

	[Fact]
	public void ButtonReportLength_Is14() => MikroMk3Protocol.ButtonReportLength.Should().Be(14);

	[Fact]
	public void PadLedReportLength_Is49() => MikroMk3Protocol.PadLedReportLength.Should().Be(49);

	// ── ParsePadPressureReport ───────────────────────────────────────────────

	[Fact]
	public void ParsePadPressureReport_IdlePressure_ReturnsNotPressed()
	{
		var report = new byte[MikroMk3Protocol.PadPressureReportLength];
		report[0] = MikroMk3Protocol.PadPressureReportId;
		report[1] = 0;    // pad index 0
		report[2] = 0x40; // idle/rest pressure

		var state = MikroMk3Protocol.ParsePadPressureReport(report);

		state.IsPressed.Should().BeFalse();
		state.Index.Should().Be(0);
	}

	[Fact]
	public void ParsePadPressureReport_ActivePressure_IsPressed()
	{
		var report = new byte[MikroMk3Protocol.PadPressureReportLength];
		report[0] = MikroMk3Protocol.PadPressureReportId;
		report[1] = 5;    // pad index 5
		report[2] = 0x50; // active: raw - 0x40 = 0x10 → pressure = 0x10 * 256 = 4096

		var state = MikroMk3Protocol.ParsePadPressureReport(report);

		state.IsPressed.Should().BeTrue();
		state.Index.Should().Be(5);
		state.Pressure.Should().Be(0x10 * 256);
	}

	[Fact]
	public void ParsePadPressureReport_Pad15_ReturnsCorrectIndex()
	{
		var report = new byte[MikroMk3Protocol.PadPressureReportLength];
		report[0] = MikroMk3Protocol.PadPressureReportId;
		report[1] = 15;   // pad index 15
		report[2] = 0x50; // active pressure

		var state = MikroMk3Protocol.ParsePadPressureReport(report);

		state.Index.Should().Be(15);
		state.IsPressed.Should().BeTrue();
	}

	[Fact]
	public void ParsePadPressureReport_NullReport_Throws()
		=> ((Func<PadState>)(() => MikroMk3Protocol.ParsePadPressureReport(null!)))
			.Should().Throw<ArgumentNullException>();

	[Fact]
	public void ParsePadPressureReport_TooShort_Throws()
	{
		var report = new byte[10];
		report[0] = MikroMk3Protocol.PadPressureReportId;
		((Func<PadState>)(() => MikroMk3Protocol.ParsePadPressureReport(report)))
			.Should().Throw<ArgumentException>().WithMessage("*too short*");
	}

	[Fact]
	public void ParsePadPressureReport_WrongReportId_Throws()
	{
		var report = new byte[MikroMk3Protocol.PadPressureReportLength];
		report[0] = 0xFF;
		((Func<PadState>)(() => MikroMk3Protocol.ParsePadPressureReport(report)))
			.Should().Throw<ArgumentException>().WithMessage("*report ID*");
	}

	// ── ParseButtonReport ────────────────────────────────────────────────────

	[Fact]
	public void ParseButtonReport_AllZero_NoButtonPressed()
	{
		var report = new byte[MikroMk3Protocol.ButtonReportLength];
		report[0] = MikroMk3Protocol.ButtonReportId;

		var states = MikroMk3Protocol.ParseButtonReport(report);

		states.Should().HaveCount(MaschineDeviceConstants.MikroMk3ButtonCount);
		states.Should().AllSatisfy(s => s.IsPressed.Should().BeFalse());
	}

	[Fact]
	public void ParseButtonReport_Button0Set_IsPressed()
	{
		var report = new byte[MikroMk3Protocol.ButtonReportLength];
		report[0] = MikroMk3Protocol.ButtonReportId;
		report[1] = 0x01; // bit 0 = button 0

		var states = MikroMk3Protocol.ParseButtonReport(report);

		states[0].IsPressed.Should().BeTrue();
	}

	[Fact]
	public void ParseButtonReport_Button7Set_IsPressed()
	{
		var report = new byte[MikroMk3Protocol.ButtonReportLength];
		report[0] = MikroMk3Protocol.ButtonReportId;
		report[1] = 0x80; // bit 7 = button 7

		var states = MikroMk3Protocol.ParseButtonReport(report);

		states[7].IsPressed.Should().BeTrue();
	}

	[Fact]
	public void ParseButtonReport_Button8Set_IsPressed()
	{
		var report = new byte[MikroMk3Protocol.ButtonReportLength];
		report[0] = MikroMk3Protocol.ButtonReportId;
		report[2] = 0x01; // byte 2, bit 0 = button 8

		var states = MikroMk3Protocol.ParseButtonReport(report);

		states[8].IsPressed.Should().BeTrue();
	}

	[Fact]
	public void ParseButtonReport_AllButtons_AreIndexedCorrectly()
	{
		var report = new byte[MikroMk3Protocol.ButtonReportLength];
		report[0] = MikroMk3Protocol.ButtonReportId;

		var states = MikroMk3Protocol.ParseButtonReport(report);

		for (var i = 0; i < states.Count; i++)
		{
			states[i].Index.Should().Be(i);
		}
	}

	[Fact]
	public void ParseButtonReport_NullReport_Throws()
		=> ((Func<IReadOnlyList<ButtonState>>)(() => MikroMk3Protocol.ParseButtonReport(null!)))
			.Should().Throw<ArgumentNullException>();

	[Fact]
	public void ParseButtonReport_TooShort_Throws()
	{
		var report = new byte[2];
		report[0] = MikroMk3Protocol.ButtonReportId;
		((Func<IReadOnlyList<ButtonState>>)(() => MikroMk3Protocol.ParseButtonReport(report)))
			.Should().Throw<ArgumentException>().WithMessage("*too short*");
	}

	[Fact]
	public void ParseButtonReport_WrongReportId_Throws()
	{
		var report = new byte[MikroMk3Protocol.ButtonReportLength];
		report[0] = 0xFF;
		((Func<IReadOnlyList<ButtonState>>)(() => MikroMk3Protocol.ParseButtonReport(report)))
			.Should().Throw<ArgumentException>().WithMessage("*report ID*");
	}

	// ── BuildSinglePadColorReport ─────────────────────────────────────────

	[Fact]
	public void BuildSinglePadColorReport_Pad0Red_SetsCorrectBytes()
	{
		var report = MikroMk3Protocol.BuildSinglePadColorReport(0, PadColor.Red);

		report[0].Should().Be(MikroMk3Protocol.PadLedReportId);
		report[1].Should().Be(255); // R
		report[2].Should().Be(0);   // G
		report[3].Should().Be(0);   // B
	}

	[Fact]
	public void BuildSinglePadColorReport_Pad15_SetsCorrectOffset()
	{
		var report = MikroMk3Protocol.BuildSinglePadColorReport(15, PadColor.Blue);

		const int offset = 1 + (15 * 3);
		report[offset].Should().Be(0);   // R
		report[offset + 1].Should().Be(0);   // G
		report[offset + 2].Should().Be(255); // B
	}

	[Fact]
	public void BuildSinglePadColorReport_HasCorrectLength()
		=> MikroMk3Protocol.BuildSinglePadColorReport(0, PadColor.Off)
			.Should().HaveCount(MikroMk3Protocol.PadLedReportLength);

	[Fact]
	public void BuildSinglePadColorReport_NegativePadIndex_Throws()
		=> ((Action)(() => MikroMk3Protocol.BuildSinglePadColorReport(-1, PadColor.Off)))
			.Should().Throw<ArgumentOutOfRangeException>();

	[Fact]
	public void BuildSinglePadColorReport_OutOfRangePadIndex_Throws()
		=> ((Action)(() => MikroMk3Protocol.BuildSinglePadColorReport(16, PadColor.Off)))
			.Should().Throw<ArgumentOutOfRangeException>();

	// ── BuildAllPadsColorReport ───────────────────────────────────────────

	[Fact]
	public void BuildAllPadsColorReport_White_AllPadsAreWhite()
	{
		var report = MikroMk3Protocol.BuildAllPadsColorReport(PadColor.White);

		report[0].Should().Be(MikroMk3Protocol.PadLedReportId);
		for (var i = 0; i < MaschineDeviceConstants.MikroMk3PadCount; i++)
		{
			var offset = 1 + (i * 3);
			report[offset].Should().Be(255, $"pad {i} R");
			report[offset + 1].Should().Be(255, $"pad {i} G");
			report[offset + 2].Should().Be(255, $"pad {i} B");
		}
	}

	[Fact]
	public void BuildAllPadsColorReport_Off_AllPadsAreOff()
	{
		var report = MikroMk3Protocol.BuildAllPadsColorReport(PadColor.Off);

		for (var i = 1; i < report.Length; i++)
		{
			report[i].Should().Be(0, $"byte index {i}");
		}
	}

	[Fact]
	public void BuildAllPadsColorReport_HasCorrectLength()
		=> MikroMk3Protocol.BuildAllPadsColorReport(PadColor.Off)
			.Should().HaveCount(MikroMk3Protocol.PadLedReportLength);
}
