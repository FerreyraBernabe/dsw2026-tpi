namespace Dsw2026Tpi.Domain.Entities
{
    public class Doctor : EntityBase
    {
        public string Name { get; set; }
        public string LicenseNumber { get; set; }
        public Specialty Speciality { get; set; }
        public Guid SpecialityId { get; set; }
        public List<Availability> AvailabilityRules { get; set; }

        protected Doctor()
        {
        }

        public Doctor(string name, string licenseNumber, Guid specialityId)
        {
            Name = name;
            LicenseNumber = licenseNumber;
            SpecialityId = specialityId;
        }

        public void Update(string name, string licenseNumber, Guid specialityId)
        {
            Name = name;
            LicenseNumber = licenseNumber;
            SpecialityId = specialityId;
        }

        public string AvailabilityStatus()
        {
            return AvailabilityRules != null &&
                AvailabilityRules.Any(a =>
                    !a.Deleted &&
                    a.Slots != null &&
                    a.Slots.Any(s => s.Status == AvailabilitySlotStatus.AVAILABLE))
                ? "Available"
                : "Not Available";
        }
    }
}
