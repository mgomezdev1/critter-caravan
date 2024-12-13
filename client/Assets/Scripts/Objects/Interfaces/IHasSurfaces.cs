using System.Collections.Generic;

public interface IHasSurfaces
{
    public IEnumerable<Surface> GetSurfaces();
}