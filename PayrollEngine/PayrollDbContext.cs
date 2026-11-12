using Microsoft.EntityFrameworkCore;

namespace PayrollEngine;

public sealed class PayrollDbContext(DbContextOptions<PayrollDbContext> options)
    : DbContext(options)
{
}
