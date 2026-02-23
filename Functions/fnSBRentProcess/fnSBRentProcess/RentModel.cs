using System;

namespace fnSBRentProcess;

public class RentModel
{
    public string? nome { get; set; }
    public string? email { get; set; }
    public string? modelo { get; set; }
    public int ano { get; set; }
    public string? tempoAluguel { get; set; }
    public DateTime data { get; set; }
}