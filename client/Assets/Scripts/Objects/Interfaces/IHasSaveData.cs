using System.Collections.Generic;

public interface IHasSaveData
{
    public void LoadData(Dictionary<string, object> data);
    public void DumpData(Dictionary<string, object> data);
}