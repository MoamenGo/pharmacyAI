using PharmaAI.Application.Sales.Commands.CreateSale;
using PharmaAI.Domain.Sales;

namespace PharmaAI.Application.Tests.Sales;

public class CreateSaleValidatorTests
{
    private static readonly CreateSaleCommandValidator Validator = new();

    private static CreateSaleCommand Sale(int quantity = 1, SaleUnit unit = SaleUnit.Pack, long paid = 4500) =>
        new([new CreateSaleItem(Guid.NewGuid(), unit, quantity)], paid);

    [Fact]
    public void Accepts_a_valid_sale() => Assert.True(Validator.Validate(Sale()).IsValid);

    [Fact]
    public void Rejects_an_empty_sale_with_a_code()
    {
        var result = Validator.Validate(new CreateSaleCommand([], 0));

        Assert.Equal("sale.empty", Assert.Single(result.Errors).ErrorCode);
    }

    [Theory]
    [InlineData(0, SaleUnit.Pack, 4500)]
    [InlineData(SaleLine.MaxQuantity + 1, SaleUnit.Pack, 4500)]
    [InlineData(1, (SaleUnit)9, 4500)]
    [InlineData(1, SaleUnit.Pack, -1)]
    [InlineData(1, SaleUnit.Pack, CreateSaleCommandValidator.MaxPaidPiasters + 1)]
    public void Rejects_bad_lines_or_payment(int quantity, SaleUnit unit, long paid) =>
        Assert.False(Validator.Validate(Sale(quantity, unit, paid)).IsValid);
}
