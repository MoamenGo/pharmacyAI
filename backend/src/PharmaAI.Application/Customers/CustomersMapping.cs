using PharmaAI.Application.Common;
using PharmaAI.Domain.Customers;

namespace PharmaAI.Application.Customers;

internal static class CustomersMapping
{
    public static CustomerDto ToDto(this Customer customer) =>
        new(customer.Id, customer.Name, customer.Phone, Piasters.FromPounds(customer.Balance));
}
