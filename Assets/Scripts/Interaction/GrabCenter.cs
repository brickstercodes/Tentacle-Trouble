using UnityEngine;

namespace Octo.Interaction
{
    /// <summary>
    /// Marker component — attach to the mesh GameObject (e.g. DWDaveMesh).
    /// OctoGrabSystem auto-finds this to centre the grab-radius sphere on the
    /// actual mesh rather than the skeleton root pivot.
    /// </summary>
    public class GrabCenter : MonoBehaviour { }
}
