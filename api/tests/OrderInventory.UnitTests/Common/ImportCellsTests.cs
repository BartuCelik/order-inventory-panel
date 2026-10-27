using OrderInventory.Application.Common;
using OrderInventory.Domain.Products;

namespace OrderInventory.UnitTests.Common;

/// <summary>Imported cells accept what the export writes ("Raw material", "m²", "Yes") and the enum names.</summary>
public sealed class ImportCellsTests
{
    [Theory]
    [InlineData("Raw material", ProductType.RawMaterial)]
    [InlineData("RawMaterial", ProductType.RawMaterial)]
    [InlineData(" semi-finished ", ProductType.SemiFinished)]
    [InlineData("Semi finished", ProductType.SemiFinished)]
    [InlineData("FINISHED", ProductType.Finished)]
    public void ProductType_AcceptsLabelsAndEnumNames(string text, ProductType expected) =>
        Assert.Equal(expected, ImportCells.ProductType(text));

    [Theory]
    [InlineData("pcs", UnitOfMeasure.Piece)]
    [InlineData("Piece", UnitOfMeasure.Piece)]
    [InlineData("m", UnitOfMeasure.Meter)]
    [InlineData("metre", UnitOfMeasure.Meter)]
    [InlineData("m²", UnitOfMeasure.SquareMeter)]
    [InlineData("m2", UnitOfMeasure.SquareMeter)]
    [InlineData("Square meter", UnitOfMeasure.SquareMeter)]
    [InlineData("kg", UnitOfMeasure.Kilogram)]
    [InlineData("L", UnitOfMeasure.Liter)]
    [InlineData("litre", UnitOfMeasure.Liter)]
    public void Unit_AcceptsLabelsAndEnumNames(string text, UnitOfMeasure expected) =>
        Assert.Equal(expected, ImportCells.Unit(text));

    [Theory]
    [InlineData("box")]
    [InlineData("")]
    [InlineData("1")]
    public void UnknownTypeOrUnit_IsNull(string text)
    {
        Assert.Null(ImportCells.ProductType(text));
        Assert.Null(ImportCells.Unit(text));
    }

    [Theory]
    [InlineData("12.5", 12.5)]
    [InlineData(" 0 ", 0)]
    [InlineData("-3", -3)]
    public void Number_ReadsInvariantNumbers(string text, decimal expected) =>
        Assert.Equal(expected, ImportCells.Number(text));

    [Theory]
    [InlineData("12,5")] // a comma is ambiguous between cultures: the reader writes numbers invariantly
    [InlineData("ten")]
    [InlineData("1e400")]
    public void Number_RejectsTextThatIsNotAPlainNumber(string text) =>
        Assert.Null(ImportCells.Number(text));

    [Theory]
    [InlineData("Yes", true)]
    [InlineData("no", false)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    public void YesNo_AcceptsTheExportAndExcelBooleans(string text, bool expected) =>
        Assert.Equal(expected, ImportCells.YesNo(text));

    [Fact]
    public void YesNo_UnknownText_IsNull() => Assert.Null(ImportCells.YesNo("maybe"));
}
