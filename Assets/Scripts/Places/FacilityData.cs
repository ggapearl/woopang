using System;

[System.Serializable]
public class FacilityData
{
    public string type { get; set; }
    public string name { get; set; }
    public double latitude { get; set; }
    public double longitude { get; set; }
    public string address { get; set; }
    public string extra_info { get; set; }
    public float altitude { get; set; }
    public string main_photo { get; set; }
}
