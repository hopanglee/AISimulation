using System.Collections.Generic;

public interface IHasExtraSenseAreas
{
    /// <summary>
    /// Sensor가 추가로 감지 대상으로 포함해야 하는 Area 목록을 제공합니다.
    /// </summary>
    List<Area> GetExtraSenseAreas();
}


