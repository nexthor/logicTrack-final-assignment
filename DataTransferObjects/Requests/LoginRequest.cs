using System.ComponentModel.DataAnnotations;

namespace Cap1.LogiTrack.DataTransferObjects.Requests;

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public string Password { get; set; } = string.Empty;
}