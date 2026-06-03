namespace Repo_Into_Graph.Samples;

public class UserService
{
    private readonly UserRepository _repository;

    public UserService(UserRepository repository)
    {
        _repository = repository;
    }

    public async Task<User> GetUserByIdAsync(int userId)
    {
        var user = await _repository.GetByIdAsync(userId);
        if (user != null)
        {
            LogUserAccess(user);
        }
        return user;
    }

    public async Task<bool> CreateUserAsync(User user)
    {
        ValidateUser(user);
        var result = await _repository.SaveAsync(user);
        if (result)
        {
            NotifyUserCreated(user);
        }
        return result;
    }

    private void ValidateUser(User user)
    {
        if (string.IsNullOrEmpty(user.Name))
        {
            throw new ArgumentException("User name cannot be empty");
        }
    }

    private void LogUserAccess(User user)
    {
        System.Console.WriteLine($"User {user.Name} accessed");
    }

    private void NotifyUserCreated(User user)
    {
        System.Console.WriteLine($"User {user.Name} created");
    }
}
