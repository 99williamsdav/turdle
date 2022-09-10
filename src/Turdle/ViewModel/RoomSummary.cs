using Turdle.Models;

namespace Turdle.ViewModel;

public class RoomSummary
{
    public string RoomCode { get; set; }
    
    public MaskedPlayer[] Players { get; set; }
    
    public string AdminAlias { get; set; }
    
    public int RoundNumber { get; set; }
    
    public RoundStatus CurrentRoundStatus { get; set; }
}