using Maschine.Api.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace Maschine.Api.Test;

/// <summary>
/// Test double for <see cref="IHidDevice"/> that allows pre-queuing reports.
/// </summary>
internal sealed class FakeHidDevice : IHidDevice
{
	private readonly Queue<byte[]> _reads = new();
	private readonly object _sync = new();
	private TaskCompletionSource<byte[]>? _pendingRead;

	/// <summary>All data written to this device.</summary>
	public List<byte[]> WrittenReports { get; } = [];

	/// <summary>Whether the device has been disposed.</summary>
	public bool IsDisposed { get; private set; }

	/// <summary>If set, thrown by the next <see cref="ReadAsync"/> call (then cleared).</summary>
	public Exception? ExceptionToThrow { get; set; }

	/// <inheritdoc/>
	public int MaxOutputReportLength { get; } = 65;

	/// <inheritdoc/>
	public int MaxFeatureReportLength { get; } = 80;

	/// <summary>All feature reports sent via <see cref="WriteFeatureAsync"/>.</summary>
	public List<byte[]> WrittenFeatureReports { get; } = [];

	/// <summary>Queues a report to be returned by the next <see cref="ReadAsync"/> call.</summary>
	public void EnqueueReport(byte[] report)
	{
		TaskCompletionSource<byte[]>? pending;
		lock (_sync)
		{
			pending = _pendingRead;
			if (pending is null)
			{
				_reads.Enqueue(report);
				return;
			}

			_pendingRead = null;
		}

		pending.TrySetResult(report);
	}

	/// <inheritdoc/>
	public Task<byte[]> ReadAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (ExceptionToThrow is not null)
		{
			var ex = ExceptionToThrow;
			ExceptionToThrow = null;
			throw ex;
		}

