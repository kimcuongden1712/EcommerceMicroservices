using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs.Basket;

public class CartDto
{
    public CartDto()
    {
    }

    public CartDto(string username)
    {
        Username = username;
    }

    public string Username { get; set; }

    [EmailAddress]
    public string EmailAddress
    {
        get => string.IsNullOrWhiteSpace(_emailAddress) ? Username  : _emailAddress;
        set => _emailAddress = value;
    }

    private string _emailAddress;

    public List<CartItemDto> Items { get; set; } = new();

    public decimal TotalPrice => Items.Sum(item => item.ItemPrice * item.Quantity);
}