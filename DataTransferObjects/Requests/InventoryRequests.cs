using System.ComponentModel.DataAnnotations;

namespace Cap1.LogiTrack.DataTransferObjects.Requests;

public class CreateInventoryItemRequest
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Quantity is required")]
    [Range(0, int.MaxValue, ErrorMessage = "Quantity must be a non-negative number")]
    public int Quantity { get; set; }
    
    [StringLength(200, ErrorMessage = "Location cannot exceed 200 characters")]
    public string? Location { get; set; }
}

public class UpdateInventoryItemRequest
{
    [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    public string? Name { get; set; }
    
    [Range(0, int.MaxValue, ErrorMessage = "Quantity must be a non-negative number")]
    public int? Quantity { get; set; }
    
    [StringLength(200, ErrorMessage = "Location cannot exceed 200 characters")]
    public string? Location { get; set; }
}