using InfotecsTestTask.Entities;
using Microsoft.EntityFrameworkCore;
using CmdScale.EntityFrameworkCore.TimescaleDB;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
namespace InfotecsTestTask.DataAccess
{
    public class AppDbContext: DbContext
    {

        /// <summary>
        /// Конструктор для Entity Framework Tools. Вызывается при создании миграций
        /// </summary>
        public AppDbContext() { }

        /// <summary>
        /// Конструктор для DI-контейнера.
        /// </summary>
        /// <param name="options"></param>
        public AppDbContext(DbContextOptions options) : base(options) { }

        public DbSet<Values> Values_ { get; set; }
        public DbSet<Result> Results { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Если не настроен, то мы в режиме миграций, значит указываем, откуда брать строку подключения
            if (!optionsBuilder.IsConfigured)
            {
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json")
                    .Build();

                var connectionString = configuration.GetConnectionString("InfotecsDB");
                optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();
            }
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Values>(entity =>
            {
                entity.HasNoKey();
                entity.IsHypertable(v => v.Date);
                entity.HasIndex(v => v.FileName);
            });

            modelBuilder.Entity<Result>()
                .HasIndex(r => r.FileName)
                .IsUnique();
              
        }
    }
}
