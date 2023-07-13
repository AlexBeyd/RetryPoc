using RetryPoc.Application.Models;

namespace RetryPoc.Infrastructure;

public interface IEventsRepository<T>
{
    Task DeleteAsync(T entity);
    Task<IEnumerable<T>> FindAsync(Func<T, bool> value);
    Task<bool> IsFound(int id);
    Task<T> AddAsync(T entity);
    Task<IEnumerable<T>> ListAllAsync();
}
