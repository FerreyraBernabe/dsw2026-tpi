using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dsw2026Tpi.Data.Configuration
{
    public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
    {
        public void Configure(EntityTypeBuilder<Appointment> builder)
        {
            builder.ToTable("Appointments");
            builder.Property(a => a.Status).HasMaxLength(20);
            builder.Property(a => a.Reason).HasMaxLength(300);
            builder.HasIndex(a => a.AvailabilitySlotId).HasFilter("[Status] = 0").IsUnique();
        }
    }
}
