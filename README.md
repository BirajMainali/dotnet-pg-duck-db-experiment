# Optimized Bulk Excel Import to PostgreSQL

This project demonstrates an **optimized approach to importing large Excel datasets (80K+ rows) into PostgreSQL** in seconds, using `DuckDB` for validation and transformation, and PostgreSQL's `COPY` for fast bulk inserts.

---

## Key Optimizations

### 1. Avoid Loading Excel into Memory
- Excel files are **kept in a temporary folder** or can be read directly from cloud storage (e.g., S3).  
- No full-memory load, preventing memory spikes.

### 2. Efficient Validation
- A **single `DuckDB` query** validates all entries at once.  
- Avoids looping or per-record checks in memory, significantly reducing validation time.

### 3. Bulk Insert Without EF Overhead
- Transformed data is exported to `.csv` using `DuckDB`.  
- PostgreSQL `COPY` command imports data **directly**, bypassing Entity Framework overhead.  
- Supports 80K+ records in **under 3 seconds**.

---

## Performance Comparison

| Method                    | Time Taken |
|----------------------------|-----------|
| Optimized Import           | 3.46s     |
| Batched EF Insert          | 1m 26s    |
| Regular EF Insert          | 1m 27s    |

**Screenshots:**

- **Optimized Import**  
![Optimized Import](https://github.com/user-attachments/assets/d55260d6-919a-4b27-a989-85d38e833744)  

- **Batched EF Insert**  
![Batched EF](https://github.com/user-attachments/assets/bc37db09-7afc-4a2e-95ae-e8c449b1a383)  

- **Regular EF Insert**  
![Regular EF](https://github.com/user-attachments/assets/605fe7f1-567c-4b4d-b46b-9e7cfab443cf)

---

## Sample DTO with Validation Rules

```csharp
public class PptDto
{
    public string SN { get; set; }

    // EmployeeId: 6 characters, starts with 'EMP', unique
    public string EmployeeId { get; set; }

    // FirstName: ≥3 chars, no spaces, no special chars, starts uppercase, cannot end with digit
    public string FirstName { get; set; }

    // MiddleName (optional): max 20 chars, no numbers
    public string? MiddleName { get; set; }

    // LastName: ≥2 chars, no special chars, no consecutive repeating letters
    public string LastName { get; set; }

    // Email (optional): valid format, max 50 chars
    public string? Email { get; set; }

    // Phone (optional): digits only, 7–15 digits
    public string? Phone { get; set; }

    // DateOfBirth (optional): past date, ≥18 years old
    public DateTime? DateOfBirth { get; set; }

    // Gender (optional): Male, Female, Other
    public string? Gender { get; set; }

    // NationalId (optional): alphanumeric, max 20 chars
    public string? NationalId { get; set; }

    // Country (optional): valid country name
    public string? Country { get; set; }

    // State & City (optional): max 50 chars
    public string? State { get; set; }
    public string? City { get; set; }

    // AddressLine (optional): max 100 chars
    public string? AddressLine { get; set; }

    // ZipCode (optional): digits only, max 10 chars
    public string? ZipCode { get; set; }
}
