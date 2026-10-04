using Maschine.Api.Interfaces;
using Maschine.Api.Models;
using Maschine.Api.Widgets;
using Microsoft.Extensions.Logging.Abstractions;

namespace Maschine.Api.Test;

/// <summary>
/// Covers the convenience overloads that replaced optional parameters on the public API.
/// </summary>
public sealed class OverloadTests
{
	private static readonly DisplayZone s_zone = new(0, 0, 32, 8);

	private static async Task<(MaschineClient Client, FakeHidDevice Device)> ConnectAsync()
	{
		var device = new FakeHidDevice();
		var options = new MaschineClientOptions { AllowExternalLedOverrides = true };
		var client = new MaschineClient(options, new FakeHidDeviceFactory(device), NullLogger<MaschineClient>.Instance);
		await client.ConnectAsync();
		return (client, device);
	}

	[Fact]
	public async Task MaschineClient_DotMatrixOverloads_WriteToDevice()
	{
		var (client, device) = await ConnectAsync();
		using (client)
		{
			var bitmap = new byte[512];
			var lines = new[] { "A", "B" };
			var dashboard = new DotMatrixDashboard();
			dashboard.AddWidget(new TextWidget("t", s_zone, ["x"]));
			using var cts = new CancellationTokenSource();

			await client.SetDotMatrixTestPatternAsync();
			await client.ClearDotMatrixAsync();
			await client.SetDotMatrixZebraLinesAsync();
			await client.SetDotMatrixZebraLinesAsync(1);
			await client.SetDotMatrixZebraLinesAsync(cts.Token);
			await client.SetDotMatrixBitmapAsync(bitmap);
			await client.SetDotMatrixBitmapAsync(bitmap, cts.Token);
			await client.SetDotMatrixBitmapAsync(bitmap, 1, 1);
			await client.SetDotMatrixTextAsync(lines, DisplayLineMode.TwoRows);
			await client.SetDotMatrixTextAsync(lines, DisplayLineMode.TwoRows, cts.Token);
			await client.SetDotMatrixTextAsync(lines, DisplayLineMode.TwoRows, 1, 1);
			await client.SetDotMatrixDashboardAsync(dashboard);
			await client.SetDotMatrixWidgetsAsync([new TextWidget("w", s_zone, ["y"])]);

			device.WrittenReports.Should().NotBeEmpty();
			await client.DisconnectAsync();
		}
	}

	[Fact]
	public async Task MaschineClient_DashboardLoopOverloads_StopWhenCancelled()
	{
		var (client, _) = await ConnectAsync();
		using (client)
		{
			var dashboard = new DotMatrixDashboard();
			using var cts = new CancellationTokenSource();
			cts.Cancel();

			await client.RunDotMatrixDashboardLoopAsync(dashboard, cts.Token);
			await client.RunDotMatrixDashboardLoopAsync(dashboard, 10, cts.Token);
			await client.DisconnectAsync();
		}
	}

	[Fact]
	public async Task MaschineClient_ConnectAsyncWithToken_Connects()
	{
		var device = new FakeHidDevice();
		using var client = new MaschineClient(new MaschineClientOptions(), new FakeHidDeviceFactory(device), NullLogger<MaschineClient>.Instance);

		await client.ConnectAsync(CancellationToken.None);
		await client.DisconnectAsync();
	}

	[Fact]
	public async Task PadAndButtonOverloads_WriteToDevice()
	{
		var (client, device) = await ConnectAsync();
		using (client)
		{
			IPads pads = client.Pads;
			IButtons buttons = client.Buttons;

			await pads.SetColorAsync(0, PadColor.Off);
			await pads.SetAllColorsAsync(PadColor.Off);
			await buttons.SetLedAsync(0, 10);
			await buttons.SetAllLedsAsync(10);
			await buttons.SetOnOffAsync(0, true);
			await buttons.SetAllOnOffAsync(false);

			device.WrittenReports.Should().NotBeEmpty();
			await client.DisconnectAsync();
		}
	}

