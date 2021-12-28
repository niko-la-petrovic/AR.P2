using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace AR.P2.Manager.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext([NotNullAttribute] DbContextOptions options) : base(options)
        {
        }

        protected ApplicationDbContext()
        {
        }
    }
}
