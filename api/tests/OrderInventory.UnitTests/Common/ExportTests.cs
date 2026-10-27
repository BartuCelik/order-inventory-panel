using System.ComponentModel.DataAnnotations;
using OrderInventory.Application.Common;

namespace OrderInventory.UnitTests.Common;

public sealed class ExportTests
{
    [Fact]
    public void Rows_EverythingFitsOnTheExportPage_ReturnsTheRows()
    {
        var rows = Export.Rows(new PagedResult<int>([1, 2, 3], 1, Export.MaxRows, 3));

        Assert.Equal([1, 2, 3], rows);
    }

    [Fact]
    public void Rows_MoreRowsMatchThanTheLimit_ThrowsInsteadOfCuttingTheFile()
    {
        var exception = Assert.Throws<ValidationException>(
            () => Export.Rows(new PagedResult<int>([1], 1, Export.MaxRows, Export.MaxRows + 1)));

        Assert.Contains("narrow", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
