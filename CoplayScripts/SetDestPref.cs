using UnityEngine;

public class SetDestPref
{
    public static string Execute()
    {
        PlayerPrefs.SetString("Destination", "Target_Bedroom");
        PlayerPrefs.Save();
        return "Destination=Target_Bedroom";
    }
}
