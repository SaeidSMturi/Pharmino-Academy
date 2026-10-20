namespace Application.Interfaces
{
    public interface IPasswordHasher
    {
        string HashPassword(string password);
        bool VerifyPassword(string hashedPassword, string providedPassword);
        bool NeedsRehash(string hashedPassword);
    }
}
