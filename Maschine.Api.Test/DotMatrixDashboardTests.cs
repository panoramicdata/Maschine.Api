using Maschine.Api.Widgets;

namespace Maschine.Api.Test;

public sealed class DotMatrixDashboardTests
{
	[Fact]
	public void AddWidget_OverlappingZone_Throws()
	{
		var dashboard = new DotMatrixDashboard();
		dashboard.AddWidget(new TextWidget("a", new DisplayZone(0, 0, 16, 8), ["A"]));

		var act = () => dashboard.AddWidget(new EqWidget("b", new DisplayZone(8, 0, 16, 8), [0.5f]));

		act.Should().Throw<DashboardLayoutException>()
			.WithMessage("*overlaps existing widget*");
	}

	[Fact]
	public void BuildBitmap_TextWidget_SetsPixels()
	{
		var dashboard = new DotMatrixDashboard();
		dashboard.AddWidget(new TextWidget("txt", new DisplayZone(0, 0, 16, 8), ["A"]));

		var bitmap = dashboard.BuildBitmap();

		bitmap.Should().HaveCount(512);
		bitmap.Should().Contain(b => b != 0);
	}

	[Fact]
	public void BuildBitmap_InvertWidget_FillsBackground()
	{
		var dashboard = new DotMatrixDashboard();
		dashboard.AddWidget(new EqWidget("eq", new DisplayZone(0, 0, 8, 4), [0f], invert: true));

		var bitmap = dashboard.BuildBitmap();

		// Top 4 rows x first 8 columns are in byte[0], byte[16], byte[32], byte[48] bit7 only range.
		bitmap[0].Should().NotBe(0);
	}

	[Fact]
	public void BuildBitmap_TextEllipsis_ProducesDifferentOutputFromNone()
	{
		var none = new DotMatrixDashboard();
		none.AddWidget(new TextWidget("txt", new DisplayZone(0, 0, 16, 8), ["ABCDEFG"], TextOverflowMode.None));

		var ellipsis = new DotMatrixDashboard();
		ellipsis.AddWidget(new TextWidget("txt", new DisplayZone(0, 0, 16, 8), ["ABCDEFG"], TextOverflowMode.Ellipsis));

		ellipsis.BuildBitmap().Should().NotEqual(none.BuildBitmap());
	}

	[Fact]
	public void BuildBitmap_TextScroll_OffsetChangesOutput()
	{
		var dashboard = new DotMatrixDashboard();
		var widget = new TextWidget("txt", new DisplayZone(0, 0, 32, 8), ["ABCDEFG"], TextOverflowMode.Scroll)
		{
			OverflowOffset = 0,
		};
		dashboard.AddWidget(widget);

		var first = dashboard.BuildBitmap();
		widget.OverflowOffset = 2;
		var second = dashboard.BuildBitmap();

		second.Should().NotEqual(first);
	}

	[Fact]
	public void BuildBitmap_TextRotate_OffsetChangesOutput()
	{
		var dashboard = new DotMatrixDashboard();
		var widget = new TextWidget("txt", new DisplayZone(0, 0, 32, 8), ["ABCDEFG"], TextOverflowMode.Rotate)
		{
			OverflowOffset = 0,
		};
		dashboard.AddWidget(widget);

		var first = dashboard.BuildBitmap();
		widget.OverflowOffset = 3;
		var second = dashboard.BuildBitmap();

		second.Should().NotEqual(first);
	}

	[Fact]
	public void BuildBitmap_VuNeedleSimpleAndDetailed_BothRender()
	{
		var simple = new DotMatrixDashboard();
		simple.AddWidget(new VuWidget("vu", new DisplayZone(0, 0, 20, 10), VuWidgetStyle.Needle, VuNeedleDetailMode.Simple, 0.5f));

		var detailed = new DotMatrixDashboard();
		detailed.AddWidget(new VuWidget("vu", new DisplayZone(0, 0, 20, 10), VuWidgetStyle.Needle, VuNeedleDetailMode.Detailed, 0.5f));

		var simpleBitmap = simple.BuildBitmap();
		var detailedBitmap = detailed.BuildBitmap();

		simpleBitmap.Should().Contain(b => b != 0);
		detailedBitmap.Should().Contain(b => b != 0);
		detailedBitmap.Should().NotEqual(simpleBitmap);
	}

	[Fact]
	public void TextWidget_DefaultsToProportional8()
	{
		var widget = new TextWidget("txt", new DisplayZone(0, 0, 32, 8), ["ABC"]);

		widget.FontKind.Should().Be(TextFontKind.Proportional8);
	}

	[Fact]
	public void BuildBitmap_Proportional8BoldDiffersFromProportional8()
	{
		var bold = new DotMatrixDashboard();
		bold.AddWidget(new TextWidget("txt", new DisplayZone(0, 0, 32, 8), ["ABC"], TextOverflowMode.None)
		{
			FontKind = TextFontKind.Proportional8Bold,
		});

		var regular = new DotMatrixDashboard();
		regular.AddWidget(new TextWidget("txt", new DisplayZone(0, 0, 32, 8), ["ABC"], TextOverflowMode.None)
		{
			FontKind = TextFontKind.Proportional8,
		});

		bold.BuildBitmap().Should().NotEqual(regular.BuildBitmap());
	}

	[Fact]
	public void BuildBitmap_Proportional12BoldDiffersFromProportional12()
	{
		var bold = new DotMatrixDashboard();
		bold.AddWidget(new TextWidget("txt", new DisplayZone(0, 0, 128, 12), ["MIXED 123"], TextOverflowMode.None)
		{
			FontKind = TextFontKind.Proportional12Bold,
		});

		var regular = new DotMatrixDashboard();
		regular.AddWidget(new TextWidget("txt", new DisplayZone(0, 0, 128, 12), ["MIXED 123"], TextOverflowMode.None)
		{
			FontKind = TextFontKind.Proportional12,
		});

		bold.BuildBitmap().Should().NotEqual(regular.BuildBitmap());
	}

	[Fact]
	public void BuildBitmap_AutoFont_Selects12WhenZoneAllows()
	{
		var auto = new DotMatrixDashboard();
		auto.AddWidget(new TextWidget("txt", new DisplayZone(0, 0, 128, 12), ["AUTO"], TextOverflowMode.None)
		{
			FontKind = TextFontKind.Auto,
		});

		var explicit12 = new DotMatrixDashboard();
		explicit12.AddWidget(new TextWidget("txt", new DisplayZone(0, 0, 128, 12), ["AUTO"], TextOverflowMode.None)
		{
			FontKind = TextFontKind.Proportional12,
		});

		auto.BuildBitmap().Should().Equal(explicit12.BuildBitmap());
	}
}
