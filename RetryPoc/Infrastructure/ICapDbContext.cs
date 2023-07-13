using Microsoft.EntityFrameworkCore;
using RetryPoc.Application.Models;

namespace RetryPoc.Infrastructure;

public interface ICapDbContext
{
    DbSet<PendingEventObject> PendingEvents { get; }

    int SaveChanges();
}
