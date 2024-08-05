using BNet.ASP.MVC.Pagination.Sample.Models;
using Microsoft.EntityFrameworkCore;

namespace BNet.ASP.MVC.Pagination.Sample.Repositories
{
    public class MyDBContext:DbContext
    {

        public MyDBContext(DbContextOptions<MyDBContext> options) : base(options) { }
        string ConnectionString = "Data Source=(LocalDB)\\MSSQLLocalDB;AttachDbFilename="+Environment.CurrentDirectory+"\\AccountDB.mdf;Integrated Security=True;Connect Timeout=30";
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseSqlServer(ConnectionString);
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<users_table>(entity =>
            {
                entity.HasKey(x => x.DBID).HasName("users_table_entity");
            });
        }
        public DbSet<users_table> users_table { get; set; }

    }
}
