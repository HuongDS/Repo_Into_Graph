namespace Repo_Into_Graph.Samples;

public class User
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
