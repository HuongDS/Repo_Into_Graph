namespace Repo_Into_Graph.Samples;

public interface IRepository<T>
{
    Task<T?> GetByIdAsync(int id);
    Task<bool> SaveAsync(T entity);
}

public class UserRepository : IRepository<User>
{
    private readonly List<User> _users = new();

    public async Task<User?> GetByIdAsync(int id)
    {
        await Task.Delay(10);
        return _users.FirstOrDefault(u => u.Id == id);
    }

    public async Task<bool> SaveAsync(User entity)
    {
        await Task.Delay(10);
        var existing = _users.FirstOrDefault(u => u.Id == entity.Id);
        if (existing != null)
        {
            _users.Remove(existing);
        }
        _users.Add(entity);
        return true;
    }
}
