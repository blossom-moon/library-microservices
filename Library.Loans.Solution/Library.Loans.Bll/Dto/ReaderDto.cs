using System.ComponentModel.DataAnnotations;

namespace Library.Loans.Bll.Dto;

public class ReaderDto
{
    public long ReaderId { get; set; }
    public string CardNumber { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Phone { get; set; }
}

/// <summary>
/// Базова модельна валідація (DataAnnotations) — ASP.NET Core ([ApiController])
/// відхиляє запит з кодом 400 ще ДО виклику сервіса, якщо ці правила порушено.
/// Бізнес-валідація (унікальність картки тощо) лишається в BLL — їй потрібен стан із БД.
/// </summary>
public class CreateReaderDto
{
    [Required(ErrorMessage = "CardNumber is required.")]
    [StringLength(20, MinimumLength = 3)]
    public string CardNumber { get; set; } = null!;

    [Required(ErrorMessage = "FullName is required.")]
    [StringLength(200, MinimumLength = 2)]
    public string FullName { get; set; } = null!;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Phone]
    public string? Phone { get; set; }
}
