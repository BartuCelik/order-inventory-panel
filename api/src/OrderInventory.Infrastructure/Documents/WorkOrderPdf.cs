using System.Globalization;
using OrderInventory.Application.Production;
using OrderInventory.Domain.Production;
using OrderInventory.Domain.Products;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OrderInventory.Infrastructure.Documents;

/// <summary>
/// The printed work order for the shop floor: what to make, which materials to pick, and space to sign.
/// English only for now; translated PDFs are on the Later list.
/// </summary>
public sealed class WorkOrderPdf(TimeProvider timeProvider)
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly Color Muted = Colors.Grey.Darken1;
    private static readonly Color Line = Colors.Grey.Lighten2;
    private static readonly string[] ComponentColumns = ["Code", "Material", "Required", "Consumed", "Picked"];
    private static readonly string[] SignatureLabels = ["Prepared by", "Completed by"];

    public byte[] Render(WorkOrderDto workOrder)
    {
        ArgumentNullException.ThrowIfNull(workOrder);
        var printedAt = timeProvider.GetUtcNow().UtcDateTime;

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(text => text.FontSize(10));

            page.Header().Element(header => Header(header, workOrder));
            page.Content().PaddingVertical(16).Column(column =>
            {
                column.Spacing(16);
                column.Item().Element(c => Summary(c, workOrder));
                column.Item().Element(c => Components(c, workOrder));
                column.Item().PaddingTop(24).Element(Signatures);
            });
            page.Footer().Row(row =>
            {
                row.RelativeItem().Text($"Printed {printedAt.ToString("yyyy-MM-dd HH:mm", Culture)} UTC").FontColor(Muted).FontSize(8);
                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.DefaultTextStyle(s => s.FontColor(Muted).FontSize(8));
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        })).GeneratePdf();
    }

    private static void Header(IContainer container, WorkOrderDto workOrder) =>
        container.BorderBottom(1).BorderColor(Line).PaddingBottom(8).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text($"Work order {workOrder.Number}").FontSize(20).Bold();
                column.Item().Text($"{workOrder.ProductCode} · {workOrder.ProductName}").FontColor(Muted);
            });
            row.AutoItem().AlignRight().AlignMiddle().Text(StatusLabel(workOrder.Status)).FontSize(12).SemiBold();
        });

    private static void Summary(IContainer container, WorkOrderDto workOrder) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(110);
                columns.RelativeColumn();
            });

            void Field(string label, string value)
            {
                table.Cell().PaddingVertical(2).Text(label).FontColor(Muted);
                table.Cell().PaddingVertical(2).Text(value);
            }

            Field("Quantity", $"{Quantity(workOrder.Quantity)} {UnitLabel(workOrder.Unit)}");
            Field("Planned for", workOrder.PlannedDate.ToString("yyyy-MM-dd", Culture));
            Field("Sales order", workOrder.SalesOrderNumber is null
                ? "For stock"
                : $"{workOrder.SalesOrderNumber} · {workOrder.CustomerName}");
            if (workOrder.CompletedAt is { } completedAt)
            {
                Field("Completed", $"{completedAt.ToString("yyyy-MM-dd HH:mm", Culture)} UTC");
            }
        });

    private static void Components(IContainer container, WorkOrderDto workOrder) =>
        container.Column(column =>
        {
            column.Item().PaddingBottom(6).Text("Materials to pick").FontSize(13).SemiBold();
            if (workOrder.Components.Count == 0)
            {
                column.Item().Text("This product has no bill of materials.").FontColor(Muted);
                return;
            }

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(4);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.ConstantColumn(44);
                });

                table.Header(header =>
                {
                    foreach (var title in ComponentColumns)
                    {
                        var cell = header.Cell().BorderBottom(1).BorderColor(Line).PaddingVertical(4);
                        (title is "Required" or "Consumed" ? cell.AlignRight() : title == "Picked" ? cell.AlignCenter() : cell)
                            .Text(title).FontColor(Muted).SemiBold();
                    }
                });

                foreach (var c in workOrder.Components)
                {
                    IContainer Cell() => table.Cell().BorderBottom(0.5f).BorderColor(Line).PaddingVertical(5);
                    var unit = UnitLabel(c.Unit);

                    Cell().Text(c.ProductCode);
                    Cell().Text(text =>
                    {
                        text.Span(c.ProductName);
                        // Rule 9 planning signal, only meaningful while the work order is open.
                        if (c.Shortage > 0)
                        {
                            text.Span($"  short {Quantity(c.Shortage)} {unit}").FontColor(Colors.Red.Darken2).SemiBold();
                        }
                    });
                    Cell().AlignRight().Text($"{Quantity(c.RequiredQty)} {unit}");
                    Cell().AlignRight().Text($"{Quantity(c.ConsumedQty)} {unit}");
                    Cell().AlignCenter().Width(12).Height(12).Border(1).BorderColor(Muted);
                }
            });
        });

    private static void Signatures(IContainer container) =>
        container.Row(row =>
        {
            row.Spacing(32);
            foreach (var label in SignatureLabels)
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Height(28).BorderBottom(1).BorderColor(Muted);
                    column.Item().PaddingTop(4).Text($"{label} · date").FontColor(Muted).FontSize(9);
                });
            }
        });

    private static string Quantity(decimal value) => value.ToString("0.####", Culture);

    private static string StatusLabel(WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.InProgress => "In progress",
        _ => status.ToString(),
    };

    private static string UnitLabel(UnitOfMeasure unit) => unit switch
    {
        UnitOfMeasure.Piece => "pcs",
        UnitOfMeasure.Meter => "m",
        UnitOfMeasure.SquareMeter => "m²",
        UnitOfMeasure.Kilogram => "kg",
        UnitOfMeasure.Liter => "l",
        _ => unit.ToString(),
    };
}
