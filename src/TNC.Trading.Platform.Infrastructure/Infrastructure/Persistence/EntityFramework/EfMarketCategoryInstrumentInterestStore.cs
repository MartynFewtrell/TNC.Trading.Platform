using System.Data;
using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class EfMarketCategoryInstrumentInterestStore(
    PlatformDbContext dbContext,
    IAppliedBrokerEnvironmentContextResolver? contextResolver = null,
    TimeProvider? timeProvider = null) :
    IMarketCategoryInstrumentInterestReader,
    IMarketCategoryInstrumentInterestWriter
{
    public async Task<MarketCategoryInstrumentInterestState> ReadAsync(
        BrokerEnvironmentKind appliedBrokerEnvironment,
        CancellationToken cancellationToken)
    {
        var environmentId = await EfMarketCategoryInstrumentEnvironmentResolver.ResolveAppliedIdAsync(
            dbContext, contextResolver, appliedBrokerEnvironment, cancellationToken, requireExecutable: false).ConfigureAwait(false);
        var state = await dbContext.MarketCategoryInterestStates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.BrokerEnvironmentId == environmentId, cancellationToken).ConfigureAwait(false);
        var selectedCodes = await dbContext.MarketCategoryInterests.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .Select(item => item.CategoryCode)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var categories = await dbContext.MarketCategories.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .Select(item => item.Code)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var interests = categories
            .Select(code => new MarketCategoryInstrumentInterest(code, selectedCodes.Contains(code, StringComparer.Ordinal)))
            .Concat(selectedCodes.Where(code => !categories.Contains(code, StringComparer.Ordinal))
                .Select(code => new MarketCategoryInstrumentInterest(code, true)))
            .OrderBy(item => item.CategoryCode, StringComparer.Ordinal)
            .ToArray();
        return new(state?.Revision ?? 0, interests);
    }

    public async Task<long> SaveAsync(
        BrokerEnvironmentKind appliedBrokerEnvironment,
        IReadOnlyList<MarketCategoryInstrumentInterest> interests,
        long expectedRevision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(interests);
        if (expectedRevision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedRevision));
        }

        var environmentId = await EfMarketCategoryInstrumentEnvironmentResolver.ResolveAppliedIdAsync(
            dbContext, contextResolver, appliedBrokerEnvironment, cancellationToken, requireExecutable: false).ConfigureAwait(false);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        await MarketCategoryInstrumentSqlLock.AcquireAsync(
            dbContext, $"MarketCategoryInstrumentInterests/{environmentId:N}", "Exclusive", cancellationToken).ConfigureAwait(false);
        var state = await dbContext.MarketCategoryInterestStates
            .SingleOrDefaultAsync(item => item.BrokerEnvironmentId == environmentId, cancellationToken).ConfigureAwait(false);
        var actualRevision = state?.Revision ?? 0;
        if (actualRevision != expectedRevision)
        {
            throw new MarketCategoryInstrumentInterestConflictException(expectedRevision, actualRevision);
        }

        var categories = await dbContext.MarketCategories.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .Select(item => item.Code)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var currentCategorySet = categories.ToHashSet(StringComparer.Ordinal);
        var previouslySelected = await dbContext.MarketCategoryInterests.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .Select(item => item.CategoryCode)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var suppliedByCode = new Dictionary<string, bool>(StringComparer.Ordinal);
        var dormantSelected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var interest in interests)
        {
            if (string.IsNullOrWhiteSpace(interest.CategoryCode)
                || interest.CategoryCode.Length > 128
                || !suppliedByCode.TryAdd(interest.CategoryCode, interest.IsSelected))
            {
                throw new ArgumentException("Interest updates must contain unique, valid category codes.", nameof(interests));
            }

            if (!currentCategorySet.Contains(interest.CategoryCode))
            {
                if (!interest.IsSelected || !previouslySelected.Contains(interest.CategoryCode, StringComparer.Ordinal))
                {
                    throw new ArgumentException("Interest can only be newly selected for a current market category.", nameof(interests));
                }

                dormantSelected.Add(interest.CategoryCode);
            }
        }

        if (currentCategorySet.Any(code => !suppliedByCode.ContainsKey(code)))
        {
            throw new ArgumentException("Interest updates must describe the entire current category set.", nameof(interests));
        }

        var desiredSelected = suppliedByCode.Where(item => currentCategorySet.Contains(item.Key) && item.Value)
            .Select(item => item.Key)
            .Concat(dormantSelected)
            .ToHashSet(StringComparer.Ordinal);
        var currentSelected = await dbContext.MarketCategoryInterests
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        dbContext.MarketCategoryInterests.RemoveRange(currentSelected.Where(item =>
            currentCategorySet.Contains(item.CategoryCode) && !desiredSelected.Contains(item.CategoryCode)));
        var existingSelected = currentSelected.Select(item => item.CategoryCode).ToHashSet(StringComparer.Ordinal);
        var selectedAtUtc = (timeProvider ?? TimeProvider.System).GetUtcNow();
        dbContext.MarketCategoryInterests.AddRange(desiredSelected
            .Where(code => !existingSelected.Contains(code))
            .Select(code => new MarketCategoryInterestEntity
            {
                BrokerEnvironmentId = environmentId,
                CategoryCode = code,
                SelectedAtUtc = selectedAtUtc
            }));

        var newRevision = checked(actualRevision + 1);
        if (state is null)
        {
            dbContext.MarketCategoryInterestStates.Add(new MarketCategoryInterestStateEntity
            {
                BrokerEnvironmentId = environmentId,
                Revision = newRevision
            });
        }
        else
        {
            state.Revision = newRevision;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await EfMarketCategoryInstrumentEnvironmentResolver.VerifyAppliedAtCommitAsync(
            dbContext, contextResolver, environmentId, appliedBrokerEnvironment, null, cancellationToken, requireExecutable: false).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return newRevision;
    }
}
