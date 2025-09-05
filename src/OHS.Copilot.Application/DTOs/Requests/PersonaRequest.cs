using System.ComponentModel.DataAnnotations;
using OHS.Copilot.Application.Interfaces;

namespace OHS.Copilot.Application.DTOs.Requests;

public class PersonaRequest
{
    [Required]
    public PersonaType Type { get; set; } = PersonaType.Inspector;
    
    public Dictionary<string, string> CustomProfile { get; set; } = [];
    public List<string> Preferences { get; set; } = [];
    public string? Description { get; set; }
}
