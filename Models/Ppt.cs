using System.ComponentModel.DataAnnotations.Schema;

namespace PgDuckExperiment.Models;

[Table("ppts")]
public class Ppt
{
    public Guid Id { get; set; }
    public string EmployeeId { get; set; }
    public string FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? NationalId { get; set; }
    public string? Country { get; set; }
    public string? State { get; set; }
    public string? City { get; set; }
    public string? AddressLine { get; set; }
    public string? ZipCode { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}