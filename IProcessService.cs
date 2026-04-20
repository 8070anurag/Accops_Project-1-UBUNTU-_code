using System;
using System.Collections.Generic;

namespace App.Net
{
    // Combined interface that extends all individual service interfaces
    // Kept for backward compatibility
    public interface IProcessService : IExcludeListService, ISuspendResumeService, 
                                       IResourceRestrictionService, IBlockService,
                                       IPriorityService, IMemoryReleaseService,
                                       IClipboardService
    {
    }
}
