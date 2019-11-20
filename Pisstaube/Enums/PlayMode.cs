using System.ComponentModel;
using System.Runtime.Serialization;

namespace Pisstaube.Enums
{
    [DefaultValue(All)]
    public enum PlayMode
    {
        All = -1,
        Osu = 0,
        Taiko,
        Catch,
        Mania
    }
}