		lock (_sync)
		{
			if (_reads.Count > 0)
			{
				return Task.FromResult(_reads.Dequeue());
			}

			_pendingRead = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
			var pending = _pendingRead;
			cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken));
			return pending.Task;
		}
	}

	/// <inheritdoc/>
	public Task WriteAsync(byte[] data, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		WrittenReports.Add(data);
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task WriteFeatureAsync(byte[] data, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		WrittenFeatureReports.Add(data);
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public void Dispose() => IsDisposed = true;
}

/// <summary>
/// Test double for <see cref="IHidDeviceFactory"/> that returns a <see cref="FakeHidDevice"/>.
/// </summary>
internal sealed class FakeHidDeviceFactory : IHidDeviceFactory
{
	private readonly FakeHidDevice? _device;

	public FakeHidDeviceFactory(FakeHidDevice? device = null) => _device = device;

	/// <inheritdoc/>
	public IHidDevice? TryOpen(int vendorId, int productId, int deviceIndex) => _device;
}

public sealed class MaschineClientTests
{
	private static MaschineClient CreateClient(FakeHidDevice device)
	{
		var options = new MaschineClientOptions();
		var factory = new FakeHidDeviceFactory(device);
		return new MaschineClient(options, factory, NullLogger<MaschineClient>.Instance);
	}

	[Fact]
	public async Task ConnectAsync_WhenDeviceFound_DoesNotThrow()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();
		await client.DisconnectAsync();
		client.Dispose();
	}

	[Fact]
	public async Task ConnectAsync_WhenNoDevice_ThrowsMaschineDeviceNotFoundException()
	{
		var options = new MaschineClientOptions();
		var factory = new FakeHidDeviceFactory(null);
		var client = new MaschineClient(options, factory, NullLogger<MaschineClient>.Instance);

		await ((Func<Task>)(() => client.ConnectAsync()))
			.Should().ThrowAsync<MaschineDeviceNotFoundException>();

		client.Dispose();
	}

	[Fact]
	public async Task Pads_BeforeConnect_ThrowsInvalidOperationException()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);

		((Func<object>)(() => client.Pads))
			.Should().Throw<InvalidOperationException>();

		await Task.CompletedTask;
		client.Dispose();
	}

	[Fact]
	public async Task Buttons_BeforeConnect_ThrowsInvalidOperationException()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);

		((Func<object>)(() => client.Buttons))
			.Should().Throw<InvalidOperationException>();

		await Task.CompletedTask;
		client.Dispose();
	}

	[Fact]
	public async Task Encoders_BeforeConnect_ThrowsInvalidOperationException()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);

		((Func<object>)(() => client.Encoders))
			.Should().Throw<InvalidOperationException>();

		await Task.CompletedTask;
		client.Dispose();
	}

	[Fact]
	public async Task Pads_AfterConnect_IsNotNull()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		client.Pads.Should().NotBeNull();

		await client.DisconnectAsync();
		client.Dispose();
	}

	[Fact]
	public async Task Buttons_AfterConnect_IsNotNull()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		client.Buttons.Should().NotBeNull();

		await client.DisconnectAsync();
		client.Dispose();
	}

	[Fact]
	public async Task Encoders_AfterConnect_IsNotNull()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		client.Encoders.Should().NotBeNull();

		await client.DisconnectAsync();
		client.Dispose();
	}

	[Fact]
	public async Task Dispose_WhenAlreadyDisposed_DoesNotThrow()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();
		client.Dispose();
		((Action)(() => client.Dispose())).Should().NotThrow();
	}

	[Fact]
	public Task ConnectAsync_AfterDispose_ThrowsObjectDisposedException()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		client.Dispose();

		return ((Func<Task>)(() => client.ConnectAsync()))
			.Should().ThrowAsync<ObjectDisposedException>();
	}

	[Fact]
	public async Task PadChanged_EventFired_WhenPadReportReceived()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		PadState? received = null;
		client.Pads.PadChanged += (_, state) => received = state;

		// Build a report where pad 0 has pressure 0x100
		var report = new byte[MikroMk3Protocol.PadPressureReportLength];
		report[0] = MikroMk3Protocol.PadPressureReportId;
		report[1] = 0x00;
		report[2] = 0x01; // pad 0 pressure = 0x100 (high nibble)
		device.EnqueueReport(report);

		// Allow the read loop to process the queued report
		await Task.Delay(100);

		received.Should().NotBeNull();
		received!.Value.Index.Should().Be(0);
		received.Value.IsPressed.Should().BeTrue();

		await client.DisconnectAsync();
		client.Dispose();
	}

	[Fact]
	public async Task ButtonChanged_EventFired_WhenButtonReportReceived()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		ButtonState? received = null;
		client.Buttons.ButtonChanged += (_, state) => received = state;

		var report = new byte[MikroMk3Protocol.ButtonReportLength];
		report[0] = MikroMk3Protocol.ButtonReportId;
		report[1] = 0x01; // button 0 pressed
		device.EnqueueReport(report);

		await Task.Delay(100);

		received.Should().NotBeNull();
		received!.Value.Index.Should().Be(0);
		received.Value.IsPressed.Should().BeTrue();

		await client.DisconnectAsync();
		client.Dispose();
	}

	[Fact]
	public async Task EncoderChanged_EventFired_WhenEncoderReportReceived()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		EncoderDelta? received = null;
		client.Encoders.EncoderChanged += (_, delta) => received = delta;

		var report = new byte[MikroMk3Protocol.EncoderReportLength];
		report[0] = MikroMk3Protocol.EncoderReportId;
		report[1] = 2; // encoder 0 CW by 2
		device.EnqueueReport(report);

		await Task.Delay(100);

		received.Should().NotBeNull();
		received!.Value.Index.Should().Be(0);
		received.Value.Delta.Should().Be(2);

		await client.DisconnectAsync();
		client.Dispose();
	}

	[Fact]
	public async Task SetColorAsync_WritesSinglePadReport()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		await client.Pads.SetColorAsync(0, PadColor.Red);

		device.WrittenReports.Should().HaveCount(1);
		device.WrittenReports[0][0].Should().Be(MikroMk3Protocol.PadLedReportId);

		await client.DisconnectAsync();
		client.Dispose();
	}

	[Fact]
	public async Task SetAllColorsAsync_WritesAllPadsReport()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		await client.Pads.SetAllColorsAsync(PadColor.Green);

		device.WrittenReports.Should().HaveCount(1);
		device.WrittenReports[0][0].Should().Be(MikroMk3Protocol.PadLedReportId);

		await client.DisconnectAsync();
		client.Dispose();
	}

	[Fact]
	public async Task GetPadStates_ReturnsAllPads()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		var states = client.Pads.GetStates();
		states.Should().HaveCount(MaschineDeviceConstants.MikroMk3PadCount);

		await client.DisconnectAsync();
		client.Dispose();
	}

	[Fact]
	public async Task GetButtonStates_ReturnsAllButtons()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		var states = client.Buttons.GetStates();
		states.Should().HaveCount(MaschineDeviceConstants.MikroMk3ButtonCount);

		await client.DisconnectAsync();
		client.Dispose();
	}

	[Fact]
	public async Task GetPadState_ValidIndex_ReturnsState()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		var state = client.Pads.GetState(5);
		state.Index.Should().Be(5);

		await client.DisconnectAsync();
		client.Dispose();
	}

	[Fact]
	public async Task GetPadState_InvalidIndex_Throws()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		((Action)(() => client.Pads.GetState(99)))
			.Should().Throw<ArgumentOutOfRangeException>();

		await client.DisconnectAsync();
		client.Dispose();
	}

	[Fact]
	public async Task GetButtonState_ValidIndex_ReturnsState()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		var state = client.Buttons.GetState(10);
		state.Index.Should().Be(10);

		await client.DisconnectAsync();
		client.Dispose();
	}

	[Fact]
	public async Task GetButtonState_InvalidIndex_Throws()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		((Action)(() => client.Buttons.GetState(99)))
			.Should().Throw<ArgumentOutOfRangeException>();

		await client.DisconnectAsync();
		client.Dispose();
	}

	// ── Public constructor coverage ──────────────────────────────────────────

	[Fact]
	public void DefaultConstructor_CanBeConstructedAndDisposed()
	{
		var client = new MaschineClient();
		client.Dispose();
	}

	[Fact]
	public void OptionsConstructor_CanBeConstructedAndDisposed()
	{
		var options = new MaschineClientOptions();
		var client = new MaschineClient(options);
		client.Dispose();
	}

	[Fact]
	public void OptionsLoggerConstructor_CanBeConstructedAndDisposed()
	{
		var options = new MaschineClientOptions();
		var client = new MaschineClient(options, NullLogger<MaschineClient>.Instance);
		client.Dispose();
	}

	// ── Read-loop edge cases ─────────────────────────────────────────────────

	[Fact]
	public async Task ReadLoop_EmptyReport_IsIgnoredAndNextReportIsProcessed()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		PadState? received = null;
		client.Pads.PadChanged += (_, state) => received = state;

		// Empty report must be ignored
		device.EnqueueReport([]);

		// Valid pad report follows
		var report = new byte[MikroMk3Protocol.PadPressureReportLength];
		report[0] = MikroMk3Protocol.PadPressureReportId;
		report[1] = 0x00;
		report[2] = 0x01;
		device.EnqueueReport(report);

		await Task.Delay(100);

		received.Should().NotBeNull();
		received!.Value.Index.Should().Be(0);

		await client.DisconnectAsync();
		client.Dispose();
	}

	[Fact]
	public async Task ReadLoop_IOException_ExitsCleanly()
	{
		var device = new FakeHidDevice();
		device.ExceptionToThrow = new System.IO.IOException("Simulated IO error");
		var client = CreateClient(device);
		await client.ConnectAsync();

		await Task.Delay(100);

		await client.DisconnectAsync();
		client.Dispose();
	}

	[Fact]
	public async Task ReadLoop_UnknownReportId_DoesNotThrow()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		device.EnqueueReport([0xFF, 0x00, 0x00]);

	// ── Button LED writes ─────────────────────────────────────────────────────
		await Task.Delay(100);

		await client.DisconnectAsync();
		client.Dispose();
	}

	// ── Button LED writes ─────────────────────────────────────────────────────

	[Fact]
	public async Task SetLedAsync_WritesSingleButtonLedReport()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		await client.Buttons.SetLedAsync(0, 100);

		device.WrittenFeatureReports.Should().BeEmpty();
		device.WrittenReports.Should().HaveCount(1);
		device.WrittenReports[0][0].Should().Be(MikroMk3Protocol.ButtonLedReportId);
		device.WrittenReports[0][1].Should().Be(100);

		await client.DisconnectAsync();
		client.Dispose();
	}

	[Fact]
	public async Task SetAllLedsAsync_WritesAllButtonLedsReport()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		await client.Buttons.SetAllLedsAsync(50);

		device.WrittenFeatureReports.Should().BeEmpty();
		device.WrittenReports.Should().HaveCount(1);
		device.WrittenReports[0][0].Should().Be(MikroMk3Protocol.ButtonLedReportId);
		for (var i = 0; i < MaschineDeviceConstants.MikroMk3ButtonCount; i++)
		{
			device.WrittenReports[0][1 + i].Should().Be(50);
		}

		await client.DisconnectAsync();
		client.Dispose();
	}

	[Fact]
	public async Task DisconnectAsync_CalledTwice_DoesNotThrow()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();
		await client.DisconnectAsync();
		await ((Func<Task>)(() => client.DisconnectAsync())).Should().NotThrowAsync();
		client.Dispose();
	}

	[Fact]
	public async Task PadReport_WithNoSubscriber_DoesNotThrow()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		var report = new byte[MikroMk3Protocol.PadPressureReportLength];
		report[0] = MikroMk3Protocol.PadPressureReportId;
		report[1] = 0x00;
		report[2] = 0x01;
		device.EnqueueReport(report);

		await Task.Delay(100);

		await client.DisconnectAsync();
		client.Dispose();
	}

	[Fact]
	public async Task ButtonReport_WithNoSubscriber_DoesNotThrow()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		var report = new byte[MikroMk3Protocol.ButtonReportLength];
		report[0] = MikroMk3Protocol.ButtonReportId;
		report[1] = 0x01;
		device.EnqueueReport(report);

		await Task.Delay(100);

		await client.DisconnectAsync();
		client.Dispose();
	}

	[Fact]
	public async Task EncoderReport_WithNoSubscriber_DoesNotThrow()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		var report = new byte[MikroMk3Protocol.EncoderReportLength];
		report[0] = MikroMk3Protocol.EncoderReportId;
		report[1] = 2;
		device.EnqueueReport(report);

		await Task.Delay(100);

		await client.DisconnectAsync();
		client.Dispose();
	}

	[Fact]
	public async Task SetDotMatrixTestPatternAsync_WritesTwoDisplayPackets()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		await client.SetDotMatrixTestPatternAsync();

		device.WrittenReports.Should().Contain(r => r.Length == 265 && r[0] == 0xE0);
		device.WrittenReports.Should().HaveCountGreaterThanOrEqualTo(2);
		device.WrittenReports[^2][0].Should().Be(0xE0);
		device.WrittenReports[^1][0].Should().Be(0xE0);

		await client.DisconnectAsync();
		client.Dispose();
	}

	[Fact]
	public async Task ClearDotMatrixAsync_WritesZeroedPixelPayloads()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		await client.ClearDotMatrixAsync();

		var top = device.WrittenReports[^2];
		var bottom = device.WrittenReports[^1];
		top.Length.Should().Be(265);
		bottom.Length.Should().Be(265);
		top[0].Should().Be(0xE0);
		bottom[0].Should().Be(0xE0);
		for (var i = 9; i < top.Length; i++)
		{
			top[i].Should().Be(0);
		}

		for (var i = 9; i < bottom.Length; i++)
		{
			bottom[i].Should().Be(0);
		}

		await client.DisconnectAsync();
		client.Dispose();
	}

	[Fact]
	public async Task SetDotMatrixZebraLinesAsync_WritesAlternatingStripePattern()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		await client.SetDotMatrixZebraLinesAsync();

		var top = device.WrittenReports[^2];
		var bottom = device.WrittenReports[^1];
		top.Length.Should().Be(265);
		bottom.Length.Should().Be(265);
		top[9 + 0].Should().Be(0x03);
		top[9 + 1].Should().Be(0x06);
		top[9 + 2].Should().Be(0x0C);
		top[9 + 3].Should().Be(0x18);
		top[9 + 4].Should().Be(0x30);
		top[9 + 5].Should().Be(0x60);
		top[9 + 6].Should().Be(0xC0);
		top[9 + 7].Should().Be(0x81);
		bottom[9 + 0].Should().Be(top[9 + 0]);
		bottom[9 + 7].Should().Be(top[9 + 7]);

		await client.DisconnectAsync();
		client.Dispose();
	}

	[Fact]
	public async Task SetDotMatrixZebraLinesAsync_WithPhase_ShiftsPattern()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		await client.SetDotMatrixZebraLinesAsync(3);

		var top = device.WrittenReports[^2];
		top[9 + 0].Should().Be(0x18);
		top[9 + 1].Should().Be(0x30);
		top[9 + 2].Should().Be(0x60);


		await client.DisconnectAsync();
		client.Dispose();
	}

	[Fact]
	public async Task DisconnectAsync_PerformsDotMatrixClearFeatureFallback()
	{
		var device = new FakeHidDevice();
		var client = CreateClient(device);
		await client.ConnectAsync();

		await client.SetDotMatrixZebraLinesAsync();
		await client.DisconnectAsync();

		device.WrittenFeatureReports.Should().Contain(r => r.Length == 265 && r[0] == 0xE0);
		var topFeature = device.WrittenFeatureReports[^2];
		var bottomFeature = device.WrittenFeatureReports[^1];
		for (var i = 9; i < topFeature.Length; i++)
		{
			topFeature[i].Should().Be(0);
		}

		for (var i = 9; i < bottomFeature.Length; i++)
		{
			bottomFeature[i].Should().Be(0);
		}

		client.Dispose();
	}
}
