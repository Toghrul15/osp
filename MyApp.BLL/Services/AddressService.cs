using Microsoft.EntityFrameworkCore;
using MyApp.BLL.DTOs.Account;
using MyApp.BLL.Exceptions;
using MyApp.Core.Entities;
using MyApp.DAL;
using MyApp.DAL.Repositories;

namespace MyApp.BLL.Services;

public interface IAddressService
{
    Task<IReadOnlyList<AddressDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<AddressDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<AddressDto> CreateAsync(AddressRequest request, CancellationToken cancellationToken = default);
    Task<AddressDto> UpdateAsync(int id, AddressRequest request, CancellationToken cancellationToken = default);
    Task<AddressDto> SetDefaultAsync(int id, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<ShippingAddress> GetShippingAddressAsync(int id, CancellationToken cancellationToken = default);
}

public class AddressService(
    IRepository<Address> addresses,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser) : IAddressService
{
    public async Task<IReadOnlyList<AddressDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;
        return await addresses.Query()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => ToDto(a))
            .ToListAsync(cancellationToken);
    }

    public async Task<AddressDto> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        ToDto(await FindOwnAsync(id, tracking: false, cancellationToken));

    public async Task<AddressDto> CreateAsync(AddressRequest request, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;
        var count = await addresses.Query().CountAsync(a => a.UserId == userId, cancellationToken);
        if (count >= AddressRequest.MaxAddressesPerUser)
            throw new BadRequestException($"You can save at most {AddressRequest.MaxAddressesPerUser} addresses.");

        var address = new Address { UserId = userId };
        Apply(address, request);
        var makeDefault = count == 0 || request.IsDefault;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        if (makeDefault)
            await ClearDefaultAsync(userId, cancellationToken);

        address.IsDefault = makeDefault;
        await addresses.AddAsync(address, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToDto(address);
    }

    public async Task<AddressDto> UpdateAsync(int id, AddressRequest request, CancellationToken cancellationToken = default)
    {
        var address = await FindOwnAsync(id, tracking: true, cancellationToken);
        Apply(address, request);

        if (request.IsDefault && !address.IsDefault)
            return await SwitchDefaultAsync(address, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(address);
    }

    public async Task<AddressDto> SetDefaultAsync(int id, CancellationToken cancellationToken = default)
    {
        var address = await FindOwnAsync(id, tracking: true, cancellationToken);
        return address.IsDefault ? ToDto(address) : await SwitchDefaultAsync(address, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var address = await FindOwnAsync(id, tracking: true, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        addresses.Remove(address);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (address.IsDefault)
        {
            var next = await addresses.Query(tracking: true)
                .Where(a => a.UserId == address.UserId)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (next is not null)
            {
                next.IsDefault = true;
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ShippingAddress> GetShippingAddressAsync(int id, CancellationToken cancellationToken = default)
    {
        var a = await FindOwnAsync(id, tracking: false, cancellationToken);
        return new ShippingAddress
        {
            RecipientName = a.RecipientName,
            PhoneNumber = a.PhoneNumber,
            Line1 = a.Line1,
            Line2 = a.Line2,
            City = a.City,
            State = a.State,
            PostalCode = a.PostalCode,
            Country = a.Country
        };
    }

    private async Task<AddressDto> SwitchDefaultAsync(Address address, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        await ClearDefaultAsync(address.UserId, cancellationToken);
        address.IsDefault = true;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return ToDto(address);
    }

    private async Task ClearDefaultAsync(string userId, CancellationToken cancellationToken)
    {
        var current = await addresses.Query(tracking: true)
            .Where(a => a.UserId == userId && a.IsDefault)
            .ToListAsync(cancellationToken);

        if (current.Count == 0)
            return;

        foreach (var a in current)
            a.IsDefault = false;

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Address> FindOwnAsync(int id, bool tracking, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        return await addresses.Query(tracking)
                   .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, cancellationToken)
               ?? throw new NotFoundException("Address not found.");
    }

    private static void Apply(Address address, AddressRequest request)
    {
        address.RecipientName = request.RecipientName.Trim();
        address.PhoneNumber = request.PhoneNumber.Trim();
        address.Line1 = request.Line1.Trim();
        address.Line2 = string.IsNullOrWhiteSpace(request.Line2) ? null : request.Line2.Trim();
        address.City = request.City.Trim();
        address.State = string.IsNullOrWhiteSpace(request.State) ? null : request.State.Trim();
        address.PostalCode = request.PostalCode.Trim();
        address.Country = request.Country.Trim();
    }

    private static AddressDto ToDto(Address a) =>
        new(a.Id, a.RecipientName, a.PhoneNumber, a.Line1, a.Line2, a.City, a.State, a.PostalCode, a.Country, a.IsDefault);
}
