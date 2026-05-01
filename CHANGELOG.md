# Changelog

## [Unreleased]

### Added
- Initial release supporting Maschine Mikro MK3 (VID 0x17CC / PID 0x1700)
- RGB pad LED control (`SetColorAsync`, `SetAllColorsAsync`)
- Pad pressure events (12-bit, `PadChanged`)
- Button state events (`ButtonChanged`)
- Encoder delta events (`EncoderChanged`)
- `IHidDeviceFactory`/`IHidDevice` abstractions for full testability without hardware
- Pure-static `MikroMk3Protocol` byte parser (100% unit-test coverage)
