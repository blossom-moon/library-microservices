using System.ComponentModel.DataAnnotations;

namespace Library.Loans.Bll.Dto;

public class LoanDto
{
    public long LoanId { get; set; }
    public long ReaderId { get; set; }
    public string Status { get; set; } = null!;
    public DateTime LoanDate { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public decimal FineAmount { get; set; }
    public List<LoanItemDto> Items { get; set; } = new();
}

public class LoanItemDto
{
    public long BookId { get; set; }
    public string BookTitle { get; set; } = null!;
    public DateTime? ReturnedAt { get; set; }
}

/// <summary>
/// Базова модельна валідація (DataAnnotations) — ASP.NET Core відхиляє запит
/// з кодом 400 ще до виклику сервіса. Бізнес-валідація (чи існує читач, чи
/// доступні книги) лишається в BLL.
/// </summary>
public class CreateLoanDto
{
    [Range(1, long.MaxValue, ErrorMessage = "ReaderId must be a positive number.")]
    public long ReaderId { get; set; }

    [Required]
    public DateTime DueDate { get; set; }

    [Required, MinLength(1, ErrorMessage = "At least one book must be selected.")]
    public List<long> BookIds { get; set; } = new();
}
