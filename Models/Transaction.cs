namespace Models;
public class Transaction
{
    public int Id { get; set; }
    public string Type { get; set; } = ""; // "pengeluaran" atau "pemasukan"
    public int Amount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
