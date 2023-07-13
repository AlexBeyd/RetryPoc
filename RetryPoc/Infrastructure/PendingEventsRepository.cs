using Microsoft.EntityFrameworkCore;
using RetryPoc.Application.Models;

namespace RetryPoc.Infrastructure;

public class PendingEventsRepository : IEventsRepository<PendingEventObject>
{
    private readonly ICapDbContext _pendingEventsContext;

    public PendingEventsRepository(ICapDbContext capDbContext)
    {
        _pendingEventsContext = capDbContext;
    }

    public async Task<PendingEventObject> AddAsync(PendingEventObject entity)
    {
        var result = (await _pendingEventsContext.PendingEvents.AddAsync(entity)).Entity;
        _pendingEventsContext.SaveChanges();

        return result;
    }

    public async Task DeleteAsync(PendingEventObject entity)
    {
        await Task.Run(() => _pendingEventsContext.PendingEvents.Remove(entity));
        _pendingEventsContext.SaveChanges();
    }

    public async Task<IEnumerable<PendingEventObject>> FindAsync(Func<PendingEventObject, bool> value)
    {
        return await Task.Run(() => _pendingEventsContext.PendingEvents?.Where(value));
    }

    public async Task<bool> IsFound(int id)
    {
        return (await FindAsync(e => e.RelatedRequestId == id)).Any();
    }

    public async Task<IEnumerable<PendingEventObject>> ListAllAsync()
    {
        return await _pendingEventsContext.PendingEvents.ToListAsync();
    }
}
