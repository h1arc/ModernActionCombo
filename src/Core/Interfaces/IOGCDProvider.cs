using System.Collections.Generic;

namespace ModernActionCombo.Core.Interfaces
{
    /// <summary>
    /// Interface for job providers that can suggest oGCDs to use in the current window.
    /// </summary>
    public interface IOGCDProvider
    {
        IEnumerable<uint> GetSuggestedOGCDs();
    }
}