	[Fact]
	public async Task TouchStripOverloads_WriteToDevice()
	{
		var (client, device) = await ConnectAsync();
		using (client)
		{
			ITouchStrip strip = client.TouchStrip;
			var count = MaschineDeviceConstants.MikroMk3TouchStripLedCount;

			await strip.SetLedAsync(0, 10);
			await strip.SetAllLedsAsync(10);
			await strip.SetLedsAsync(new byte[count]);
			await strip.SetLedsAsync(new PadColor[count]);

			device.WrittenReports.Should().NotBeEmpty();
			await client.DisconnectAsync();
		}
	}

	[Fact]
	public void TextWidget_ShortConstructors_ApplyDefaults()
	{
		var plain = new TextWidget("a", s_zone, ["x"]);
		plain.OverflowMode.Should().Be(TextOverflowMode.None);
		plain.Invert.Should().BeFalse();

		var scrolling = new TextWidget("b", s_zone, ["x"], TextOverflowMode.Scroll);
		scrolling.OverflowMode.Should().Be(TextOverflowMode.Scroll);
		scrolling.Invert.Should().BeFalse();
	}

	[Fact]
	public void VuWidget_ShortConstructors_ApplyDefaults()
	{
		var byStyle = new VuWidget("a", s_zone, VuWidgetStyle.Bar);
		byStyle.NeedleDetailMode.Should().Be(VuNeedleDetailMode.Auto);
		byStyle.Level.Should().Be(0f);
		byStyle.PeakLevel.Should().BeNull();
		byStyle.Invert.Should().BeFalse();

		var byMode = new VuWidget("b", s_zone, VuWidgetStyle.Needle, VuNeedleDetailMode.Simple);
		byMode.NeedleDetailMode.Should().Be(VuNeedleDetailMode.Simple);
		byMode.Level.Should().Be(0f);

		var byLevel = new VuWidget("c", s_zone, VuWidgetStyle.Needle, VuNeedleDetailMode.Detailed, 0.5f);
		byLevel.Level.Should().Be(0.5f);
		byLevel.PeakLevel.Should().BeNull();

		var levelAndPeak = new VuWidget("d", s_zone, VuWidgetStyle.Bar, 0.4f, 0.6f);
		levelAndPeak.NeedleDetailMode.Should().Be(VuNeedleDetailMode.Auto);
		levelAndPeak.PeakLevel.Should().Be(0.6f);
		levelAndPeak.Invert.Should().BeFalse();

		var inverted = new VuWidget("e", s_zone, VuWidgetStyle.Bar, 0.4f, 0.6f, true);
		inverted.Invert.Should().BeTrue();

		var modeLevelAndPeak = new VuWidget("f", s_zone, VuWidgetStyle.Needle, VuNeedleDetailMode.Simple, 0.3f, 0.7f);
		modeLevelAndPeak.NeedleDetailMode.Should().Be(VuNeedleDetailMode.Simple);
		modeLevelAndPeak.PeakLevel.Should().Be(0.7f);
		modeLevelAndPeak.Invert.Should().BeFalse();
	}

	[Fact]
	public void SpectrumAndEqWidgets_ShortConstructors_AreNotInverted()
	{
		new SpectrumWidget("s", s_zone, [0.1f, 0.2f]).Invert.Should().BeFalse();
		new EqWidget("e", s_zone, [0.1f, 0.2f]).Invert.Should().BeFalse();
	}

	[Fact]
	public void WidgetBase_ShortConstructor_IsNotInverted()
	{
		new TestWidget("w", s_zone).Invert.Should().BeFalse();
	}

	[Fact]
	public void AdvanceFrame_WithoutArgument_MatchesForwardDirection()
	{
		var dashboard = new DotMatrixDashboard();
		dashboard.AdvanceFrame().Should().Be(dashboard.AdvanceFrame(1));
	}

	private sealed class TestWidget(string id, DisplayZone zone) : DotMatrixWidgetBase(id, zone);
}
