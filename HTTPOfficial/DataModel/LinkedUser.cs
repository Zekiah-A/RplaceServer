namespace HTTPOfficial.DataModel;

// Links a canvas user to an account
public class LinkedUser
{
    public int Id { get; set; }
    public int UserIntId { get; set; }

    public int InstanceId { get; set; }
    // Navigation property to linked instance
    public Instance Instance { get; set; }

    public int AccountId { get; set; }
    // Navigation property to linked account
    public Account Account { get; set; }
}