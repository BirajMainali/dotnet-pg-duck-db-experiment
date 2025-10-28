namespace PgDuckExperiment.Dto;

public class PptDto
{
    public string SN { get; set; }

    // EmployeeId Requirements
    // 1. Must be exactly 6 characters
    // 2. Must start with 'EMP'
    // 3. Must be unique
    public string EmployeeId { get; set; }

    // FirstName Requirements
    // 1. Must be at least 3 characters long
    // 2. Must not contain spaces
    // 3. Must not contain special characters
    // 4. Must start with uppercase
    // 5. Cannot end with a digit
    public string FirstName { get; set; }

    // MiddleName Requirements (optional)
    // 1. Maximum length 20
    // 2. Must not contain numbers
    public string? MiddleName { get; set; }

    // LastName Requirements
    // 1. Must be at least 2 characters
    // 2. Must not contain special characters
    // 3. Must not contain consecutive repeating letters
    public string LastName { get; set; }

    // Email Requirements
    // 1. Must be a valid email format
    // 2. Maximum 50 characters
    public string? Email { get; set; }

    // Phone Requirements
    // 1. Must be digits only
    // 2. Minimum 7 digits, maximum 15 digits
    public string? Phone { get; set; }

    // DateOfBirth Requirements (optional)
    // 1. Must be a past date
    // 2. Must be at least 18 years old if provided
    public DateTime? DateOfBirth { get; set; }

    // Gender Requirements (optional)
    // 1. Must be one of: Male, Female, Other
    public string? Gender { get; set; }

    // NationalId Requirements (optional)
    // 1. Must be alphanumeric
    // 2. Maximum 20 characters
    public string? NationalId { get; set; }

    // Country Requirements (optional)
    // 1. Must be a valid country name
    public string? Country { get; set; }

    // State Requirements (optional)
    // 1. Maximum 50 characters
    public string? State { get; set; }

    // City Requirements (optional)
    // 1. Maximum 50 characters
    public string? City { get; set; }

    // AddressLine Requirements (optional)
    // 1. Maximum 100 characters
    public string? AddressLine { get; set; }

    // ZipCode Requirements (optional)
    // 1. Must be digits only
    // 2. Maximum 10 characters
    public string? ZipCode { get; set; }
}