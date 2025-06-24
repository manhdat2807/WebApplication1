using Microsoft.EntityFrameworkCore;

namespace testti.Models
{
    public class ApplicationDbcontext : DbContext
    {
        public ApplicationDbcontext(DbContextOptions options) : base(options)
        {
        }
        public DbSet<Usercs> users {  get; set; }
        public DbSet<Attendance> Attendances { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
        }
    }
}
