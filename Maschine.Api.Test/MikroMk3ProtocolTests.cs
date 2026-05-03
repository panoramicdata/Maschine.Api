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
	public void ParsePadPressureReport_IdleValue_ReturnsPressureZero()
	{
		var report = new byte[MikroMk3Protocol.PadPressureReportLength];
		report[0] = MikroMk3Protocol.PadPressureReportId;
		report[1] = 0x05; // pad index 5
		report[2] = 0x40; // idle value → pressure = 0

		var state = MikroMk3Protocol.ParsePadPressureReport(report);

		state.Index.Should().Be(5);
		state.Pressure.Should().Be(0);
		state.IsPressed.Should().BeFalse();
	}

	[Fact]
	public void ParsePadPressureReport_ActivePressure_ReturnsScaledPressure()
	{
		var report = new byte[MikroMk3Protocol.PadPressureReportLength];
		report[0] = MikroMk3Protocol.PadPressureReportId;
		report[1] = 0x0C; // pad index 12 (physical pad 1, bottom-left)
		report[2] = 0x4D; // peak tap → 0x4D - 0x40 = 13; scaled = 13 * 256 = 3328

		var state = MikroMk3Protocol.ParsePadPressureReport(report);

		state.Index.Should().Be(0x0C);
		state.Pressure.Should().Be(13 * 256);
		state.IsPressed.Should().BeTrue();
	}

	[Fact]
	public void ParsePadPressureReport_ReleasedSignal_ReturnsPressureZero()
	{
		var report = new byte[MikroMk3Protocol.PadPressureReportLength];
		report[0] = MikroMk3Protocol.PadPressureReportId;
		report[1] = 0x03; // pad index 3
		report[2] = 0x30; // released/lifted signal → pressure = 0

		var state = MikroMk3Protocol.ParsePadPressureReport(report);

		state.Index.Should().Be(3);
		state.Pressure.Should().Be(0);
		state.IsPressed.Should().BeFalse();
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

	// ── ParseEncoderReport ───────────────────────────────────────────────────


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

	// ── Button LED constants ─────────────────────────────────────────────────

	[Fact]
	public void ButtonLedReportId_Is0x81() => MikroMk3Protocol.ButtonLedReportId.Should().Be(0x81);

	[Fact]
	public void ButtonLedReportLength_Is80() => MikroMk3Protocol.ButtonLedReportLength.Should().Be(80);

	// ── BuildButtonLedReport ─────────────────────────────────────────────────

	[Fact]
	public void BuildButtonLedReport_ValidIndex_SetsByteAtCorrectOffset()
	{
		var report = MikroMk3Protocol.BuildButtonLedReport(5, 100);

		report[0].Should().Be(MikroMk3Protocol.ButtonLedReportId);
		report[6].Should().Be(100);   // offset 1 + 5 = 6
		report.Should().HaveCount(MikroMk3Protocol.ButtonLedReportLength);
	}

	[Fact]
	public void BuildButtonLedReport_Index0_SetsByte1()
	{
		var report = MikroMk3Protocol.BuildButtonLedReport(0, 127);

		report[1].Should().Be(127);
	}

	[Fact]
	public void BuildButtonLedReport_LastIndex_SetsCorrectByte()
	{
		var lastIndex = MaschineDeviceConstants.MikroMk3ButtonCount - 1;
		var report = MikroMk3Protocol.BuildButtonLedReport(lastIndex, 50);

		report[1 + lastIndex].Should().Be(50);
	}

	[Fact]
	public void BuildButtonLedReport_NegativeIndex_Throws()
		=> ((Func<byte[]>)(() => MikroMk3Protocol.BuildButtonLedReport(-1, 0)))
			.Should().Throw<ArgumentOutOfRangeException>();

	[Fact]
	public void BuildButtonLedReport_IndexTooHigh_Throws()
		=> ((Func<byte[]>)(() => MikroMk3Protocol.BuildButtonLedReport(MaschineDeviceConstants.MikroMk3ButtonCount, 0)))
			.Should().Throw<ArgumentOutOfRangeException>();

	// ── BuildAllButtonLedsReport ─────────────────────────────────────────────

	[Fact]
	public void BuildAllButtonLedsReport_SetsAllButtonBytes()
	{
		var report = MikroMk3Protocol.BuildAllButtonLedsReport(75);

		report[0].Should().Be(MikroMk3Protocol.ButtonLedReportId);
		report.Should().HaveCount(MikroMk3Protocol.ButtonLedReportLength);
		for (var i = 0; i < MaschineDeviceConstants.MikroMk3ButtonCount; i++)
		{
			report[1 + i].Should().Be(75, because: $"button {i} should be 75");
		}
	}

	[Fact]
	public void BuildAllButtonLedsReport_ZeroBrightness_AllBytesZero()
	{
		var report = MikroMk3Protocol.BuildAllButtonLedsReport(0);

		for (var i = 0; i < MaschineDeviceConstants.MikroMk3ButtonCount; i++)
		{
			report[1 + i].Should().Be(0);
		}
	}
}
